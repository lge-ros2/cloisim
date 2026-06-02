/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

public sealed class LinuxNativePluginBuildPreprocessor : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform != BuildTarget.StandaloneLinux64)
		{
			return;
		}

		LinuxNativePluginTools.PrepareMainSceneForBuild();
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

		LinuxNativePluginTools.RestoreMainSceneAfterBuild();
		LinuxNativePluginTools.DeleteDebugArtifactsFromBuild(report.summary.outputPath);
	}
}

public static class LinuxNativePluginTools
{
	private const string MainScenePath = "Assets/Scenes/MainScene.unity";
	private const string WorldRootName = "World";
	private const string MainSceneBackupFileName = "MainScene.unity.build-backup";

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

	private static string MainSceneBackupPath =>
		System.IO.Path.Combine(System.IO.Path.GetTempPath(), "CLOiSim", MainSceneBackupFileName);

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

	public static void PrepareMainSceneForBuild()
	{
		if (!System.IO.File.Exists(MainScenePath))
		{
			Debug.LogWarning($"Main scene not found at '{MainScenePath}'. Skipping scene strip.");
			return;
		}

		System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MainSceneBackupPath));
		System.IO.File.Copy(MainScenePath, MainSceneBackupPath, true);

		var activeScene = EditorSceneManager.GetActiveScene();
		var activeScenePath = activeScene.path;
		var mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
		var worldRoot = mainScene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRootName);
		if (worldRoot == null)
		{
			Debug.LogWarning($"'{WorldRootName}' root not found in '{MainScenePath}'. Skipping scene strip.");
			RestorePreviouslyActiveScene(activeScenePath);
			return;
		}

		for (var index = worldRoot.transform.childCount - 1; index >= 0; index--)
		{
			Object.DestroyImmediate(worldRoot.transform.GetChild(index).gameObject);
		}

		EditorSceneManager.SaveScene(mainScene);
		RestorePreviouslyActiveScene(activeScenePath);
		AssetDatabase.Refresh();
		Debug.Log("Prepared MainScene for build by stripping World children.");
	}

	public static void RestoreMainSceneAfterBuild()
	{
		if (!System.IO.File.Exists(MainSceneBackupPath))
		{
			return;
		}

		var activeScene = EditorSceneManager.GetActiveScene();
		var activeScenePath = activeScene.path;
		System.IO.File.Copy(MainSceneBackupPath, MainScenePath, true);
		System.IO.File.Delete(MainSceneBackupPath);
		AssetDatabase.Refresh();
		RestorePreviouslyActiveScene(activeScenePath);
		Debug.Log("Restored MainScene after build.");
	}

	[MenuItem("CLOiSim/Build/Cleanup MainScene Backup Metadata")]
	private static void CleanupMainSceneBackupMetadata()
	{
		const string staleBackupAssetPath = MainScenePath + ".build-backup";
		const string staleBackupMetaPath = staleBackupAssetPath + ".meta";

		if (System.IO.File.Exists(staleBackupMetaPath))
		{
			System.IO.File.Delete(staleBackupMetaPath);
		}

		AssetDatabase.Refresh();
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

	private static void RestorePreviouslyActiveScene(string activeScenePath)
	{
		if (string.IsNullOrEmpty(activeScenePath) || activeScenePath == MainScenePath)
		{
			return;
		}

		if (!System.IO.File.Exists(activeScenePath))
		{
			return;
		}

		EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
	}
}
