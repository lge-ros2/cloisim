/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public partial class MeshLoader
{
	private static Color GetColor(Assimp.Color4D color)
	{
		return (color == null) ? Color.clear : new Color(color.R, color.G, color.B, color.A);
	}

	private static List<string> MaterialSearchPaths = new List<string>()
		{
			"",
			"/textures/",
			"../",
			"../materials/", "../materials/textures/",
			"../../materials/", "../../materials/textures/"
		};

	private static List<string> GetRootTexturePaths(in string parentPath)
	{
		var texturePaths = new List<string>(){};

		foreach (var matPath in MaterialSearchPaths)
		{
			texturePaths.Add(Path.Combine(parentPath, matPath));
		}

		return texturePaths;
	}

	class MeshMaterialSet
	{
		private readonly int _materialIndex;
		private readonly Mesh _mesh;
		private Material _material;

		public MeshMaterialSet(in Mesh mesh, in int materialIndex)
		{
			_mesh = mesh;
			_materialIndex = materialIndex;
		}

		public int MaterialIndex => _materialIndex;

		public Material Material
		{
			get => _material;
			set => _material = value;
		}

		public Mesh Mesh => _mesh;
	}

	class MeshMaterialList
	{
		private List<MeshMaterialSet> meshMatList = new List<MeshMaterialSet>();

		public int Count => meshMatList.Count;

		public void Add(in MeshMaterialSet meshMatSet)
		{
			meshMatList.Add(meshMatSet);
		}

		public void SetMaterials(in List<Material> materials)
		{
			foreach (var meshMatSet in meshMatList)
			{
				meshMatSet.Material = materials[meshMatSet.MaterialIndex];
			}
		}

		public MeshMaterialSet Get(in int index)
		{
			return meshMatList[index];
		}
	}

	private static bool CheckFileSupport(in string fileExtension)
	{
		var isFileSupported = true;

		switch (fileExtension)
		{
			case ".dae":
			case ".obj":
			case ".stl":
			case ".fbx":
				break;

			default:
				isFileSupported = false;
				break;
		}

		return isFileSupported;
	}

	private static Quaternion GetRotationByFileExtension(in string fileExtension, in string meshPath)
	{
		var eulerRotation = Quaternion.identity;

		switch (fileExtension)
		{
			case ".dae":
			case ".obj":
			case ".stl":
				eulerRotation =  Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 0) * Quaternion.Euler(0, 0, 90);
				goto case ".fbx";

			case ".fbx":
				break;
			default:
				break;
		}

		return eulerRotation;
	}

	private static Matrix4x4 ConvertAssimpMatrix4x4ToUnity(in Assimp.Matrix4x4 assimpMatrix)
	{
		assimpMatrix.Decompose(out var scaling, out var rotation, out var translation);
		var pos = new Vector3(translation.X, translation.Y, translation.Z);
		var q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
		var s = new Vector3(scaling.X, scaling.Y, scaling.Z);
		return Matrix4x4.TRS(pos, q, s);
	}

	private static readonly Assimp.AssimpContext importer = new Assimp.AssimpContext();

	private static readonly Assimp.LogStream logstream = new Assimp.LogStream(
		delegate (String msg, String userData)
		{
			Debug.Log(msg);
		});

	private static Assimp.Scene GetScene(in string targetPath, out Quaternion meshRotation)
	{
		meshRotation = Quaternion.identity;

		if (!File.Exists(targetPath))
		{
			Debug.LogError("File doesn't exist: " + targetPath);
			return null;
		}

		var colladaIgnoreConfig = new Assimp.Configs.ColladaIgnoreUpDirectionConfig(true);
		importer.SetConfig(colladaIgnoreConfig);

		// logstream.Attach();

		var fileExtension = Path.GetExtension(targetPath).ToLower();

		if (!CheckFileSupport(fileExtension))
		{
			Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + targetPath);
			return null;
		}

		const Assimp.PostProcessSteps postProcessFlags =
			Assimp.PostProcessSteps.OptimizeGraph |
			Assimp.PostProcessSteps.OptimizeMeshes |
			Assimp.PostProcessSteps.CalculateTangentSpace |
			Assimp.PostProcessSteps.JoinIdenticalVertices |
			Assimp.PostProcessSteps.RemoveRedundantMaterials |
			Assimp.PostProcessSteps.Triangulate |
			Assimp.PostProcessSteps.SortByPrimitiveType |
			Assimp.PostProcessSteps.ValidateDataStructure |
			Assimp.PostProcessSteps.FindInvalidData |
			Assimp.PostProcessSteps.MakeLeftHanded;

		var scene = importer.ImportFile(targetPath, postProcessFlags);
		if (scene == null)
		{
			return null;
		}

		// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
		meshRotation = GetRotationByFileExtension(fileExtension, targetPath);

		return scene;
	}
}