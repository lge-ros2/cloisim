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
		if (fileExtension ==  ".dae" ||
			fileExtension ==  ".obj" ||
			fileExtension ==  ".stl" ||
			fileExtension ==  ".fbx")
			return true;
		else
			return false;
	}

	private static SN.Quaternion GetRotationByFileExtension(in string fileExtension, in string meshPath)
	{
		if (fileExtension == ".obj" || fileExtension == ".stl")
			return Quaternion.Euler(90, -90, 0).ToNumerics();
		else if (fileExtension == ".dae")
			return Quaternion.Euler(0, -90, 0).ToNumerics();
		else // ".fbx" or etc
			return Quaternion.identity.ToNumerics();
	}

	private static SN.Quaternion ToNumerics(this Quaternion q)
	{
		return new SN.Quaternion(q.x, q.y, q.z, q.w);
	}

	private static SN.Matrix4x4 Transpose(this SN.Matrix4x4 m)
	{
		return SN.Matrix4x4.Transpose(m);
	}

	private static Color ToUnity(this SN.Vector4 color)
		=> (color == null) ? Color.clear : new Color(color.X, color.Y, color.Z, color.W);

	private static Matrix4x4 ToUnity(this SN.Matrix4x4 m)
		=> new Matrix4x4(
			new Vector4(m.M11, m.M21, m.M31, m.M41),
			new Vector4(m.M12, m.M22, m.M32, m.M42),
			new Vector4(m.M13, m.M23, m.M33, m.M43),
			new Vector4(m.M14, m.M24, m.M34, m.M44)
		);

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

			// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
			var meshRotation = GetRotationByFileExtension(fileExtension, targetPath);
			rootNode.Transform = SN.Matrix4x4.CreateFromQuaternion(meshRotation).Transpose() * rootNode.Transform;

			return scene;
		}
		catch (Assimp.AssimpException e)
		{
			Debug.LogError(e.Message);
		}

		return null;
	}
}