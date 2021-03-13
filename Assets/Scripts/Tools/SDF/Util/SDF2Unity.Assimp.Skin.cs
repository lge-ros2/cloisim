/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;

public partial class SDF2Unity
{
	private static int boneMapIndex = -1;
	private static Dictionary<string, Tuple<int,Transform>> boneNameIndexMap = new Dictionary<string, Tuple<int,Transform>>();
	// private static Dictionary<int, Matrix4x4> boneIndexPose = new Dictionary<int, Matrix4x4>();
	private static Matrix4x4[] boneIndexPose = null;


	private static GameObject GetBonesFromAssimpNode(in Assimp.Node node, in float scaleX = 1, in float scaleY = 1, in float scaleZ = 1)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("Bone Object: " + rootObject.name);

		// Convert Assimp transfrom into Unity transform
		node.Transform.Decompose(out var nodeScale, out var nodeQuat, out var nodeTranslation);
		rootObject.transform.localPosition = new Vector3(nodeTranslation.X, nodeTranslation.Y, nodeTranslation.Z);
		rootObject.transform.localRotation = new Quaternion(nodeQuat.X, nodeQuat.Y, nodeQuat.Z, nodeQuat.W);
		rootObject.transform.localScale = new Vector3(nodeScale.X, nodeScale.Y, nodeScale.Z);
		// Debug.Log(node.Name + ", " + nodeScale + ", " + nodeQuat + ", " + nodeTranslation);

		var boneIndex = boneMapIndex++;
		boneNameIndexMap.Add(node.Name, new Tuple<int, Transform>(boneIndex, rootObject.transform));
		Debug.Log(" => Bone Object: " + node.Name + " bone index = " + boneIndex);

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				var childObject = GetBonesFromAssimpNode(child, scaleX, scaleY, scaleZ);
				childObject.transform.SetParent(rootObject.transform, false);
			}
		}


		return rootObject;
	}

	public static GameObject LoadSkinObject(in string meshPath)
	{
		return LoadSkinObject(meshPath, Vector3.one);
	}

	public static GameObject LoadSkinObject(in string meshPath, in Vector3 scale)
	{
		if (!File.Exists(meshPath))
		{
			Debug.Log("File doesn't exist: " + meshPath);
			return null;
		}

		var colladaIgnoreConfig = new Assimp.Configs.ColladaIgnoreUpDirectionConfig(false);
		importer.SetConfig(colladaIgnoreConfig);

		var postProcessFlags =
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

		// var logstream = new Assimp.LogStream(delegate (String msg, String userData)
		// 		{
		// 			// Console.WriteLine(msg);
		// 			Debug.Log(msg);
		// 		});
		// logstream.Attach();

		var scene = importer.ImportFile(meshPath, postProcessFlags);
		if (scene == null)
		{
			return null;
		}

		var fileExtension = Path.GetExtension(meshPath).ToLower();
		var eulerRotation = GetRotationByFileExtension(fileExtension, meshPath);

		if (!CheckFileSupport(fileExtension))
		{
			Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + meshPath);
			return null;
		}

		// Check structure
		var rootNode = scene.RootNode;
		if (rootNode.ChildCount != 2)
		{
			Debug.LogError("file(" + meshPath + ") has wrong number of children: " + rootNode.ChildCount);
			return null;
		}

		// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
		var meshRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, eulerRotation.z);

		var rootObject = GetBonesFromAssimpNode(rootNode);
		// foreach (var transform in rootObject.GetComponentsInChildren<Transform>())
		{
			// transform.localRotation = meshRotation * transform.localRotation;
		}

		var meshObject = rootObject.transform.GetChild(1).gameObject;
		var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();

		var rootBoneTransform = rootObject.transform.GetChild(0);
		skinnedMeshRenderer.rootBone = rootBoneTransform;

		var bones = rootBoneTransform.GetComponentsInChildren<Transform>();
		skinnedMeshRenderer.bones = bones;

		boneIndexPose = new Matrix4x4[bones.Length];
		for (var i = 0; i < boneIndexPose.Length; i++)
		{
			boneIndexPose[i] = Matrix4x4.identity;
		}

		// Materials
		List<Material> meshMaterials = null;
		if (scene.HasMaterials)
		{
			var parentPath = Directory.GetParent(meshPath).FullName;
			meshMaterials = LoadMaterials(parentPath, scene.Materials);
		}

		// Meshes
		List<MeshMaterialSet> meshMats = null;
		if (scene.HasMeshes)
		{
			meshMats = LoadMeshes(scene.Meshes);
		}

		// match materials
		foreach (var meshMat in meshMats)
		{
			meshMat.SetMaterial(meshMaterials[meshMat.MaterialIndex]);
		}

		var combinedBoneWeights = new List<BoneWeight>();
		var combinedMaterials = new List<Material>();

		var combineIndex = 0;
		var combine = new CombineInstance[meshMats.Count];
		foreach (var meshMat in meshMats)
		{
			combinedMaterials.Add(meshMaterials[meshMat.MaterialIndex]);
			combine[combineIndex].mesh = meshMat.Mesh;
			combine[combineIndex].subMeshIndex = 0;
			combine[combineIndex].transform = Matrix4x4.identity;

			combinedBoneWeights.AddRange(meshMat.Mesh.boneWeights);

			combineIndex++;
		}

		var combinedMesh = new Mesh();
		combinedMesh.name = meshObject.name;
		combinedMesh.CombineMeshes(combine, false, true);
		combinedMesh.bindposes = boneIndexPose;
		combinedMesh.boneWeights = combinedBoneWeights.ToArray();
		combinedMesh.Optimize();
		// combinedMesh.RecalculateBounds();

		skinnedMeshRenderer.sharedMaterials = combinedMaterials.ToArray();
		skinnedMeshRenderer.sharedMesh = combinedMesh;
		// skinnedMeshRenderer.localBounds = combinedMesh.bounds;

		boneMapIndex = -1;
		boneNameIndexMap.Clear();

		return rootObject;
	}
}