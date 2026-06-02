/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class StrippedSceneBuildTools
{
	private const string MainScenePath = "Assets/Scenes/MainScene.unity";
	private const string WorldRootName = "World";
	private const string MainSceneBuildTempFileName = "MainScene.build-temp.unity";
	private const string MainSceneBuildTempDirectory = "Assets/Scenes/__BuildTemp";
	private const string ReleaseRunScriptPath = "Release/run.sh";
	private const string ReleaseRunBatchPath = "Release/run.bat";
	private const string LinuxExecutableExtension = ".x86_64";
	private const string WindowsExecutableExtension = ".exe";

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

	private static string MainSceneBuildTempPath =>
		System.IO.Path.Combine(MainSceneBuildTempDirectory, MainSceneBuildTempFileName).Replace('\\', '/');

	[MenuItem("CLOiSim/Diagnostics/Log Build Link Settings")]
	private static void LogBuildLinkSettings()
	{
		Debug.Log(
			$"Build link settings: symlinkSources={EditorUserBuildSettings.symlinkSources}, " +
			$"installInBuildFolder={EditorUserBuildSettings.installInBuildFolder}, " +
			$"activeBuildTarget={EditorUserBuildSettings.activeBuildTarget}, " +
			$"selectedBuildTargetGroup={EditorUserBuildSettings.selectedBuildTargetGroup}");
	}

	[MenuItem("CLOiSim/Build/Build Linux With Stripped MainScene")]
	private static void BuildLinuxWithStrippedMainScene()
	{
		var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
		if (enabledScenes.Length == 0)
		{
			Debug.LogError("No enabled scenes found in EditorBuildSettings.");
			return;
		}

		var defaultLocation = EditorUtility.SaveFolderPanel("Select Linux Build Output Folder", "Builds/Linux", string.Empty);
		if (string.IsNullOrEmpty(defaultLocation))
		{
			return;
		}

		var executableName = System.IO.Path.GetFileNameWithoutExtension(Application.productName) + LinuxExecutableExtension;
		var buildLocation = System.IO.Path.Combine(defaultLocation, executableName);

		var buildPlayerOptions = new BuildPlayerOptions
		{
			scenes = enabledScenes,
			target = BuildTarget.StandaloneLinux64,
			targetGroup = BuildTargetGroup.Standalone,
			locationPathName = buildLocation,
			options = BuildOptions.None,
		};

		BuildLinuxWithStrippedMainScene(buildPlayerOptions);
	}

	[MenuItem("CLOiSim/Build/Build Windows With Stripped MainScene")]
	private static void BuildWindowsWithStrippedMainScene()
	{
		var enabledScenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
		if (enabledScenes.Length == 0)
		{
			Debug.LogError("No enabled scenes found in EditorBuildSettings.");
			return;
		}

		var defaultLocation = EditorUtility.SaveFolderPanel("Select Windows Build Output Folder", "Builds/Windows", string.Empty);
		if (string.IsNullOrEmpty(defaultLocation))
		{
			return;
		}

		var executableName = System.IO.Path.GetFileNameWithoutExtension(Application.productName) + WindowsExecutableExtension;
		var buildLocation = System.IO.Path.Combine(defaultLocation, executableName);

		var buildPlayerOptions = new BuildPlayerOptions
		{
			scenes = enabledScenes,
			target = BuildTarget.StandaloneWindows64,
			targetGroup = BuildTargetGroup.Standalone,
			locationPathName = buildLocation,
			options = BuildOptions.None,
		};

		BuildWithStrippedMainScene(buildPlayerOptions);
	}

	public static BuildReport BuildLinuxWithStrippedMainScene(BuildPlayerOptions buildPlayerOptions)
	{
		return BuildWithStrippedMainScene(buildPlayerOptions);
	}

	private static BuildReport BuildWithStrippedMainScene(BuildPlayerOptions buildPlayerOptions)
	{
		var activeScene = EditorSceneManager.GetActiveScene();
		var activeScenePath = activeScene.path;

		PrepareMainSceneBuildTemp();
		buildPlayerOptions.scenes = GetBuildScenesWithStrippedMainScene();

		try
		{
			var buildReport = BuildPipeline.BuildPlayer(buildPlayerOptions);
			CopyReleaseLauncher(buildPlayerOptions.locationPathName, buildPlayerOptions.target, buildReport);
			ShowBuildResultDialog(buildPlayerOptions.locationPathName, buildReport);
			return buildReport;
		}
		finally
		{
			CleanupMainSceneBuildTemp();
			RestorePreviouslyActiveScene(activeScenePath);
		}
	}

	public static void PrepareMainSceneBuildTemp()
	{
		if (!System.IO.File.Exists(MainScenePath))
		{
			Debug.LogWarning($"Main scene not found at '{MainScenePath}'. Skipping scene strip.");
			return;
		}

		var activeScene = EditorSceneManager.GetActiveScene();
		var activeScenePath = activeScene.path;
		System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(MainSceneBuildTempPath));
		var mainScene = EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
		EditorSceneManager.SaveScene(mainScene, MainSceneBuildTempPath);
		var buildScene = EditorSceneManager.OpenScene(MainSceneBuildTempPath, OpenSceneMode.Single);
		var worldRoot = buildScene.GetRootGameObjects().FirstOrDefault(root => root.name == WorldRootName);
		if (worldRoot == null)
		{
			Debug.LogWarning($"'{WorldRootName}' root not found in '{MainScenePath}'. Skipping scene strip.");
			return;
		}

		for (var index = worldRoot.transform.childCount - 1; index >= 0; index--)
		{
			Object.DestroyImmediate(worldRoot.transform.GetChild(index).gameObject);
		}

		EditorSceneManager.SaveScene(buildScene, MainSceneBuildTempPath);
		RestorePreviouslyActiveScene(activeScenePath);
		AssetDatabase.Refresh();
		Debug.Log("Prepared MainScene for build by stripping World children.");
	}

	public static void CleanupMainSceneBuildTemp()
	{
		if (!System.IO.File.Exists(MainSceneBuildTempPath))
		{
			return;
		}

		EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
		AssetDatabase.DeleteAsset(MainSceneBuildTempPath);
		if (AssetDatabase.IsValidFolder(MainSceneBuildTempDirectory) &&
			AssetDatabase.FindAssets(string.Empty, new[] { MainSceneBuildTempDirectory }).Length == 0)
		{
			AssetDatabase.DeleteAsset(MainSceneBuildTempDirectory);
		}
		Debug.Log("Cleaned up stripped MainScene build temp.");
	}

	private static string[] GetBuildScenesWithStrippedMainScene()
	{
		return EditorBuildSettings.scenes
			.Where(scene => scene.enabled)
			.Select(scene => scene.path == MainScenePath ? MainSceneBuildTempPath : scene.path)
			.ToArray();
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

	private static void CopyReleaseLauncher(string buildLocationPath, BuildTarget buildTarget, BuildReport buildReport)
	{
		if (buildReport == null || buildReport.summary.result != BuildResult.Succeeded)
		{
			return;
		}

		var sourcePath = GetReleaseLauncherSourcePath(buildTarget);
		if (string.IsNullOrEmpty(sourcePath))
		{
			return;
		}

		if (!System.IO.File.Exists(sourcePath))
		{
			Debug.LogWarning($"Release launcher not found at '{sourcePath}'. Skipping copy.");
			return;
		}

		var buildDirectory = System.IO.Path.GetDirectoryName(buildLocationPath);
		if (string.IsNullOrEmpty(buildDirectory) || !System.IO.Directory.Exists(buildDirectory))
		{
			Debug.LogWarning($"Build output directory not found for '{buildLocationPath}'. Skipping run.sh copy.");
			return;
		}

		var destinationPath = System.IO.Path.Combine(buildDirectory, System.IO.Path.GetFileName(sourcePath));
		System.IO.File.Copy(sourcePath, destinationPath, true);
		Debug.Log($"Copied '{sourcePath}' to '{destinationPath}'.");
	}

	private static void ShowBuildResultDialog(string buildLocationPath, BuildReport buildReport)
	{
		if (buildReport == null)
		{
			EditorUtility.DisplayDialog("Build Finished", "Build completed, but no build report was returned.", "OK");
			return;
		}

		var result = buildReport.summary.result;
		var title = result == BuildResult.Succeeded ? "Build Succeeded" : "Build Finished";
		var message = $"Result: {result}\nOutput: {buildLocationPath}";
		EditorUtility.DisplayDialog(title, message, "OK");
	}

	private static string GetReleaseLauncherSourcePath(BuildTarget buildTarget)
	{
		switch (buildTarget)
		{
			case BuildTarget.StandaloneLinux64:
				return ReleaseRunScriptPath;
			case BuildTarget.StandaloneWindows:
			case BuildTarget.StandaloneWindows64:
				return ReleaseRunBatchPath;
			default:
				return null;
		}
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

		EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
		EditorSceneManager.OpenScene(activeScenePath, OpenSceneMode.Single);
	}
}
