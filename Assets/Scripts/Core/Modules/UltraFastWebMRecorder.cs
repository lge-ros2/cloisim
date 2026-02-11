using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Rendering;

public class UltraFastWebMRecorder : MonoBehaviour
{
	[Header("Default parameters")]
	[SerializeField] int _width = 960;
	[SerializeField] int _height = 540;
	[SerializeField, Range(1, 240)] int _targetFps = 24;

	[Header("Output")]
	[SerializeField] string _outputDir;
	[SerializeField] string _baseName = "capture"; // capture.webm

	[Header("Performance/Stability")]
	[SerializeField, Tooltip("If the write queue exceeds this number, keep only the latest frames (drops)")]
	int _maxQueuedFrames = 128;

	[SerializeField, Tooltip("Allow Frame Drops in Write Errors/Delays")]
	bool _dropWhenBackedUp = true;

	[Header("Encoding option for WebM(VP8)")]
	[Tooltip("Quality (lower definition/capacity ↑). Recommended 16-28, 24-30 smaller/ faster")]
	[Range(4, 63)] public int crf = 24;

	[Tooltip("Target bitrate (maximum guide), e.g. \"4M\", \"8M\". When empty, the CRF is mainly")]
	public string videoBitrate = "6M";

	[SerializeField, Tooltip("Real-time mode. If you turn it on, the encoder will speed up")]
	public bool realtimeDeadline = true;

	[Tooltip("Speed/quality switch (value↑= speed↑/quality↓)8~12 is fast, 4 to 6 are angry")]
	[Range(0, 15)] public int cpuUsed = 8;

	[Tooltip("Automatic multi-thread setting (0=automatic)")]
	public int threads = 0;

	[Tooltip("Row-based Multi-Threading")]
	public bool rowMT = true;

	[Tooltip("Path to the ffmpeg executable (search in PATH if left empty)")]
	public string ffmpegPath = "ffmpeg";

	public bool IsRecording { get; private set; }
	public int Width => _width;
	public int Height => _height;
	public int TargetFps => _targetFps;

	private Camera _cam;
	private RenderTexture _rt;
	private int _bytesPerFrame; // RGBA32
	private float _interval;
	private float _accum;

	private ConcurrentQueue<byte[]> _queue = new();
	private Thread _writerThread;
	private volatile bool _writerRunning;

	private readonly object _poolLock = new();
	private Queue<byte[]> _bufferPool = new();

	// FFmpeg
	private System.Diagnostics.Process _ffmpeg;
	private Stream _ffmpegStdin;
	private string _absDir;
	private string _outPath; // .webm

	void Awake()
	{
		Application.runInBackground = true;
		_cam = Camera.main;
		if (_cam == null) {
			Debug.LogError("[WebMRecorder] No Main Camera");
			enabled = false;
			return;
		}

		PrepareTargets(_width, _height);
		ApplyFps(_targetFps);
		PrepareOutput();
	}

	void OnDestroy() => StopCaptureInternal();
	void OnApplicationQuit() => StopCaptureInternal();

	public void SetResolution(int w, int h)
	{
		if (w <= 0 || h <= 0)
			return;
		_width = w; _height = h;
		PrepareTargets(_width, _height, true);
		Debug.Log($"[WebMRecorder] Resolution = {_width}x{_height}");
	}

	public void SetFps(int fps)
	{
		_targetFps = Mathf.Clamp(fps, 1, 240);
		ApplyFps(_targetFps);
		Debug.Log($"[WebMRecorder] Target FPS = {_targetFps}");
	}

	public void SetOutput(string baseName, string dir = "recordings")
	{
		if (!string.IsNullOrEmpty(dir))
			_outputDir = dir;
		if (!string.IsNullOrEmpty(baseName))
			_baseName = baseName;
		else
			_baseName = $"manual_rec_simulation_{DateTime.Now.ToString("yyyyMMddHHmmss")}";
		PrepareOutput();
	}

	public bool StartCapture()
	{
		if (IsRecording)
			return false;

		Directory.CreateDirectory(_absDir);
		_outPath = Path.Combine(_absDir, $"{_baseName}.webm");

		if (!StartFFmpeg())
		{
			Debug.LogError("[WebMRecorder] FFmpeg fail to start. Check path");
			return false;
		}

		_accum = 0f;
		_writerRunning = true;
		_writerThread = new Thread(WriterLoop) { IsBackground = true, Name = "WebMWriter" };
		_writerThread.Start();
		IsRecording = true;

		Debug.Log($"[WebMRecorder] ▶ START: {_outPath}");
		return true;
	}

	public void StopCapture()
	{
		if (!IsRecording)
			return;
		StopCaptureInternal();
		Debug.Log($"[WebMRecorder] ■ STOP: {_outPath}");
	}

	void Update()
	{
		if (!IsRecording)
			return;

		_accum += Time.deltaTime;
		if (_accum < _interval)
			return;
		_accum -= _interval;

		_cam.targetTexture = _rt;
		_cam.Render();
		_cam.targetTexture = null;

		AsyncGPUReadback.Request(_rt, 0, request =>
		{
			if (!IsRecording)
				return;

			if (request.hasError)
			{
				Debug.LogWarning("[WebMRecorder] GPUReadback error");
				return;
			}

			var data = request.GetData<byte>(); // RGBA32
			if (data.Length != _bytesPerFrame) return;

			var buf = RentBuffer();
			data.CopyTo(buf);

			if (_queue.Count > _maxQueuedFrames && _dropWhenBackedUp)
			{
				if (_queue.TryDequeue(out var old)) ReturnBuffer(old);
			}
			_queue.Enqueue(buf);
		});
	}

	void WriterLoop()
	{
		try
		{
			while (_writerRunning || !_queue.IsEmpty)
			{
				if (!_queue.TryDequeue(out var f))
				{
					Thread.Sleep(1);
					continue;
				}

				_ffmpegStdin.Write(f, 0, f.Length);
				ReturnBuffer(f);
			}
		}
		catch (Exception e)
		{
			Debug.LogError($"[WebMRecorder] WriterLoop error: {e.Message}");
		}
	}

	void StopCaptureInternal()
	{
		if (!IsRecording)
			return;
		IsRecording = false;

		_writerRunning = false;
		_writerThread?.Join(2000);

		try { _ffmpegStdin?.Flush(); } catch { }
		try { _ffmpegStdin?.Close(); } catch { }

		if (_ffmpeg != null && !_ffmpeg.HasExited)
		{
			if (!_ffmpeg.WaitForExit(4000))
			{
				try { _ffmpeg.Kill(); } catch { }
			}
		}

		_ffmpeg?.Dispose();
		_ffmpeg = null;
		_ffmpegStdin = null;
	}

	void PrepareTargets(int w, int h, bool recreate = false)
	{
		_bytesPerFrame = w * h * 4; // RGBA32
		if (_rt != null && !recreate)
			return;

		if (_rt != null)
		{
			_rt.Release();
			Destroy(_rt);
			_rt = null;
		}

		var desc = new RenderTextureDescriptor(w, h, RenderTextureFormat.ARGB32, 0)
		{
			sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear),
			msaaSamples = 1
		};
		_rt = new RenderTexture(desc) { name = "WebMRecorder_RT" };
		_rt.Create();

		lock (_poolLock)
		{
			_bufferPool.Clear();
			var warm = Mathf.Min(_maxQueuedFrames, 32);
			for (int i = 0; i < warm; i++)
				_bufferPool.Enqueue(new byte[_bytesPerFrame]);
		}
	}

	void ApplyFps(int fps) => _interval = 1f / Mathf.Max(1, fps);

	void PrepareOutput()
	{
		_absDir = Path.IsPathRooted(_outputDir)
			? _outputDir
			: Path.Combine(Directory.GetCurrentDirectory(), _outputDir);
	}

	byte[] RentBuffer()
	{
		lock (_poolLock)
		{
			if (_bufferPool.Count > 0) return _bufferPool.Dequeue();
		}
		return new byte[_bytesPerFrame];
	}

	void ReturnBuffer(byte[] buf)
	{
		if (buf == null || buf.Length != _bytesPerFrame) return;
		lock (_poolLock)
		{
			if (_bufferPool.Count < _maxQueuedFrames * 2)
				_bufferPool.Enqueue(buf);
		}
	}

	bool StartFFmpeg()
	{
		// FFmpeg factor:
		// - Read raw RGBA stdin as WxH @ FPS
		// - Encode to libvpx (VP8), yuv420p output
		// - Real time: - Deadline real time, - CPU usage N, row-mt, thread
		var deadline = realtimeDeadline ? "realtime" : "good";
		var rowmt = rowMT ? "1" : "0";
		var thr = (threads > 0) ? threads.ToString() : "0"; // 0=auto
		var bitratePart = string.IsNullOrEmpty(videoBitrate) ? "" : $"-b:v {videoBitrate} ";
		var filter = "vflip,eq=brightness=0.06:contrast=1.05:gamma=1.0";

		var args =
			$"-y " +
			$"-f rawvideo -pix_fmt rgba -s {_width}x{_height} -r {_targetFps} -i pipe:0 " +
			$"-vf \"{filter}\" " +
			$"-c:v libvpx -pix_fmt yuv420p {bitratePart}-crf {crf} " +
			$"-deadline {deadline} -cpu-used {cpuUsed} -row-mt {rowmt} -threads {thr} " +
			$"-r {_targetFps} " +
			$"\"{_outPath}\"";


		var psi = new System.Diagnostics.ProcessStartInfo
		{
			FileName = string.IsNullOrEmpty(ffmpegPath) ? "ffmpeg" : ffmpegPath,
			Arguments = args,
			UseShellExecute = false,
			RedirectStandardInput = true,
			RedirectStandardError = false,
			CreateNoWindow = true
		};

		try
		{
			_ffmpeg = System.Diagnostics.Process.Start(psi);
			_ffmpegStdin = _ffmpeg?.StandardInput.BaseStream;
			Debug.Log($"{ffmpegPath} {args}");
			return _ffmpeg != null && _ffmpegStdin != null;
		}
		catch (Exception e)
		{
			Debug.LogError($"[WebMRecorder] FFmpeg fail to start: {e.Message}");
			return false;
		}
	}
}
