/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using messages = cloisim.msgs;

namespace SensorDevices
{
	/// <summary>
	/// Livox non-repetitive scan pattern support for the Lidar device.
	/// This partial class adds all Livox-specific fields and methods.
	/// </summary>
	public partial class Lidar
	{
		#region "Livox fields"
		private const int XYZComponents = 3;
		private LivoxScanPattern _livoxPattern = null;
		private ComputeBuffer _scanPatternBuffer = null;
		private bool IsLivoxMode => _livoxPattern != null;

		private static ComputeShader ComputeShaderLivoxRayTrace = null;

		// Shader property IDs for Livox-specific parameters
		private static readonly int PID_ScanPattern = Shader.PropertyToID("_ScanPattern");
		private static readonly int PID_PatternSize = Shader.PropertyToID("_PatternSize");
		private static readonly int PID_SampleCount = Shader.PropertyToID("_SampleCount");
		private static readonly int PID_StartIndex = Shader.PropertyToID("_StartIndex");
		private static readonly int PID_DownsampleStep = Shader.PropertyToID("_DownsampleStep");
		private static readonly int PID_XYZOutput = Shader.PropertyToID("_XYZOutput");

		#endregion

		#region "Livox setup"

		private static void LoadLivoxComputeShader()
		{
			if (ComputeShaderLivoxRayTrace == null)
			{
				ComputeShaderLivoxRayTrace = Resources.Load<ComputeShader>("Shader/LivoxLidarRayTrace");
			}
		}

		private static void UnloadLivoxComputeShader()
		{
			if (ComputeShaderLivoxRayTrace != null)
			{
				Resources.UnloadAsset(ComputeShaderLivoxRayTrace);
				ComputeShaderLivoxRayTrace = null;
			}
		}

		/// <summary>
		/// Configure the Lidar to operate in Livox non-repetitive scan mode.
		/// Called from LaserPlugin after reading plugin parameters.
		/// Can be called before or after the standard OnStart/SetupURT.
		/// </summary>
		public void SetupLivoxPattern(string csvPath, int samplesPerCycle, int downsample)
		{
			_livoxPattern = new LivoxScanPattern();
			if (!_livoxPattern.Load(csvPath, samplesPerCycle, downsample))
			{
				Debug.LogError("[Lidar] Failed to load Livox scan pattern");
				_livoxPattern = null;
				return;
			}

			Debug.LogWarning($"[Lidar] Livox pattern loaded: {csvPath}, samples/cycle={samplesPerCycle}, downsample={downsample}");

			// Re-setup messages for Livox mode (different buffer sizes)
			SetupMessages();

			// Re-setup URT if already initialized; otherwise OnStart → SetupURT will handle it
			if (_rtShader != null)
			{
				CleanupLivoxResources();
				CleanupURTResources();
				SetupURT();
			}

			// Drain any stale readback results from the standard-mode queue
			while (_outputQueue.TryDequeue(out _)) { }

			Debug.Log($"[Lidar] Livox mode enabled — {_livoxPattern.TotalRaysPerCycle} rays/cycle, " +
				$"pattern size {_livoxPattern.PatternSize}");
		}

		/// <summary>
		/// Set up Livox-specific message layout.
		/// For PointCloud2Raw: Ranges stores XYZ triples, Intensities stores one value per point.
		/// </summary>
		private void SetupLivoxMessages()
		{
			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			_laserScan.Count = raysPerCycle;
			_laserScan.VerticalCount = 1;

			_totalSamples = raysPerCycle;

			// Ranges stores 3 floats per point (x, y, z) for PointCloud2Raw
			_laserScan.Ranges = new double[_totalSamples * XYZComponents];
			_laserScan.Intensities = new double[_totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, 0.0);

			// Clear and pre-populate range data pool to avoid runtime GC allocations
			while (_rangeDataPool.TryDequeue(out _)) { }
			for (var i = 0; i < RangeDataPoolSize; i++)
				_rangeDataPool.Enqueue(new float[_totalSamples * XYZComponents]);
		}

		/// <summary>
		/// Set up URT resources for Livox mode.
		/// Uses a dedicated compute shader with a pattern buffer.
		/// </summary>
		private void SetupLivoxURT()
		{
			LoadLivoxComputeShader();

			if (!URTSensorManager.Register(GetEntityId()))
			{
				Debug.LogError("[Lidar] Failed to register with URTSensorManager (Livox)");
				return;
			}

			_csRayTrace = Instantiate(ComputeShaderLivoxRayTrace);
			if (_csRayTrace == null)
			{
				Debug.LogError("[Lidar] Failed to instantiate LivoxLidarRayTrace compute shader");
				return;
			}

			_rtShader = URTSensorManager.CreateShader(_csRayTrace);

			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			// Output buffer: 3 floats per ray (x, y, z)
			_rangeOutputBuffer?.Release();
			_rangeOutputBuffer = new ComputeBuffer((int)(raysPerCycle * XYZComponents), sizeof(float));

			// Scratch buffer for 1D dispatch (width = raysPerCycle, height = 1)
			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(_rtShader, raysPerCycle, 1, 1);

			// Upload scan pattern to GPU (persistent buffer for entire CSV)
			_scanPatternBuffer?.Release();
			_scanPatternBuffer = new ComputeBuffer(_livoxPattern.PatternSize, sizeof(float) * 2);
			_scanPatternBuffer.SetData(_livoxPattern.Pattern);

			_urtCmdBuffer = new CommandBuffer { name = "Livox Lidar URT Dispatch" };

			_urtAccelStructGeneration = URTSensorManager.AccelStructGeneration;

			Debug.Log($"[Lidar] Livox URT initialized, rays/cycle={raysPerCycle}, " +
				$"pattern={_livoxPattern.PatternSize}, " +
				$"range=[{_scanRange.min:F2}, {_scanRange.max:F2}]");
		}

		private void SetLivoxPatternParams(CommandBuffer cmd, uint raysPerCycle)
		{
			_rtShader.SetBufferParam(cmd, PID_ScanPattern, _scanPatternBuffer);
			_rtShader.SetIntParam(cmd, PID_PatternSize, _livoxPattern.PatternSize);
			_rtShader.SetIntParam(cmd, PID_SampleCount, (int)raysPerCycle);
			_rtShader.SetIntParam(cmd, PID_StartIndex, _livoxPattern.CurrentStartIndex);
			_rtShader.SetIntParam(cmd, PID_DownsampleStep, _livoxPattern.Downsample);
		}

		/// <summary>
		/// Execute GPU ray trace for Livox mode.
		/// Dispatches rays from the current pattern window and outputs XYZ positions.
		/// </summary>
		private void RebuildURTPerSensorResourcesLivox()
		{
			_rtShader = null;

			// Fence-gated deferred dispose (see standard-lidar rebuild): immediate free
			// can hit an in-flight dispatch → "incompatible ComputeBuffer" / Xid 109.
			URTSensorManager.DeferDispose(_rtTraceScratchBuffer);
			_rtTraceScratchBuffer = null;

			_rtShader = URTSensorManager.CreateShader(_csRayTrace);
			if (_rtShader == null)
			{
				Debug.LogError("[Lidar/Livox] Failed to recreate RT shader after accel struct reset");
				return;
			}

			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;
			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(_rtShader, raysPerCycle, 1, 1);

			// Recreate output buffer and command buffer — same reason as standard lidar rebuild.
			URTSensorManager.DeferDispose(_rangeOutputBuffer);
			_rangeOutputBuffer = new ComputeBuffer((int)(raysPerCycle * XYZComponents), sizeof(float));

			_urtCmdBuffer?.Release();
			_urtCmdBuffer = new CommandBuffer { name = "Livox Lidar URT Dispatch" };

			Debug.Log($"[Lidar/Livox:{DeviceName}] rebuilt gen={URTSensorManager.AccelStructGeneration}"
				+ $" rangeBuf=0x{_rangeOutputBuffer.GetHashCode():X}"
				+ $" traceScratch=0x{(_rtTraceScratchBuffer?.GetHashCode() ?? 0):X}");
		}

		private void ExecuteLivoxRender()
		{
			if (URTSensorManager.AccelStruct == null || _rtShader == null)
				return;

			var currentGen = URTSensorManager.AccelStructGeneration;
			if (_urtAccelStructGeneration != currentGen)
			{
				RebuildURTPerSensorResourcesLivox();
				_urtAccelStructGeneration = currentGen;
				if (_rtShader == null)
					return;
			}

			var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

			var sensorTransform = transform;
			var sensorPos = sensorTransform.position;
			var sensorRight = sensorTransform.right;
			var sensorUp = sensorTransform.up;
			var sensorForward = sensorTransform.forward;
			var sensorWorldPose = new Pose(sensorPos, sensorTransform.rotation);

			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			// Resize scratch buffer if needed (grow-with-headroom + deferred dispose
			// to avoid freeing a buffer still referenced by an in-flight Dispatch)
			_rtTraceScratchBuffer = URTSensorManager.GrowScratch(
				_rtTraceScratchBuffer, _rtShader.GetTraceScratchBufferRequiredSizeInBytes(raysPerCycle, 1, 1));

			// === Record GPU work ===
			_urtCmdBuffer.Clear();

			// 1. Shared BVH
			URTSensorManager.EnsureBVHReady(_urtCmdBuffer);

			// Post-TDR warmup: skip dispatch on the gen-increment frame (BVH build only).
			if (URTSensorManager.IsPostTDRDispatchWarmup())
				return;

			// 2. Bind resources
			BindShaderResources(_urtCmdBuffer);

			// 3. Pattern parameters
			SetLivoxPatternParams(_urtCmdBuffer, raysPerCycle);

			// 4. Range parameters
			SetScanRangeConfigParams(_urtCmdBuffer);

			// 5. Sensor pose
			SetSensorPoseParams(_urtCmdBuffer, sensorPos, sensorRight, sensorUp, sensorForward);

			// 6. Dispatch (1D: raysPerCycle × 1 × 1)
			CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:Dispatch");
			_rtShader.Dispatch(_urtCmdBuffer, _rtTraceScratchBuffer, raysPerCycle, 1, 1);

			// === Execute ===
			CLOiSim.Diagnostics.FreezeWatchdog.Mark("URT:ReadbackWait");
			Graphics.ExecuteCommandBuffer(_urtCmdBuffer);

			// Advance pattern for next frame
			_livoxPattern.AdvanceCycle();

			// --- Async readback ---
			Device.GpuReadbackBegin();
			AsyncGPUReadback.Request(_rangeOutputBuffer, (req) =>
			{
				Device.GpuReadbackEnd();
				if (req.hasError || !req.done)
				{
					Debug.LogWarning("[Lidar] Livox async GPU readback failed");
					return;
				}

				var src = req.GetData<float>();

				// Reuse pooled array to avoid GC allocation on the main thread.
				// Falls back to allocation only if pool is exhausted or size mismatch.
				if (!_rangeDataPool.TryDequeue(out var rangeData) || rangeData.Length != src.Length)
				{
#if UNITY_EDITOR
					if (rangeData != null && rangeData.Length != src.Length)
						Debug.LogWarning($"[Lidar] Livox readback pool size mismatch ({rangeData.Length} vs {src.Length}). Allocating new array.");
					else
						Debug.LogWarning($"[Lidar] Livox readback pool exhausted for {DeviceName}. Allocating new array (size={src.Length}). Consider increasing RangeDataPoolSize.");
#endif
					rangeData = new float[src.Length];
				}

				src.CopyTo(rangeData);

				_outputQueue.Enqueue((capturedTime, sensorWorldPose, rangeData));
				_dataAvailable.Set();
			});

			CLOiSim.Diagnostics.FreezeWatchdog.Mark("idle");
		}

		/// <summary>
		/// Process Livox readback data into LaserScan message.
		/// XYZ triples are copied directly into the Ranges array for PointCloud2Raw output.
		/// </summary>
		private messages.LaserScan ProcessLivoxData(double capturedTime, Pose sensorWorldPose, float[] xyzData)
		{
			_laserScan.Header.Stamp.Set(capturedTime);

			var laserScan = _laserScan;
			laserScan.WorldPose.Position.Set(sensorWorldPose.position);
			laserScan.WorldPose.Orientation.Set(sensorWorldPose.rotation);

			// Copy XYZ triples directly into Ranges array
			var ranges = laserScan.Ranges;
			var count = Math.Min(ranges.Length, xyzData.Length);
			for (var i = 0; i < count; i++)
			{
				ranges[i] = xyzData[i];
			}

			// Intensities: no intensity model for Livox patterns currently,
			// fill with 0.0 (already initialized in SetupLivoxMessages)

			return _laserScan;
		}

		/// <summary>Clean up Livox-specific GPU resources.</summary>
		private void CleanupLivoxResources()
		{
			_scanPatternBuffer?.Release();
			_scanPatternBuffer = null;
		}

		/// <summary>Clean up shared URT resources (used before re-initialization).</summary>
		private void CleanupURTResources()
		{
			_urtCmdBuffer?.Release();
			_urtCmdBuffer = null;

			URTSensorManager.DeferDispose(_rtTraceScratchBuffer);
			_rtTraceScratchBuffer = null;

			URTSensorManager.DeferDispose(_rangeOutputBuffer);
			_rangeOutputBuffer = null;

			if (_csRayTrace != null)
			{
				Destroy(_csRayTrace);
				_csRayTrace = null;
			}

			_rtShader = null;

			URTSensorManager.Unregister(GetEntityId());
		}

		#endregion
	}
}
