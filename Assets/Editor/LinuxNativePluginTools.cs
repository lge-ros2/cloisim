/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class LinuxNativePluginBuildPreprocessor : IPreprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPreprocessBuild(BuildReport report)
	{
		if (report.summary.platform != BuildTarget.StandaloneLinux64)
		{
			return;
		}

		LinuxNativePluginImportTools.ConfigureLinuxNativePluginsForBuild();
	}
}

public sealed class LinuxBuildPdbCleanupPostprocessor : IPostprocessBuildWithReport
{
	public int callbackOrder => 0;

	public void OnPostprocessBuild(BuildReport report)
	{
		if (!StrippedSceneBuildTools.ShouldCleanupBuildArtifacts(report.summary.platform))
		{
			return;
		}

		StrippedSceneBuildTools.DeleteDebugArtifactsFromBuild(report.summary.outputPath);
	}
}
