/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class LinuxNativePluginImportTools
{
	private static readonly string StdHashPluginPath =
		"Assets/Scripts/Tools/StdHash/lib/linux/libStdHash.so";

	private const string LinuxAssimpSuffix = "/runtimes/linux-x64/native/libassimp.so";

	public static void ConfigureLinuxNativePluginsForBuild()
	{
		ValidateStdHashPluginImporter();
		ConfigureLinuxNativePlugin(FindAssimpPluginAssetPath());

		AssetDatabase.SaveAssets();
		AssetDatabase.Refresh();
		Debug.Log("Configured Linux native plugin import settings.");
	}

	private static void ValidateStdHashPluginImporter()
	{
		var importer = AssetImporter.GetAtPath(StdHashPluginPath) as PluginImporter;
		if (importer == null)
		{
			throw new BuildFailedException($"PluginImporter not found for '{StdHashPluginPath}'");
		}

		if (IsLinuxNativePluginConfigured(importer))
		{
			return;
		}

		throw new BuildFailedException(
			$"'{StdHashPluginPath}' importer settings are invalid for Linux build. " +
			"Fix the plugin importer in the Inspector instead of relying on auto-rewrite.");
	}

	private static string FindAssimpPluginAssetPath()
	{
		var matchedAssetPaths = new System.Collections.Generic.List<string>();
		CollectAssimpPluginAssets("Assets/Plugins", matchedAssetPaths);

		if (matchedAssetPaths.Count == 1)
		{
			return matchedAssetPaths[0];
		}

		if (matchedAssetPaths.Count == 0)
		{
			throw new BuildFailedException("Linux libassimp.so plugin asset was not found under Assets/Plugins.");
		}

		Debug.LogWarning(
			"Multiple Linux libassimp.so plugin assets were found:\n" +
			string.Join("\n", matchedAssetPaths));
		throw new BuildFailedException("Multiple Linux libassimp.so plugin assets were found. Resolve the duplicate plugins first.");
	}

	private static void CollectAssimpPluginAssets(string rootPath, System.Collections.Generic.List<string> matchedAssetPaths)
	{
		foreach (var assetPath in AssetDatabase.GetAllAssetPaths())
		{
			if (!assetPath.StartsWith(rootPath + "/", System.StringComparison.Ordinal))
			{
				continue;
			}

			if (!assetPath.EndsWith(LinuxAssimpSuffix, System.StringComparison.Ordinal))
			{
				continue;
			}

			matchedAssetPaths.Add(assetPath);
		}
	}

	private static void ConfigureLinuxNativePlugin(string assetPath)
	{
		if (string.IsNullOrEmpty(assetPath))
		{
			return;
		}

		var importer = AssetImporter.GetAtPath(assetPath) as PluginImporter;
		if (importer == null)
		{
			Debug.LogWarning($"PluginImporter not found for '{assetPath}'");
			return;
		}

		if (IsLinuxNativePluginConfigured(importer))
		{
			Debug.Log($"Linux native plugin importer already configured for '{assetPath}'.");
			return;
		}

		importer.SetCompatibleWithAnyPlatform(false);
		importer.SetCompatibleWithEditor(false);
		importer.SetCompatibleWithPlatform(BuildTarget.StandaloneLinux64, true);
		importer.SetPlatformData(BuildTarget.StandaloneLinux64, "CPU", "x86_64");
		importer.SaveAndReimport();
	}

	private static bool IsLinuxNativePluginConfigured(PluginImporter importer)
	{
		return importer.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64) &&
			importer.GetPlatformData(BuildTarget.StandaloneLinux64, "CPU") == "x86_64";
	}
}
