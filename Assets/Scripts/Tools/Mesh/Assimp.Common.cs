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
	private static Assimp.PostProcessSteps PostProcessFlags =
		// PreTransformVertices
		// LimitBoneWeights
		// FindInstances
		// FindDegenerates
		// FlipUVs
		// FlipWindingOrder
		// SplitByBoneCount
		// Debone
		// GlobalScale
		// Assimp.PostProcessSteps.OptimizeGraph | // --> occurs sub-mesh merged
		// Assimp.PostProcessSteps.GenerateSmoothNormals | // --> it may causes conflict with GenerateNormals
		// Assimp.PostProcessSteps.OptimizeMeshes | // -> it may causes face reverting
		// Assimp.PostProcessSteps.FixInFacingNormals | // -> it may causes wrong face
		Assimp.PostProcessSteps.GenerateNormals |
		Assimp.PostProcessSteps.GenerateUVCoords |
		Assimp.PostProcessSteps.RemoveComponent |
		Assimp.PostProcessSteps.ImproveCacheLocality |
		Assimp.PostProcessSteps.CalculateTangentSpace |
		Assimp.PostProcessSteps.JoinIdenticalVertices |
		Assimp.PostProcessSteps.RemoveRedundantMaterials |
		Assimp.PostProcessSteps.Triangulate |
		Assimp.PostProcessSteps.SortByPrimitiveType |
		Assimp.PostProcessSteps.ValidateDataStructure |
		Assimp.PostProcessSteps.SplitLargeMeshes |
		Assimp.PostProcessSteps.FindInvalidData |
		Assimp.PostProcessSteps.MakeLeftHanded;


	private static Color GetColor(Assimp.Color4D color)
	{
		return (color == null) ? Color.clear : new Color(color.R, color.G, color.B, color.A);
	}

	private static List<string> MaterialSearchPaths = new List<string>()
		{
			"",
			"/textures/",
			"../materials/", "../materials/textures/",
			"../../materials/", "../../materials/textures/",
			"../", "../../"
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

	struct MeshMaterial
	{
		public bool valid;
		public readonly int materialIndex;
		public readonly Mesh mesh;
		public Material material;

		public MeshMaterial(in Mesh mesh, in int materialIndex)
		{
			this.valid = true;
			this.materialIndex = materialIndex;
			this.mesh = mesh;
			this.material = null;
		}
	}

	class MeshMaterialList : List<MeshMaterial>
	{
		public void SetMaterials(in List<Material> materials)
		{
			// foreach (var meshMat in meshMatList)
			for (var i = 0; i < this.Count; i++)
			{
				var meshMat = this[i];
				meshMat.material = materials[meshMat.materialIndex];
				this[i] = meshMat;
			}
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
			case ".obj":
			case ".stl":
				eulerRotation = Quaternion.Euler(90, 0, 0) * Quaternion.Euler(0, 0, 90);
				break;

			case ".dae":
				eulerRotation = Quaternion.Euler(0, -90, 0);
				break;

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

	private static Assimp.Scene GetScene(in string targetPath, in string subMesh = null)
	{
		if (!File.Exists(targetPath))
		{
			Debug.LogWarning("File doesn't exist: " + targetPath);
			return null;
		}

		// logstream.Attach();

		var fileExtension = Path.GetExtension(targetPath).ToLower();

		if (!CheckFileSupport(fileExtension))
		{
			Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + targetPath);
			return null;
		}

		try {
			var scene = importer.ImportFile(targetPath, PostProcessFlags);

			// Remove cameras and lights
			scene.Cameras.Clear();
			scene.Lights.Clear();

			var rootNode = scene.RootNode;
			if (!string.IsNullOrEmpty(subMesh))
			{
				// Debug.Log(subMesh);
				var foundSubMesh = rootNode.FindNode(subMesh);
				rootNode.Children.Clear();
				if (foundSubMesh != null)
				{
					// Debug.Log($"submesh({subMesh}) exist");
					rootNode.Children.Add(foundSubMesh);
				}
			}

			// var metaData = scene.Metadata;
			// foreach (var metaDataSet in metaData)
			// {
			// 	var metaDataKey = metaDataSet.Key;
			// 	var metaDataValue = metaDataSet.Value;
			// 	Debug.Log($"{metaDataKey} : {metaDataValue}");
			// }
			// Debug.Log(rootNode.Transform);

			// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
			var meshRotation = GetRotationByFileExtension(fileExtension, targetPath);

			var rootNodeMatrix = ConvertAssimpMatrix4x4ToUnity(rootNode.Transform);
			rootNodeMatrix = Matrix4x4.Rotate(meshRotation) * rootNodeMatrix;

			rootNode.Transform = new Assimp.Matrix4x4(
				rootNodeMatrix.m00, rootNodeMatrix.m01, rootNodeMatrix.m02, rootNodeMatrix.m03,
				rootNodeMatrix.m10,	rootNodeMatrix.m11, rootNodeMatrix.m12, rootNodeMatrix.m13,
				rootNodeMatrix.m20, rootNodeMatrix.m21, rootNodeMatrix.m22, rootNodeMatrix.m23,
				rootNodeMatrix.m30, rootNodeMatrix.m31, rootNodeMatrix.m32, rootNodeMatrix.m33
			);

			return scene;
		}
		catch (Assimp.AssimpException e)
		{
			Debug.LogError(e.Message);
		}
		return null;
	}
}