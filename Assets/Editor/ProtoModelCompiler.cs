/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Linq;
using System.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Precompiles a static protobuf-net serializer assembly (CloisimProtoModel.dll)
/// for all cloisim.msgs types. This moves the reflection-based model scan
/// (which calls ParameterInfo/PropertyInfo.GetRequiredCustomModifiers, unsupported
/// under IL2CPP) to the Editor's Mono host, so the shipped runtime code never
/// scans reflection metadata at all.
/// </summary>
public static class ProtoModelCompiler
{
	private const string OutputAssemblyName = "CloisimProtoModel";
	private const string OutputPath = "Assets/Plugins/CloisimProtoModel/CloisimProtoModel.dll";

	[MenuItem("CLOiSim/Build/Compile Protobuf Model")]
	public static void Compile()
	{
		var model = RuntimeTypeModel.Create();

		var messageTypes = typeof(cloisim.msgs.WorldStatistics).Assembly
			.GetTypes()
			.Where(t => t.Namespace == "cloisim.msgs" && t.GetCustomAttribute<ProtoContractAttribute>() != null)
			.OrderBy(t => t.FullName)
			.ToList();

		if (messageTypes.Count == 0)
		{
			Debug.LogError("ProtoModelCompiler: no cloisim.msgs [ProtoContract] types found — aborting.");
			return;
		}

		foreach (var type in messageTypes)
		{
			model.Add(type, true);
		}

		var outputDir = System.IO.Path.GetDirectoryName(OutputPath);
		if (!System.IO.Directory.Exists(outputDir))
		{
			System.IO.Directory.CreateDirectory(outputDir);
		}

		// Mono's AssemblyBuilder.DefineDynamicModule (which RuntimeTypeModel.Compile
		// delegates to) rejects a fileName containing path separators. Do NOT work
		// around this by changing the process-wide current directory (Directory.
		// SetCurrentDirectory) — Unity relies on relative paths internally and that
		// previously crashed the running Editor. Instead compile a bare filename
		// into whatever the current directory already is, then move the result.
		var bareFileName = System.IO.Path.GetFileName(OutputPath);
		var producedPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), bareFileName);

		try
		{
			model.Compile(OutputAssemblyName, bareFileName);

			var absoluteOutputPath = System.IO.Path.GetFullPath(OutputPath);
			if (System.IO.File.Exists(absoluteOutputPath))
			{
				System.IO.File.Delete(absoluteOutputPath);
			}
			System.IO.File.Move(producedPath, absoluteOutputPath);

			Debug.Log($"ProtoModelCompiler: compiled {messageTypes.Count} message types into {OutputPath}");
			AssetDatabase.Refresh();
		}
		catch (Exception ex)
		{
			Debug.LogError($"ProtoModelCompiler: Compile() failed: {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}");
		}
		finally
		{
			if (System.IO.File.Exists(producedPath))
			{
				System.IO.File.Delete(producedPath);
			}
		}
	}
}
