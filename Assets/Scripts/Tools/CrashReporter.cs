/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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
	private readonly string _dumpRootPath;

	private bool _disposed;

	public CrashReporter()
	{
		// Place crash dumps next to the executable (standalone) or in persistentDataPath
#if UNITY_EDITOR
		_dumpRootPath = Path.Combine(Application.dataPath, "..", DumpDirectoryName);
#else
		_dumpRootPath = Path.Combine(
			Path.GetDirectoryName(Application.dataPath) ?? ".",
			DumpDirectoryName);
#endif

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
			Directory.CreateDirectory(_dumpRootPath);

			var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
			var dumpDir = Path.Combine(_dumpRootPath, $"crash_{timestamp}");
			Directory.CreateDirectory(dumpDir);

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
				writer.WriteLine();
				writer.WriteLine("=== Stack Trace ===");
				writer.WriteLine(stackTrace);
			}

			// --- 3. System info ---
			var sysInfoPath = Path.Combine(dumpDir, "system_info.txt");
			WriteSystemInfo(sysInfoPath);

			// --- 4. Copy Player.log if available ---
			CopyPlayerLog(dumpDir);

			// --- 5. Copy Unity CrashReports if available ---
			CopyCrashReports(dumpDir);

			Debug.LogWarning($"[CrashReporter] Dump saved to: {dumpDir}");
		}
		catch (Exception ex)
		{
			// Last resort — don't let the reporter itself crash the app
			Debug.LogError($"[CrashReporter] Failed to write dump: {ex.Message}");
		}
	}

	private static void WriteSystemInfo(string path)
	{
		using var writer = new StreamWriter(path, false, Encoding.UTF8);
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
		writer.WriteLine($"Quality Level   : {QualitySettings.GetQualityLevel()} ({QualitySettings.names[QualitySettings.GetQualityLevel()]})");
		writer.WriteLine($"Target FPS      : {Application.targetFrameRate}");
		writer.WriteLine($"Screen          : {Screen.width}x{Screen.height} @ {Screen.currentResolution.refreshRateRatio}Hz");
	}

	private static void CopyPlayerLog(string destDir)
	{
		var candidates = new List<string>();

		// Unity standalone Linux: ~/.config/unity3d/<Company>/<Product>/Player.log
		var unityLogDir = Path.Combine(
			Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
			"unity3d",
			Application.companyName,
			Application.productName);
		candidates.Add(Path.Combine(unityLogDir, "Player.log"));
		candidates.Add(Path.Combine(unityLogDir, "Player-prev.log"));

		// Some Unity versions place it next to the executable
		var exeDir = Path.GetDirectoryName(Application.dataPath) ?? ".";
		candidates.Add(Path.Combine(exeDir, "Player.log"));

		// Linux XDG config path
		var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
		if (!string.IsNullOrEmpty(xdgConfig))
		{
			var xdgLogDir = Path.Combine(xdgConfig, "unity3d",
				Application.companyName, Application.productName);
			candidates.Add(Path.Combine(xdgLogDir, "Player.log"));
		}

		foreach (var logFile in candidates)
		{
			if (!File.Exists(logFile))
			{
				continue;
			}

			try
			{
				var destFile = Path.Combine(destDir, Path.GetFileName(logFile));
				// Avoid name collisions
				if (File.Exists(destFile))
				{
					destFile = Path.Combine(destDir,
						Path.GetFileNameWithoutExtension(logFile) + "_alt" + Path.GetExtension(logFile));
				}
				File.Copy(logFile, destFile, overwrite: false);
			}
			catch
			{
				// Player.log may be locked; best-effort copy
			}
		}
	}

	private static void CopyCrashReports(string destDir)
	{
#if !UNITY_EDITOR
		var reports = CrashReport.reports;
		if (reports == null || reports.Count == 0)
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
