/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;

/// <summary>
/// Captures Unity logs in a ring buffer and writes a crash dump
/// (recent logs + system info + stack traces) when a fatal error
/// or unhandled exception occurs.
/// </summary>
public sealed class CrashReporter : IDisposable
{
	private const int MaxLogEntries = 2000;
	private const string DumpDirectoryName = "CrashDumps";

	private readonly LinkedList<string> _logBuffer = new();
	private readonly object _lock = new();
	private readonly object _dumpLock = new();
	private readonly string _dumpRootPath;
	private readonly string _mirrorDumpRootPath;
	private readonly int _mainThreadId;
	private readonly string _systemInfoSnapshot;
	private readonly string[] _sessionLogCandidates;
	private static string _persistentDataPath;
	private readonly string _companyName;
	private readonly string _productName;

	private int _dumpSequence = 0;
	private bool _disposed;

	public CrashReporter()
	{
		_mainThreadId = Thread.CurrentThread.ManagedThreadId;
		_companyName = Application.companyName;
		_productName = Application.productName;

		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		_persistentDataPath = Path.Combine(appDataPath, "unity3d", _companyName, _productName);

		_dumpRootPath = BuildLocalDumpRootPath();
		_mirrorDumpRootPath = BuildMirrorDumpRootPath(_dumpRootPath);
		_systemInfoSnapshot = CaptureSystemInfoSnapshot();
		_sessionLogCandidates = BuildSessionLogCandidates();

		Application.logMessageReceivedThreaded += OnLogMessageReceived;
		AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		Application.logMessageReceivedThreaded -= OnLogMessageReceived;
		AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;

		GC.SuppressFinalize(this);
	}

	private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
	{
		var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
		var entry = $"[{timestamp}] [{type}] {condition}";

		if (type is LogType.Exception or LogType.Error)
		{
			entry += $"\n  StackTrace: {stackTrace}";
		}

		lock (_lock)
		{
			_logBuffer.AddLast(entry);

			while (_logBuffer.Count > MaxLogEntries)
			{
				_logBuffer.RemoveFirst();
			}
		}

		if (type == LogType.Exception)
		{
			WriteDump("UnityException", condition, stackTrace);
		}
	}

	private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
	{
		var ex = e.ExceptionObject as Exception;
		WriteDump(
			"UnhandledException",
			ex?.Message ?? "Unknown",
			ex?.StackTrace ?? "No stack trace");
	}

	private void WriteDump(string reason, string message, string stackTrace)
	{
		try
		{
			string dumpDir;
			string mirroredDumpDir = null;
			lock (_dumpLock)
			{
				Directory.CreateDirectory(_dumpRootPath);

				var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
				var sequence = ++_dumpSequence;
				dumpDir = Path.Combine(_dumpRootPath, $"crash_{timestamp}_{sequence:000}");
				Directory.CreateDirectory(dumpDir);
				WriteDumpContents(dumpDir, reason, message, stackTrace);
				mirroredDumpDir = TryMirrorDumpDirectory(dumpDir);
			}

			if (IsMainThread())
			{
				if (!string.IsNullOrEmpty(mirroredDumpDir))
				{
					Debug.LogWarning($"[CrashReporter] Dump saved to: {dumpDir} (mirrored to {mirroredDumpDir})");
				}
				else
				{
					Debug.LogWarning($"[CrashReporter] Dump saved to: {dumpDir}");
				}
			}
		}
		catch (Exception ex)
		{
			// Last resort — don't let the reporter itself crash the app
			ReportWriteFailure(ex);
		}
	}

	private bool IsMainThread()
	{
		return Thread.CurrentThread.ManagedThreadId == _mainThreadId;
	}

	private void ReportWriteFailure(Exception ex)
	{
		var message = $"[CrashReporter] Failed to write dump: {ex.Message}";
		try
		{
			AppendFailureLog(_dumpRootPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}\n{ex}\n");
		}
		catch
		{
			// Nothing left to do safely.
		}

		if (IsMainThread())
		{
			Debug.LogError(message);
		}
	}

	private void WriteSystemInfo(string path)
	{
		File.WriteAllText(path, _systemInfoSnapshot, Encoding.UTF8);
	}

	private string TryMirrorDumpDirectory(string dumpDir)
	{
		if (string.IsNullOrEmpty(_mirrorDumpRootPath))
		{
			return null;
		}

		try
		{
			Directory.CreateDirectory(_mirrorDumpRootPath);

			var mirroredDumpDir = Path.Combine(_mirrorDumpRootPath, Path.GetFileName(dumpDir));
			CopyDirectory(dumpDir, mirroredDumpDir);
			return mirroredDumpDir;
		}
		catch (Exception ex)
		{
			AppendFailureLog(_dumpRootPath,
				$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [CrashReporter] Failed to mirror dump to '{_mirrorDumpRootPath}': {ex}\n");
			return null;
		}
	}

	private static void CopyDirectory(string sourceDir, string destinationDir)
	{
		Directory.CreateDirectory(destinationDir);

		foreach (var filePath in Directory.GetFiles(sourceDir))
		{
			var destinationFile = Path.Combine(destinationDir, Path.GetFileName(filePath));
			File.Copy(filePath, destinationFile, true);
		}

		foreach (var childDir in Directory.GetDirectories(sourceDir))
		{
			var destinationChildDir = Path.Combine(destinationDir, Path.GetFileName(childDir));
			CopyDirectory(childDir, destinationChildDir);
		}
	}

	private static void AppendFailureLog(string dumpRootPath, string content)
	{
		Directory.CreateDirectory(dumpRootPath);
		File.AppendAllText(
			Path.Combine(dumpRootPath, "crash_reporter_failures.log"),
			content,
			Encoding.UTF8);
	}

	private void WriteDumpContents(string dumpDir, string reason, string message, string stackTrace)
	{
		// --- 1. Recent logs ---
		var logPath = Path.Combine(dumpDir, "recent_logs.txt");
		lock (_lock)
		{
			using var writer = new StreamWriter(logPath, false, Encoding.UTF8);
			foreach (var entry in _logBuffer)
			{
				writer.WriteLine(entry);
			}
		}

		// --- 2. Crash info ---
		var crashInfoPath = Path.Combine(dumpDir, "crash_info.txt");
		using (var writer = new StreamWriter(crashInfoPath, false, Encoding.UTF8))
		{
			writer.WriteLine($"Crash Time   : {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");
			writer.WriteLine($"Reason       : {reason}");
			writer.WriteLine($"Message      : {message}");
			writer.WriteLine($"Company Name : {_companyName}");
			writer.WriteLine($"Product Name : {_productName}");
			writer.WriteLine($"Persistent   : {_persistentDataPath}");
			writer.WriteLine($"Local Dump   : {_dumpRootPath}");
			writer.WriteLine($"Mirror Dump  : {_mirrorDumpRootPath ?? "<none>"}");
			writer.WriteLine();
			writer.WriteLine("=== Stack Trace ===");
			writer.WriteLine(stackTrace);
		}

		// --- 3. System info ---
		var sysInfoPath = Path.Combine(dumpDir, "system_info.txt");
		WriteSystemInfo(sysInfoPath);

		// --- 4. Copy the current session log if available ---
		CopySessionLog(dumpDir);

		// --- 5. Copy Unity CrashReports if available ---
		if (IsMainThread())
		{
			CopyCrashReports(dumpDir);
		}
	}

	private static string BuildLocalDumpRootPath()
	{
		return BuildExecutableDumpRootPath();
	}

	private static string BuildMirrorDumpRootPath(string localDumpRootPath)
	{
		if (string.IsNullOrEmpty(_persistentDataPath))
		{
			return null;
		}

		var dumpRootPath = Path.GetFullPath(_persistentDataPath);
		if (string.Equals(dumpRootPath, localDumpRootPath, StringComparison.Ordinal))
		{
			return null;
		}

		return dumpRootPath;
	}

	private static string BuildExecutableDumpRootPath()
	{
#if UNITY_EDITOR
		return Path.GetFullPath(Path.Combine(Application.dataPath, "..", DumpDirectoryName));
#else
		return Path.Combine(
			Path.GetDirectoryName(Application.dataPath) ?? ".",
			DumpDirectoryName);
#endif
	}

	private static string CaptureSystemInfoSnapshot()
	{
		var builder = new StringBuilder();
		using var writer = new StringWriter(builder);
		try
		{
			var qualityLevel = QualitySettings.GetQualityLevel();
			var qualityNames = QualitySettings.names;
			var qualityName = qualityNames != null && qualityLevel >= 0 && qualityLevel < qualityNames.Length ?
				qualityNames[qualityLevel] : "Unknown";

			writer.WriteLine($"OS              : {SystemInfo.operatingSystem}");
			writer.WriteLine($"Device Model    : {SystemInfo.deviceModel}");
			writer.WriteLine($"Device Type     : {SystemInfo.deviceType}");
			writer.WriteLine($"Processor       : {SystemInfo.processorType}");
			writer.WriteLine($"Processor Count : {SystemInfo.processorCount}");
			writer.WriteLine($"System Memory   : {SystemInfo.systemMemorySize} MB");
			writer.WriteLine($"GPU             : {SystemInfo.graphicsDeviceName}");
			writer.WriteLine($"GPU Vendor      : {SystemInfo.graphicsDeviceVendor}");
			writer.WriteLine($"GPU Memory      : {SystemInfo.graphicsMemorySize} MB");
			writer.WriteLine($"GPU API         : {SystemInfo.graphicsDeviceType}");
			writer.WriteLine($"GPU Version     : {SystemInfo.graphicsDeviceVersion}");
			writer.WriteLine($"Unity Version   : {Application.unityVersion}");
			writer.WriteLine($"App Version     : {Application.version}");
			writer.WriteLine($"Product Name    : {Application.productName}");
			writer.WriteLine($"Platform        : {Application.platform}");
			writer.WriteLine($"Quality Level   : {qualityLevel} ({qualityName})");
			writer.WriteLine($"Target FPS      : {Application.targetFrameRate}");
			writer.WriteLine($"Screen          : {Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio}Hz");
		}
		catch (Exception ex)
		{
			writer.WriteLine($"Failed to capture Unity system info: {ex.Message}");
		}

		return builder.ToString();
	}

	private static string[] BuildSessionLogCandidates()
	{
		var candidates = new List<string>();
		AddLogCandidate(candidates, TryGetConsoleLogPath());

		var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
#if UNITY_EDITOR
		var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
		if (!string.IsNullOrEmpty(appDataPath))
		{
			AddLogCandidate(candidates, Path.Combine(appDataPath, "unity3d", "Editor.log"));
		}

		if (!string.IsNullOrEmpty(xdgConfig))
		{
			AddLogCandidate(candidates, Path.Combine(xdgConfig, "unity3d", "Editor.log"));
		}
#else
		// Unity standalone Linux: ~/.config/unity3d/<Company>/<Product>/Player.log
		var unityLogDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"unity3d",
			Application.companyName,
			Application.productName);
		AddLogCandidate(candidates, Path.Combine(unityLogDir, "Player.log"));

		// Some Unity versions place it next to the executable
		var exeDir = Path.GetDirectoryName(Application.dataPath) ?? ".";
		AddLogCandidate(candidates, Path.Combine(exeDir, "Player.log"));

		// Linux XDG config path
		if (!string.IsNullOrEmpty(xdgConfig))
		{
			var xdgLogDir = Path.Combine(xdgConfig, "unity3d",
				Application.companyName, Application.productName);
			AddLogCandidate(candidates, Path.Combine(xdgLogDir, "Player.log"));
		}
#endif

		return candidates.ToArray();
	}

	private static string TryGetConsoleLogPath()
	{
		try
		{
			var property = typeof(Application).GetProperty("consoleLogPath", BindingFlags.Public | BindingFlags.Static);
			return property?.GetValue(null) as string;
		}
		catch
		{
			return null;
		}
	}

	private static void AddLogCandidate(List<string> candidates, string logPath)
	{
		if (string.IsNullOrWhiteSpace(logPath))
		{
			return;
		}

		var fullPath = Path.GetFullPath(logPath);
		if (candidates.Exists(candidate => string.Equals(candidate, fullPath, StringComparison.OrdinalIgnoreCase)))
		{
			return;
		}

		candidates.Add(fullPath);
	}

	private void CopySessionLog(string destDir)
	{
		foreach (var logFile in _sessionLogCandidates)
		{
			if (!File.Exists(logFile))
			{
				continue;
			}

			try
			{
				var destFile = Path.Combine(destDir, Path.GetFileName(logFile));
				File.Copy(logFile, destFile, overwrite: true);
				return;
			}
			catch
			{
				// The session log may be locked; best-effort copy
			}
		}
	}

	private static void CopyCrashReports(string destDir)
	{
#if !UNITY_EDITOR
		var reports = CrashReport.reports;
		if (reports == null || reports.Length == 0)
		{
			return;
		}

		var reportDir = Path.Combine(destDir, "unity_crash_reports");
		Directory.CreateDirectory(reportDir);

		foreach (var report in reports)
		{
			var reportFile = Path.Combine(reportDir,
				$"crash_{report.time:yyyyMMdd_HHmmss}.txt");

			using var writer = new StreamWriter(reportFile, false, Encoding.UTF8);
			writer.WriteLine($"Time: {report.time}");
			writer.WriteLine($"Text:\n{report.text}");
		}
#endif
	}
}
