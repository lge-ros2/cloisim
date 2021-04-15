/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEngine;

public partial class MeshLoader
{
	private static int boneMapIndex;
	private static Dictionary<string, Tuple<int, Transform>> boneNameIndexMap = new Dictionary<string, Tuple<int, Transform>>();

	public class BindPoseList
	{
		private Matrix4x4[] bindPoses = null;

		public BindPoseList(in int length)
		{
			bindPoses = new Matrix4x4[length];
			for (var i = 0; i < bindPoses.Length; i++)
			{
				bindPoses[i] = Matrix4x4.identity;
			}
		}

		public void SetBindPose(in int boneIndex, in Matrix4x4 pose)
		{
			bindPoses[boneIndex] = pose;
		}

		public Matrix4x4[] BindPoses => bindPoses;
	}

	public class BoneWeightItem
	{
		public List<Tuple<int, float>> items = new List<Tuple<int, float>>(4);

		public void AddPair(in int index, in float weight)
		{
			items.Add(new Tuple<int, float>(index, weight));
		}

		public List<Tuple<int, float>> Sort()
		{
			var sortedItems = items.OrderByDescending(x => x.Item2);
			return sortedItems.ToList();
		}
	};

	public class BoneWeightItemList
	{
		private BoneWeightItem[] vertexBoneWeightList;

		public BoneWeightItemList(in int length)
		{
			vertexBoneWeightList = new BoneWeightItem[length];
			for (var i = 0; i < vertexBoneWeightList.Length; i++)
			{
				vertexBoneWeightList[i] = new BoneWeightItem();
			}
		}

		public BoneWeightItem GetItem(in int index)
		{
			return vertexBoneWeightList[index];
		}

		public BoneWeight[] GetBoneWeightsArray()
		{
			var bones = new BoneWeight[vertexBoneWeightList.Length];
			for (var i = 0; i < vertexBoneWeightList.Length; i++)
			{
				bones[i] = new BoneWeight();

				var temp = vertexBoneWeightList[i].Sort();

				if (temp.Count > 0)
				{
					bones[i].boneIndex0 = temp[0].Item1;
					bones[i].weight0 = temp[0].Item2;
				}

				if (temp.Count > 1)
				{
					bones[i].boneIndex1 = temp[1].Item1;
					bones[i].weight1 = temp[1].Item2;
				}

				if (temp.Count > 2)
				{
					bones[i].boneIndex2 = temp[2].Item1;
					bones[i].weight2 = temp[2].Item2;
				}

				if (temp.Count > 3)
				{
					bones[i].boneIndex3 = temp[3].Item1;
					bones[i].weight3 = temp[3].Item2;
				}
			}
			return bones;
		}
	};

	private static BindPoseList LoadBones(in List<Assimp.Mesh> sceneMeshes, in int totalBones, in MeshMaterialList meshMatList)
	{
		var bindPoseList = new BindPoseList(totalBones);
		var meshIndex = 0;
		foreach (var sceneMesh in sceneMeshes)
		{
			var meshMat = meshMatList.Get(meshIndex++);

			// Bones
			if (meshMat != null && sceneMesh.HasBones)
			{
				var mesh = meshMat.Mesh;
				var boneWeightList = new BoneWeightItemList(mesh.vertexCount);
				// Debug.Log(newMesh.name + ": vertexCount=" + newMesh.vertexCount + " or " + sceneMesh.VertexCount + ", boneCount=" + sceneMesh.BoneCount + ", " + sceneMesh.Bones.Count);

				foreach (var bone in sceneMesh.Bones)
				{
					boneNameIndexMap.TryGetValue(bone.Name, out var tupleBone);
					var boneIndex = tupleBone.Item1;
					// Debug.Log(bone.Name + ", index= " + boneIndex + "--------------- " + bone.OffsetMatrix.ToString());
					var bindPoseMat = ConvertAssimpMatrix4x4ToUnity(bone.OffsetMatrix);
					bindPoseList.SetBindPose(boneIndex, bindPoseMat);

					if (bone.HasVertexWeights)
					{
						// Debug.Log("BoneName to Index " + bone.Name + " => " + boneIndex + ", weight count=" + bone.VertexWeights.Count);
						foreach (var vertexWeight in bone.VertexWeights)
						{
							if (vertexWeight.Weight > 0)
							{
								var boneWeight = boneWeightList.GetItem(vertexWeight.VertexID);
								boneWeight.AddPair(boneIndex, vertexWeight.Weight);
								// Debug.LogWarning(boneWeight.items.Count + " -- vid: " + vertexWeight.VertexID + "," + boneIndex + ", " + vertexWeight.Weight);
							}
							else
							{
								Debug.LogWarning("weight is zero!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!1");
							}
						}
					}
					// Debug.LogWarning("bone index=" + boneIndex + ", bonename=" + bone.Name + ", bindpose number=" + bindPoses.Count);
				}

				// Debug.LogWarning("bone weight number:boneIndexPose" + boneWeightList.Length + ", bindpose number=" + bindPoses.Count);
				mesh.boneWeights = boneWeightList.GetBoneWeightsArray();
			}
		}

		return bindPoseList;
	}

	private static GameObject GetBonesFromAssimpNode(in Assimp.Node node, in float scale)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("Bone Object: " + rootObject.name);

		// Convert Assimp transfrom into Unity transform
		var nodeTransform = ConvertAssimpMatrix4x4ToUnity(node.Transform);
		rootObject.transform.position = nodeTransform.GetColumn(3);
		rootObject.transform.rotation = nodeTransform.rotation;
		rootObject.transform.localScale = nodeTransform.lossyScale * scale;
		// Debug.Log(node.Name + ", " + rootObject.transform.position + ", " + rootObject.transform.rotation + ", " + rootObject.transform.localScale);

		var boneIndex = boneMapIndex++;
		boneNameIndexMap.Add(node.Name, new Tuple<int, Transform>(boneIndex, rootObject.transform));
		// Debug.Log(" => Bone Object: " + node.Name + " bone index = " + boneIndex);

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				var childObject = GetBonesFromAssimpNode(child, scale);
				childObject.transform.SetParent(rootObject.transform, true);
			}
		}

		return rootObject;
	}

	public static GameObject CreateSkinObject(in string meshPath, in float scale)
	{
		var scene = GetScene(meshPath, out var meshRotation);
		if (scene == null)
		{
			return null;
		}

		// Check structure
		var rootNode = scene.RootNode;
		if (rootNode.ChildCount != 2)
		{
			Debug.LogError("file(" + meshPath + ") has wrong number of children: " + rootNode.ChildCount);
			return null;
		}

		boneMapIndex = -2;
		boneNameIndexMap.Clear();
		var rootObject = GetBonesFromAssimpNode(rootNode, scale);

		var meshObject = rootObject.transform.GetChild(1).gameObject;

		var skinnedMeshRenderer = meshObject.AddComponent<SkinnedMeshRenderer>();
		skinnedMeshRenderer.updateWhenOffscreen = true;

		var rootBoneTransform = rootObject.transform.GetChild(0);
		var rootBoneRotation = Quaternion.Euler(90, -90, 0);
		rootBoneTransform.localRotation *= rootBoneRotation;
		skinnedMeshRenderer.rootBone = rootBoneTransform.GetChild(0);

		var bones = skinnedMeshRenderer.rootBone.GetComponentsInChildren<Transform>();
		skinnedMeshRenderer.bones = bones;

		// Materials
		List<Material> materials = null;
		if (scene.HasMaterials)
		{
			materials = LoadMaterials(meshPath, scene.Materials);
		}

		// Meshes
		MeshMaterialList meshMatList = null;
		BindPoseList bindPoseList = null;
		if (scene.HasMeshes)
		{
			// additional rotation for skin loading
			meshMatList = LoadMeshes(scene.Meshes);
			meshMatList.SetMaterials(materials);

			// Bones
			var totalBones = skinnedMeshRenderer.bones.Length;
			bindPoseList = LoadBones(scene.Meshes, totalBones, meshMatList);
		}

		var combinedBoneWeights = new List<BoneWeight>();
		var combinedMaterials = new List<Material>();
		var combine = new CombineInstance[meshMatList.Count];
		for (var combineIndex = 0; combineIndex < meshMatList.Count; combineIndex++)
		{
			var meshMat = meshMatList.Get(combineIndex);
			combinedMaterials.Add(meshMat.Material);
			combine[combineIndex].mesh = meshMat.Mesh;
			combine[combineIndex].transform = Matrix4x4.identity;
			combinedBoneWeights.AddRange(meshMat.Mesh.boneWeights);
		}

		var combinedMesh = new Mesh();
		combinedMesh.name = meshObject.name;
		combinedMesh.CombineMeshes(combine, false, true);
		combinedMesh.bindposes = bindPoseList.BindPoses;
		combinedMesh.boneWeights = combinedBoneWeights.ToArray();
		combinedMesh.Optimize();

		skinnedMeshRenderer.sharedMaterials = combinedMaterials.ToArray();
		skinnedMeshRenderer.sharedMesh = combinedMesh;

		return rootObject;
	}
}