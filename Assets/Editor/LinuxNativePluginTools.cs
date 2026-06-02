/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class LinuxNativePluginBuildPreprocessor : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform != BuildTarget.StandaloneLinux64)
		{
			return;
		}

		LinuxNativePluginTools.ConfigureLinuxNativePluginsForBuild();
	}
}

public sealed class LinuxBuildPdbCleanupPostprocessor : IPostprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPostprocessBuild(BuildReport report)
	{
		if (!LinuxNativePluginTools.ShouldCleanupBuildArtifacts(report.summary.platform))
		{
			return;
		}

		LinuxNativePluginTools.DeleteDebugArtifactsFromBuild(report.summary.outputPath);
	}
}

public static class LinuxNativePluginTools
{
	private static readonly BuildTarget[] SupportedCleanupTargets =
	{
		BuildTarget.StandaloneLinux64,
		BuildTarget.StandaloneWindows,
		BuildTarget.StandaloneWindows64,
		BuildTarget.StandaloneOSX,
	};

	private static readonly string[] DebugArtifactPatterns =
	{
		"*.pdb",
		"*_BurstDebugInformation*",
	};

	private static readonly string[] LinuxNativePluginPaths =
	{
		"Assets/Plugins/AssimpNetter.6.0.4/runtimes/linux-x64/native/libassimp.so",
		"Assets/Scripts/Tools/StdHash/lib/linux/libStdHash.so",
	};

	[MenuItem("CLOiSim/Diagnostics/Log Build Link Settings")]
	private static void LogBuildLinkSettings()
	{
		Debug.Log(
			$"Build link settings: symlinkSources={EditorUserBuildSettings.symlinkSources}, " +
			$"installInBuildFolder={EditorUserBuildSettings.installInBuildFolder}, " +
			$"activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}, " +
			$"selectedBuildTargetGroup={EditorUserBuildSettings.selectedBuildTargetGroup}");
	}

	[MenuItem("CLOiSim/Plugins/Configure Linux Native Plugins")]
	private static void ConfigureLinuxNativePlugins()
	{
		ConfigureLinuxNativePluginsForBuild();
	}

	public static void ConfigureLinuxNativePluginsForBuild()
	{
		foreach (var assetPath in LinuxNativePluginPaths)
		{
			ConfigureLinuxNativePlugin(assetPath);
		}

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Configured Linux native plugin import settings.");
	}

	public static bool ShouldCleanupBuildArtifacts(BuildTarget buildTarget)
	{
		foreach (var supportedTarget in SupportedCleanupTargets)
		{
			if (supportedTarget == buildTarget)
			{
				return true;
			}
		}

		return false;
	}

	public static void DeleteDebugArtifactsFromBuild(string buildOutputPath)
	{
		if (string.IsNullOrEmpty(buildOutputPath))
		{
			Debug.LogWarning("Build output path is empty. Skipping debug artifact cleanup.");
			return;
		}

		var buildDirectory = System.IO.Path.GetDirectoryName(buildOutputPath);
		if (string.IsNullOrEmpty(buildDirectory) || !System.IO.Directory.Exists(buildDirectory))
		{
			Debug.LogWarning($"Build output directory not found for '{buildOutputPath}'. Skipping debug artifact cleanup.");
			return;
		}

		var removedFileCount = 0;
		var removedDirectoryCount = 0;
		foreach (var pattern in DebugArtifactPatterns)
		{
			var artifactFiles = System.IO.Directory.GetFiles(buildDirectory, pattern, System.IO.SearchOption.AllDirectories);
			foreach (var artifactFile in artifactFiles)
			{
				System.IO.File.Delete(artifactFile);
				removedFileCount++;
			}

			var artifactDirectories = System.IO.Directory.GetDirectories(buildDirectory, pattern, System.IO.SearchOption.AllDirectories);
			foreach (var artifactDirectory in artifactDirectories)
			{
				System.IO.Directory.Delete(artifactDirectory, true);
				removedDirectoryCount++;
			}
		}

		Debug.Log(
			$"Removed {removedFileCount} debug artifact file(s) and {removedDirectoryCount} directory(s) from '{buildDirectory}'.");
	}

	private static void ConfigureLinuxNativePlugin(string assetPath)
	{
		var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
		if (importer == null)
		{
			Debug.LogWarning($"PluginImporter not found for '{assetPath}'");
			return;
		}

		importer.SetCompatibleWithAnyPlatform(false);
		importer.SetCompatibleWithEditor(false);
		importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, true);
		importer.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", "x86_64");
		importer.SaveAndReimport();
	}
}
