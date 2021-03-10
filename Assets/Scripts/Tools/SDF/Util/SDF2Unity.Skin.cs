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
	private static GameObject GetBonesFromAssimpNode(in Assimp.Node node, in float scaleX = 1, in float scaleY = 1, in float scaleZ = 1)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("RootObject: " + rootObject.name);

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
			Assimp.PostProcessSteps.OptimizeGraph |
			Assimp.PostProcessSteps.OptimizeMeshes |
			Assimp.PostProcessSteps.JoinIdenticalVertices |
			Assimp.PostProcessSteps.SortByPrimitiveType |
			Assimp.PostProcessSteps.RemoveRedundantMaterials |
			Assimp.PostProcessSteps.ImproveCacheLocality |
			// Assimp.PostProcessSteps.SplitLargeMeshes |
			// Assimp.PostProcessSteps.GenerateSmoothNormals |
			Assimp.PostProcessSteps.Triangulate |
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

		// bone
		var rootNode = scene.RootNode;
		if (rootNode.ChildCount != 2)
		{
			Debug.LogError("file(" + meshPath + ") has wrong number of children: " + rootNode.ChildCount);
			return;
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

		combinedMesh.Optimize();
		combinedMesh.RecalculateTangents();
		combinedMesh.RecalculateBounds();
		combinedMesh.RecalculateNormals();

		var bonesObject = GetBonesFromAssimpNode(rootNode.Children[0]);
		bonesObject.transform.SetParent(targetObject.transform, false);

		var meshObject = new GameObject(rootNode.Children[1].Name);
		meshObject.transform.SetParent(targetObject.transform, false);
		combinedMesh.name = meshObject.name;

		var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
		skinnedMeshRenderer.sharedMesh = combinedMesh;
		skinnedMeshRenderer.rootBone = bonesObject.transform.GetChild(0);
		skinnedMeshRenderer.materials = materials.ToArray();
	}
}