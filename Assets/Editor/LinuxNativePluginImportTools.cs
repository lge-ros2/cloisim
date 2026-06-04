/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEditor;
using UnityEngine;

public static class LinuxNativePluginImportTools
{
	private static readonly string[] LinuxNativePluginPaths =
	{
		"Assets/Plugins/AssimpNetter.6.0.4/runtimes/linux-x64/native/libassimp.so",
		"Assets/Scripts/Tools/StdHash/lib/linux/libStdHash.so",
	};

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
