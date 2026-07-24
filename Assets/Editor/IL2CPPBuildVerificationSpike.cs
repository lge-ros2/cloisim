using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Temporary, non-interactive IL2CPP build entry point for verifying the
/// protobuf-net AOT fix (see Assets/Editor/ProtoModelCompiler.cs). Not part of
/// the permanent build tooling — delete after verification.
/// </summary>
public static class IL2CPPBuildVerificationSpike
{
	public static void Run()
	{
		var outputDir = "/tmp/claude-1000/-home-yg-Work-cloisim/bd18c0ca-8cca-4a83-b936-7fce2cad0da8/scratchpad/il2cpp-verify";
		var buildLocation = System.IO.Path.Combine(outputDir, "CLOiSim.x86_64");

		var buildPlayerOptions = new BuildPlayerOptions
		{
			scenes = null, // filled in by BuildLinuxWithStrippedMainScene via EditorBuildSettings
			target = BuildTarget.StandaloneLinux64,
			targetGroup = BuildTargetGroup.Standalone,
			locationPathName = buildLocation,
			options = BuildOptions.None,
		};

		try
		{
			var report = StrippedSceneBuildTools.BuildLinuxWithStrippedMainScene(buildPlayerOptions);
			Debug.Log($"IL2CPP_BUILD_RESULT: summary.result={report.summary.result} " +
				$"totalErrors={report.summary.totalErrors} totalWarnings={report.summary.totalWarnings} " +
				$"outputPath={report.summary.outputPath}");
		}
		catch (Exception ex)
		{
			Debug.LogError($"IL2CPP_BUILD_RESULT: EXCEPTION {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
		}
	}
}
