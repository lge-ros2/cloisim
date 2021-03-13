/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System;
using UnityEngine;
using UnityEngine.Rendering;

public partial class SDF2Unity
{
	public class BoneWeightItem
	{
		public List<Tuple<int, float>> items = new List<Tuple<int, float>>(4);

		public void AddPair(in int index, in float weight)
		{
			items.Add(new Tuple<int, float>(index, weight));
			// if (items.Count == 2)
			// {
			// 	Debug.Log("legnth 2");
			// }
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

		public int Length => vertexBoneWeightList.Length;

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

	private static List<Material> LoadMaterials(in string parentPath, in List<Assimp.Material> sceneMaterials)
	{
		var materials = new List<Material>();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = new Material(commonShader);

			mat.name = sceneMat.Name;

			// Albedo
			if (sceneMat.HasColorDiffuse)
			{
				var color = new Color(sceneMat.ColorDiffuse.R, sceneMat.ColorDiffuse.G, sceneMat.ColorDiffuse.B, sceneMat.ColorDiffuse.A);
				mat.color = color;
			}

			// Emission
			if (sceneMat.HasColorEmissive)
			{
				var color = new Color(sceneMat.ColorEmissive.R, sceneMat.ColorEmissive.G, sceneMat.ColorEmissive.B, sceneMat.ColorEmissive.A);
				mat.SetColor("_EmissionColor", color);
				mat.EnableKeyword("_EMISSION");
			}

			// Reflectivity
			if (sceneMat.HasReflectivity)
			{
				mat.SetFloat("_Glossiness", sceneMat.Reflectivity);
			}

			// Texture
			if (sceneMat.HasTextureDiffuse)
			{
				var filePath = sceneMat.TextureDiffuse.FilePath;
				var texturePaths = new List<string>(){};

				foreach (var matPath in possibleMaterialPaths)
				{
					texturePaths.Add(Path.Combine(parentPath, matPath, filePath));
				}

				byte[] byteArray = null;
				foreach (var texturePath in texturePaths)
				{
					if (File.Exists(texturePath))
					{
						byteArray = File.ReadAllBytes(texturePath);
						if (byteArray != null)
						{
							break;
						}
					}
				}

				var texture = new Texture2D(2, 2);
				var isLoaded = texture.LoadImage(byteArray);
				if (!isLoaded)
				{
					throw new Exception("Cannot find texture file: " + filePath);
				}

				mat.SetTexture("_MainTex", texture);
			}

			materials.Add(mat);
		}

		return materials;
	}

	private static List<MeshMaterialSet> LoadMeshes(in List<Assimp.Mesh> sceneMeshes)
	{
		return LoadMeshes(sceneMeshes, Quaternion.identity);
	}

	private static List<MeshMaterialSet> LoadMeshes(in List<Assimp.Mesh> sceneMeshes, in Quaternion meshRotation)
	{
		var meshMats = new List<MeshMaterialSet>();

		foreach (var sceneMesh in sceneMeshes)
		{
			var newMesh = new Mesh();

			newMesh.name = sceneMesh.Name;

			// Vertices
			if (sceneMesh.HasVertices)
			{
				newMesh.indexFormat = (sceneMesh.VertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;

				var vertices = new Queue<Vector3>();
				foreach (var v in sceneMesh.Vertices)
				{
					var vertex = new Vector3(v.X, v.Y, v.Z);
					vertex = meshRotation * vertex;
					vertices.Enqueue(vertex);
				}

				newMesh.vertices = vertices.ToArray();
			}

			// UV (texture coordinate)
			if (sceneMesh.HasTextureCoords(0))
			{
				var uvs = new Queue<Vector2>();
				foreach (var uv in sceneMesh.TextureCoordinateChannels[0])
				{
					uvs.Enqueue(new Vector2(uv.X, uv.Y));
				}

				newMesh.uv = uvs.ToArray();
			}

			// Triangles
			if (sceneMesh.HasFaces)
			{
				var indices = new Queue<int>();
				foreach (var face in sceneMesh.Faces)
				{
					if (face.IndexCount == 3)
					{
						indices.Enqueue(face.Indices[2]);
						indices.Enqueue(face.Indices[1]);
						indices.Enqueue(face.Indices[0]);
					}
				}

				newMesh.triangles = indices.ToArray();
			}

			// Normals
			if (sceneMesh.HasNormals)
			{
				var normals = new Queue<Vector3>();
				foreach (var n in sceneMesh.Normals)
				{
					normals.Enqueue(new Vector3(n.X, n.Y, n.Z));
				}

				newMesh.normals = normals.ToArray();
			}

			// Tangent
			if (sceneMesh.HasTangentBasis)
			{
				var tangents = new Queue<Vector4>();
				foreach (var t in sceneMesh.Tangents)
				{
					tangents.Enqueue(new Vector4(t.X, t.Y, t.Z, 1));
				}
				newMesh.tangents = tangents.ToArray();
			}

			// Bones
			if (sceneMesh.HasBones)
			{
				var bindPoses = new Queue<Matrix4x4>();

				var boneWeightList = new BoneWeightItemList(newMesh.vertexCount);
				// Debug.Log(newMesh.name + ": vertexCount=" + newMesh.vertexCount + " or " + sceneMesh.VertexCount + ", boneCount=" + sceneMesh.BoneCount + ", " + sceneMesh.Bones.Count);

				foreach (var bone in sceneMesh.Bones)
				{
					boneNameIndexMap.TryGetValue(bone.Name, out var tupleBone);
					var boneIndex = tupleBone.Item1;
					// Debug.Log(bone.Name + ", index= " + boneIndex + "--------------- " + bone.OffsetMatrix.ToString());

					var offsetMat = bone.OffsetMatrix;
					offsetMat.Decompose(out var scaling, out var rotation, out var translation);
					var pos = new Vector3(translation.X, translation.Y, translation.Z);
					var q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
					var s = new Vector3(scaling.X, scaling.Y, scaling.Z);
					// Debug.Log(bone.Name + " - " + pos + q + s);
					var finalMat = new Matrix4x4();
					finalMat.SetTRS(pos, q, s);

					var jointNode = tupleBone.Item2;

					var localToWorldMat = jointNode.localToWorldMatrix;
					var worldToLocalMat = jointNode.worldToLocalMatrix;
					var parentLocalToWorldMat = jointNode.parent.localToWorldMatrix;
					var parentworldToLocalMat = jointNode.parent.worldToLocalMatrix;

					bindPoses.Enqueue(finalMat);

					boneIndexPose[boneIndex] = finalMat;
					Debug.Log(boneIndex + " \n" + finalMat);


					// if (boneIndexPose.TryGetValue(boneIndex, out var currentMat))
					// {
					// 	currentMat = currentMat * finalMat;
					// }
					// else
					// {
					// 	boneIndexPose.Add(boneIndex, finalMat);
					// }

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
					Debug.LogWarning("bone index=" + boneIndex + ", bonename=" + bone.Name + ", bindpose number=" + bindPoses.Count);
					// Debug.Log(finalMat);
				}

				Debug.LogWarning("bone weight number:" + boneWeightList.Length + ", bindpose number=" + bindPoses.Count);
				newMesh.bindposes = bindPoses.ToArray();
				// newMesh.bindposes = bindPoses2;
				newMesh.boneWeights = boneWeightList.GetBoneWeightsArray();
			}

			meshMats.Add(new MeshMaterialSet(newMesh, sceneMesh.MaterialIndex));
			// Debug.Log("Done - " + sceneMesh.Name + ", " + newMesh.vertexCount + " : " + sceneMesh.MaterialIndex + ", " + newMesh.bindposes.LongLength);
		}

		return meshMats;
	}

	private static GameObject ConvertAssimpNodeToMeshObject(in Assimp.Node node, in List<MeshMaterialSet> meshMats, in float scaleX = 1, in float scaleY = 1, in float scaleZ = 1)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("RootObject: " + rootObject.name);

		// Set Mesh
		if (node.HasMeshes)
		{
			foreach (var index in node.MeshIndices)
			{
				var meshMat = meshMats[index];

				var subObject = new GameObject(meshMat.Mesh.name);
				var meshFilter = subObject.AddComponent<MeshFilter>();
				var meshRenderer = subObject.AddComponent<MeshRenderer>();

				meshFilter.mesh = meshMat.Mesh;
				meshRenderer.material = meshMat.Material;

				subObject.transform.SetParent(rootObject.transform, true);
				subObject.transform.localScale = new Vector3(scaleX, scaleY, scaleZ);
				// Debug.Log("Sub Object: " + subObject.name);
			}
		}

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
				var childObject = ConvertAssimpNodeToMeshObject(child, meshMats, scaleX, scaleY, scaleZ);
				childObject.transform.SetParent(rootObject.transform, false);
			}
		}

		return rootObject;
	}

	public static GameObject LoadMeshObject(in string meshPath)
	{
		return LoadMeshObject(meshPath, Vector3.one);
	}

	public static GameObject LoadMeshObject(in string meshPath, in Vector3 scale)
	{
		if (!File.Exists(meshPath))
		{
			Debug.Log("File doesn't exist: " + meshPath);
			return null;
		}

		// var colladaIgnoreConfig = new Assimp.Configs.ColladaIgnoreUpDirectionConfig(false);
		// importer.SetConfig(colladaIgnoreConfig);

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

		// Materials
		List<Material> materials = null;
		if (scene.HasMaterials)
		{
			var parentPath = Directory.GetParent(meshPath).FullName;
			materials = LoadMaterials(parentPath, scene.Materials);
		}

		// Meshes
		List<MeshMaterialSet> meshMats = null;
		if (scene.HasMeshes)
		{
			// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
			var meshRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, eulerRotation.z);

			// meshMats = LoadMeshes(scene.Meshes, meshRotation);
			meshMats = LoadMeshes(scene.Meshes);
		}

		// match materials
		foreach (var meshMat in meshMats)
		{
			meshMat.SetMaterial(materials[meshMat.MaterialIndex]);
		}

		// Create GameObjects from nodes
		var nodeObject = ConvertAssimpNodeToMeshObject(scene.RootNode, meshMats, scale.x, scale.y, scale.z);

		return nodeObject;
	}
}