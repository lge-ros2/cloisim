/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using SN = System.Numerics;

public static partial class MeshLoader
{
	private static readonly Assimp.AssimpContext importer = new Assimp.AssimpContext();

	private static readonly Assimp.LogStream logstream = new Assimp.LogStream(
		delegate (String msg, String userData)
		{
			Debug.Log(msg);
		});

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
		Assimp.PostProcessSteps.RemoveComponent |
		Assimp.PostProcessSteps.ImproveCacheLocality |
		Assimp.PostProcessSteps.RemoveRedundantMaterials |
		Assimp.PostProcessSteps.ValidateDataStructure |
		Assimp.PostProcessSteps.SplitLargeMeshes |
		Assimp.PostProcessSteps.FindInvalidData |
		Assimp.PostProcessSteps.MakeLeftHanded |
		// Assimp.PostProcessSteps.CalculateTangentSpace | => defined in Preset TargetRealTimeFast
		// Assimp.PostProcessSteps.GenerateNormals | => defined in Preset TargetRealTimeFast
		// Assimp.PostProcessSteps.JoinIdenticalVertices | => defined in Preset TargetRealTimeFast
		// Assimp.PostProcessSteps.Triangulate | => defined in Preset TargetRealTimeFast
		// Assimp.PostProcessSteps.GenerateUVCoords | => defined in Preset TargetRealTimeFast
		// Assimp.PostProcessSteps.SortByPrimitiveType | => defined in Preset TargetRealTimeFast
		Assimp.PostProcessPreset.TargetRealTimeFast;

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

	private static Color ToUnity(this SN.Vector4 color)
	{
		return (color == null) ? Color.clear : new Color(color.X, color.Y, color.Z, color.W);
	}

	private static Matrix4x4 ToUnity(this SN.Matrix4x4 assimpMatrix)
	{
		Assimp.Unmanaged.AssimpLibrary.Instance.DecomposeMatrix(ref assimpMatrix, out var scaling, out var rotation, out var translation);
		var pos = new Vector3(translation.X, translation.Y, translation.Z);
		var rot = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
		var scale = new Vector3(scaling.X, scaling.Y, scaling.Z);

#region  Temporay CODE until Assimp Library is fixed.
		// Debug.Log($"rotation = {rot.eulerAngles}");
		// Debug.Log($"scaling  = {scaling.X} {scaling.Y} {scaling.Z}");

		const float precision = 1000f;
		var isRotZeroX = Mathf.Approximately((int)(rot.eulerAngles.x * precision), 0);
		var isRotZeroY = Mathf.Approximately((int)(rot.eulerAngles.y * precision), 0);
		var isRotZeroZ = Mathf.Approximately((int)(rot.eulerAngles.z * precision), 0);

		if (isRotZeroX && !isRotZeroY && !isRotZeroZ)
		{
			var newScale = new Vector3(scale.y, scale.z, scale.x);
			scale = newScale;
		}
		else if (isRotZeroX && !isRotZeroY &&  isRotZeroZ &&
				!Mathf.Approximately(rot.eulerAngles.y, 180f))
		{
			var newScale = new Vector3(scale.z, scale.y, scale.x);
			scale = newScale;
		}
		else if (isRotZeroX && isRotZeroY && !isRotZeroZ &&
				Mathf.Approximately(rot.eulerAngles.z, 90f))
		{
			var newScale = new Vector3(scale.y, scale.x, scale.z);
			scale = newScale;
		}
		else if	(!isRotZeroX && isRotZeroY && isRotZeroZ)
		{
			var newScale = new Vector3(scale.x, scale.z, scale.y);
			scale = newScale;
		}
		else if	(!isRotZeroX && !isRotZeroY && isRotZeroZ)
		{
			var newScale = new Vector3(scale.z, scale.x, scale.y);
			scale = newScale;
		}
		// Debug.Log($"new isRotZero={isRotZeroX}/{isRotZeroY}/{isRotZeroZ} scaling={scale.x} {scale.y} {scale.z} rot={rot.eulerAngles}");
#endregion

		return Matrix4x4.TRS(pos, rot, scale);
	}

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

		Assimp.Scene scene = null;
		try {
			scene = importer.ImportFile(targetPath, PostProcessFlags);

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
			// Debug.Log("rootNode.Transform=" + rootNode.Transform);

			// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
			var meshRotation = GetRotationByFileExtension(fileExtension, targetPath);

			var rootNodeMatrix = rootNode.Transform.ToUnity();
			rootNodeMatrix = Matrix4x4.Rotate(meshRotation) * rootNodeMatrix;

			rootNode.Transform = new SN.Matrix4x4(
				rootNodeMatrix.m00, rootNodeMatrix.m01, rootNodeMatrix.m02, rootNodeMatrix.m03,
				rootNodeMatrix.m10,	rootNodeMatrix.m11, rootNodeMatrix.m12, rootNodeMatrix.m13,
				rootNodeMatrix.m20, rootNodeMatrix.m21, rootNodeMatrix.m22, rootNodeMatrix.m23,
				rootNodeMatrix.m30, rootNodeMatrix.m31, rootNodeMatrix.m32, rootNodeMatrix.m33
			);
		}
		catch (Assimp.AssimpException e)
		{
			Debug.LogError(e.Message);
		}

		return scene;
	}
}