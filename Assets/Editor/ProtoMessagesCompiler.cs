/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ProtoBuf;
using ProtoBuf.Meta;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Precompiles a static protobuf-net serializer assembly
/// (CloisimProtoMessages.dll) for all cloisim.msgs types.
///
/// The cloisim.msgs source files must belong to the CLOiSim.Messages
/// assembly rather than Assembly-CSharp.
/// </summary>
public static class ProtoMessagesCompiler
{
	private const string OutputAssemblyName = "CloisimProtoMessages";

	private const string OutputPath = "Assets/Plugins/CloisimProtoMessages/CloisimProtoMessages.dll";

	private const string ExpectedMessageAssemblyName = "CLOiSim.Messages";

	private static bool isCompiling;

	[MenuItem("CLOiSim/Build/Compile Protobuf Messages for CLOiSimPlugins")]
	public static void Compile()
	{
		if (isCompiling)
		{
			Debug.LogWarning("ProtoMessagesCompiler: compilation is already in progress.");
			return;
		}

		isCompiling = true;

		try
		{
			CompileInternal();
		}
		finally
		{
			isCompiling = false;
		}
	}

	private static void CompileInternal()
	{
		var sourceAssembly =
			typeof(cloisim.msgs.WorldStatistics).Assembly;

		var sourceAssemblyName =
			sourceAssembly.GetName().Name;

		if (string.Equals(
				sourceAssemblyName,
				"Assembly-CSharp",
				StringComparison.Ordinal))
		{
			Debug.LogError(
				"ProtoMessagesCompiler: cloisim.msgs types are compiled " +
				"into Assembly-CSharp.\n\n" +
				"CloisimProtoMessages.dll must not reference " +
				"Assembly-CSharp because Unity loads precompiled plugin " +
				"assemblies before Assembly-CSharp.\n\n" +
				"Place CLOiSim.Messages.asmdef in the common parent " +
				"directory containing the cloisim.msgs source files.\n\n" +
				$"Current message assembly: {sourceAssemblyName}");

			return;
		}

		if (!string.Equals(
				sourceAssemblyName,
				ExpectedMessageAssemblyName,
				StringComparison.Ordinal))
		{
			Debug.LogError(
				"ProtoMessagesCompiler: unexpected protobuf message " +
				$"assembly '{sourceAssemblyName}'.\n" +
				$"Expected '{ExpectedMessageAssemblyName}'.");

			return;
		}

		List<Type> messageTypes;

		try
		{
			messageTypes = sourceAssembly
				.GetTypes()
				.Where(IsProtoMessageType)
				.OrderBy(
					type => type.FullName,
					StringComparer.Ordinal)
				.ToList();
		}
		catch (ReflectionTypeLoadException ex)
		{
			var loaderErrors = ex.LoaderExceptions == null
				? string.Empty
				: string.Join(
					"\n",
					ex.LoaderExceptions
						.Where(error => error != null)
						.Select(error =>
							$"- {error.GetType().FullName}: " +
							error.Message));

			Debug.LogError(
				"ProtoMessagesCompiler: failed to enumerate protobuf " +
				$"message types from '{sourceAssemblyName}'.\n" +
				loaderErrors);

			return;
		}
		catch (Exception ex)
		{
			Debug.LogError(
				"ProtoMessagesCompiler: failed to enumerate protobuf " +
				$"message types from '{sourceAssemblyName}'.\n" +
				$"{ex.GetType().FullName}: {ex.Message}\n" +
				ex.StackTrace);

			return;
		}

		if (messageTypes.Count == 0)
		{
			Debug.LogError(
				"ProtoMessagesCompiler: no cloisim.msgs types marked " +
				"with [ProtoContract] were found.");

			return;
		}

		var projectRoot =
			Directory.GetCurrentDirectory();

		var absoluteOutputPath =
			Path.GetFullPath(OutputPath);

		var outputDirectory =
			Path.GetDirectoryName(absoluteOutputPath);

		if (string.IsNullOrEmpty(outputDirectory))
		{
			Debug.LogError(
				$"ProtoMessagesCompiler: invalid output path: " +
				OutputPath);

			return;
		}

		Directory.CreateDirectory(outputDirectory);

		/*
		 * RuntimeTypeModel.Compile() uses the output filename when
		 * creating the generated assembly identity.
		 *
		 * The filename must therefore remain exactly:
		 *
		 *     CloisimProtoMessages.dll
		 *
		 * A filename containing a GUID or ".tmp" would change the
		 * generated assembly name.
		 */
		var temporaryFileName =
			$"{OutputAssemblyName}.dll";

		var producedPath =
			Path.Combine(projectRoot, temporaryFileName);

		var stagedPath =
			absoluteOutputPath + ".new";

		var backupPath =
			absoluteOutputPath + ".backup";

		try
		{
			DeleteIfExists(producedPath);
			DeleteIfExists(stagedPath);
			DeleteIfExists(backupPath);

			var model = RuntimeTypeModel.Create();

			foreach (var messageType in messageTypes)
			{
				model.Add(messageType, true);
			}

			model.Compile(
				OutputAssemblyName,
				temporaryFileName);

			ValidateGeneratedAssembly(
				producedPath,
				sourceAssemblyName);

			File.Copy(
				producedPath,
				stagedPath,
				true);

			if (File.Exists(absoluteOutputPath))
			{
				File.Move(
					absoluteOutputPath,
					backupPath);
			}

			try
			{
				File.Move(
					stagedPath,
					absoluteOutputPath);
			}
			catch
			{
				DeleteIfExists(absoluteOutputPath);

				if (File.Exists(backupPath))
				{
					File.Move(
						backupPath,
						absoluteOutputPath);
				}

				throw;
			}

			DeleteIfExists(backupPath);

			AssetDatabase.ImportAsset(
				OutputPath,
				ImportAssetOptions.ForceSynchronousImport |
				ImportAssetOptions.ForceUpdate);

			var pluginImporter = AssetImporter.GetAtPath(OutputPath) as PluginImporter;

			if (pluginImporter == null)
			{
				throw new InvalidOperationException(
					$"ProtoMessagesCompiler: failed to get PluginImporter for '{OutputPath}'.");
			}

			var importerChanged = false;

			if (!pluginImporter.GetCompatibleWithAnyPlatform())
			{
				pluginImporter.SetCompatibleWithAnyPlatform(true);
				importerChanged = true;
			}

			if (!pluginImporter.GetCompatibleWithEditor())
			{
				pluginImporter.SetCompatibleWithEditor(true);
				importerChanged = true;
			}

			if (importerChanged)
			{
				pluginImporter.SaveAndReimport();
			}

			Debug.Log(
				"ProtoMessagesCompiler: compiled " +
				$"{messageTypes.Count} message types from " +
				$"'{sourceAssemblyName}' into '{OutputPath}'.");
		}
		catch (Exception ex)
		{
			DeleteIfExists(stagedPath);

			if (File.Exists(backupPath))
			{
				DeleteIfExists(absoluteOutputPath);

				File.Move(
					backupPath,
					absoluteOutputPath);
			}

			Debug.LogError(
				"ProtoMessagesCompiler: compilation failed. " +
				"The previous serializer DLL was preserved when " +
				"possible.\n" +
				$"{ex.GetType().FullName}: {ex.Message}\n" +
				ex.StackTrace);
		}
		finally
		{
			DeleteIfExists(producedPath);
			DeleteIfExists(stagedPath);
			DeleteIfExists(backupPath);
		}
	}

	private static bool IsProtoMessageType(Type type)
	{
		return type != null &&
			string.Equals(
				type.Namespace,
				"cloisim.msgs",
				StringComparison.Ordinal) &&
			type.GetCustomAttribute<ProtoContractAttribute>() != null;
	}

	private static void ValidateGeneratedAssembly(
		string assemblyPath,
		string messageAssemblyName)
	{
		if (!File.Exists(assemblyPath))
		{
			throw new FileNotFoundException(
				"protobuf-net did not produce the expected assembly.",
				assemblyPath);
		}

		var fileInfo =
			new FileInfo(assemblyPath);

		if (fileInfo.Length == 0)
		{
			throw new InvalidDataException(
				$"Generated serializer assembly is empty: " +
				assemblyPath);
		}

		var generatedAssemblyName =
			AssemblyName.GetAssemblyName(assemblyPath);

		if (!string.Equals(
				generatedAssemblyName.Name,
				OutputAssemblyName,
				StringComparison.Ordinal))
		{
			throw new InvalidDataException(
				"Generated assembly name mismatch. " +
				$"Expected '{OutputAssemblyName}', " +
				$"got '{generatedAssemblyName.Name}'.");
		}

		if (string.Equals(
				messageAssemblyName,
				"Assembly-CSharp",
				StringComparison.Ordinal))
		{
			throw new InvalidOperationException(
				"Generated serializer DLL must not reference " +
				"Assembly-CSharp.");
		}
	}

	private static void DeleteIfExists(string path)
	{
		if (!string.IsNullOrEmpty(path) &&
			File.Exists(path))
		{
			File.Delete(path);
		}
	}
}