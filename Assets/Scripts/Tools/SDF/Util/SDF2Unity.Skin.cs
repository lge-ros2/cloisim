/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using UnityEngine;

public partial class SDF2Unity
{
	private static Dictionary<string, int> boneNameIndexMap = new Dictionary<string, int>();

	private static GameObject GetBonesFromAssimpNode(in Assimp.Node node, in float scaleX = 1, in float scaleY = 1, in float scaleZ = 1)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("Bone Object: " + rootObject.name);

		node.Transform.Decompose(out var nodeScale, out var nodeQuat, out var nodeTranslation);
		// Debug.Log(node.Name + ", " + nodeScale + ", " + nodeQuat + ", " + nodeTranslation);

		// Convert Assimp transfrom into Unity transform
		rootObject.transform.localPosition = new Vector3(nodeTranslation.X, nodeTranslation.Y, nodeTranslation.Z);
		rootObject.transform.localRotation = new Quaternion(nodeQuat.X, nodeQuat.Y, nodeQuat.Z, nodeQuat.W);
		rootObject.transform.localScale = new Vector3(nodeScale.X, nodeScale.Y, nodeScale.Z);

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				// Debug.Log(" => Child Object: " + child.Name);
				var childObject = GetBonesFromAssimpNode(child, scaleX, scaleY, scaleZ);
				childObject.transform.SetParent(rootObject.transform, false);
			}
		}

		return rootObject;
	}

	public static void LoadSkinObject(in GameObject targetObject, in string meshPath)
	{
		LoadSkinObject(targetObject, meshPath, Vector3.one);
	}

	public static void LoadSkinObject(in GameObject targetObject, in string meshPath, in Vector3 scale)
	{
		if (!File.Exists(meshPath))
		{
			Debug.Log("File doesn't exist: " + meshPath);
			return;
		}

		var postProcessFlags =
			// Assimp.PostProcessSteps.OptimizeGraph |
			// Assimp.PostProcessSteps.OptimizeMeshes |
			Assimp.PostProcessSteps.JoinIdenticalVertices |
			// Assimp.PostProcessSteps.SortByPrimitiveType |
			Assimp.PostProcessSteps.RemoveRedundantMaterials |
			// Assimp.PostProcessSteps.ImproveCacheLocality |
			Assimp.PostProcessSteps.Triangulate |
			// Assimp.PostProcessSteps.SplitByBoneCount |
			Assimp.PostProcessSteps.CalculateTangentSpace |
			Assimp.PostProcessSteps.MakeLeftHanded;

		var scene = importer.ImportFile(meshPath, postProcessFlags);
		if (scene == null)
		{
			return;
		}

		var fileExtension = Path.GetExtension(meshPath).ToLower();
		var eulerRotation = GetRotationByFileExtension(fileExtension, meshPath);

		if (!CheckFileSupport(fileExtension))
		{
			Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + meshPath);
			return;
		}

		// Check structure
		var rootNode = scene.RootNode;
		if (rootNode.ChildCount != 2)
		{
			Debug.LogError("file(" + meshPath + ") has wrong number of children: " + rootNode.ChildCount);
			return;
		}

		// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
		var meshRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, eulerRotation.z);

		var meshObject = new GameObject(rootNode.Children[1].Name);
		meshObject.transform.SetParent(targetObject.transform, false);

		var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();

		// Bone
		var bonesObject = GetBonesFromAssimpNode(rootNode.Children[0]);

		foreach (var transform in bonesObject.GetComponentsInChildren<Transform>())
		{
			// transform.localRotation = meshRotation * transform.localRotation;
		}

		bonesObject.transform.SetParent(targetObject.transform, false);
		var rootBoneObject = bonesObject.transform.GetChild(0);
		skinnedMeshRenderer.rootBone = rootBoneObject;
		skinnedMeshRenderer.bones = rootBoneObject.GetComponentsInChildren<Transform>();

		var boneIndex = 0;
		foreach (var bone in skinnedMeshRenderer.bones)
		{
			Debug.Log("Bone Index: " + bone.name + "=" + boneIndex);
			boneNameIndexMap.Add(bone.name, boneIndex++);
		}

		// Meshes
		List<MeshMaterialSet> meshMats = null;
		if (scene.HasMeshes)
		{
			// Materials
			List<Material> meshMaterials = null;
			if (scene.HasMaterials)
			{
				var parentPath = Directory.GetParent(meshPath).FullName;
				meshMaterials = LoadMaterials(parentPath, scene.Materials);
			}

			meshMats = LoadMeshes(scene.Meshes, meshMaterials);
		}

		var materials = new List<Material>();
		var combine = new CombineInstance[meshMats.Count];
		var combineIndex = 0;
		foreach (var meshMat in meshMats)
		{
			combine[combineIndex].mesh = meshMat.Mesh;
			combine[combineIndex].transform = Matrix4x4.identity;
			combineIndex++;
			materials.Add(meshMat.Material);
		}

		var combinedMesh = new Mesh();
		combinedMesh.CombineMeshes(combine, false);
		combinedMesh.name = meshObject.name;
		// combinedMesh.Optimize();
		// combinedMesh.RecalculateTangents();
		combinedMesh.RecalculateBounds();
		// combinedMesh.RecalculateNormals();
		skinnedMeshRenderer.sharedMesh = combinedMesh;
		skinnedMeshRenderer.sharedMaterials = materials.ToArray();
	}
}