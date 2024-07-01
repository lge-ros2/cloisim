/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class MeshLoader
{
	private static Dictionary<string, GameObject> MeshCache = new Dictionary<string, GameObject>();

	public static Texture2D GetTexture(in string textureFullPath)
	{
		if (!string.IsNullOrEmpty(textureFullPath) && File.Exists(textureFullPath))
		{
			var byteArray = File.ReadAllBytes(textureFullPath);
			if (byteArray != null)
			{
				var texture = new Texture2D(1, 1);
				if (texture.LoadImage(byteArray))
				{
					return texture;
				}
				else
				{
					throw new Exception("Cannot find texture file: " + textureFullPath);
				}
			}
		}

		return null;
	}

	private static List<Material> LoadMaterials(in string meshPath, in List<Assimp.Material> sceneMaterials)
	{
		var parentPath = Directory.GetParent(meshPath).FullName;
		var textureDirectories = GetRootTexturePaths(parentPath);
		var materials = new List<Material>();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = SDF2Unity.Material.Create(sceneMat.Name);

			if (sceneMat.HasColorAmbient)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": ColorAmbient but not support. " + sceneMat.ColorAmbient.ToUnity());
#endif
			}

			if (sceneMat.HasColorDiffuse)
			{
				SDF2Unity.Material.SetBaseColor(mat, sceneMat.ColorDiffuse.ToUnity());
				// Debug.Log(sceneMat.Name + ": HasColorDiffuse " + sceneMat.ColorDiffuse.ToUnity());
			}

			if (sceneMat.HasColorEmissive)
			{
				SDF2Unity.Material.SetEmission(mat, sceneMat.ColorEmissive.ToUnity());
				// Debug.Log(sceneMat.Name + ": HasColorEmissive " + sceneMat.ColorEmissive.ToUnity());
			}

			if (sceneMat.HasColorSpecular)
			{
				SDF2Unity.Material.SetSpecular(mat, sceneMat.ColorSpecular.ToUnity());
				// Debug.Log(sceneMat.Name + ": HasColorSpecular " + sceneMat.ColorSpecular.ToUnity());
			}

			if (sceneMat.HasColorTransparent)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasColorTransparent but not support. " + sceneMat.ColorTransparent);
#endif
			}

			// Reflectivity
			if (sceneMat.HasReflectivity)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasReflectivity but not support. " + sceneMat.Reflectivity);
#endif
			}

			// reflective
			if (sceneMat.HasColorReflective)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasColorReflective but not support. " + sceneMat.ColorReflective);
#endif
			}

			if (sceneMat.HasShininess)
			{
				mat.SetFloat("_Shininess", sceneMat.Shininess);
			}

			// Texture
			if (sceneMat.HasTextureAmbient)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureAmbient but not support. " + sceneMat.TextureAmbient.FilePath);
#endif
			}

			if (sceneMat.HasTextureDiffuse)
			{
				var filePath = sceneMat.TextureDiffuse.FilePath;
				if (filePath.Contains("model://"))
				{
					filePath = filePath.Replace("model://", "");
				}

				foreach (var textureDirectory in textureDirectories)
				{
					var textureFullPath = Path.Combine(textureDirectory, filePath);
					if (File.Exists(textureFullPath))
					{
						mat.SetTexture("_BaseMap", GetTexture(textureFullPath));
						// Debug.Log(sceneMat.Name + ": HasTextureDiffuse -> " + filePath);
						break;
					}
				}
			}

			if (sceneMat.HasTextureEmissive)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureEmissive but not support. " + sceneMat.TextureEmissive.FilePath);
#endif
			}

			if (sceneMat.HasTextureSpecular)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureSpecular but not support. " + sceneMat.TextureSpecular.FilePath);
#endif
			}

			if (sceneMat.HasTextureDisplacement)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureDisplacement but not support. " + sceneMat.TextureDisplacement.FilePath);
#endif
			}

			if (sceneMat.HasTextureHeight)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureHeight but not support. " + sceneMat.TextureHeight.FilePath);
#endif
			}

			if (sceneMat.HasBumpScaling)
			{
				mat.SetFloat("_BumpScale", sceneMat.BumpScaling);
				Debug.Log(sceneMat.Name + ": HasBumpScaling but not support. " + sceneMat.BumpScaling);
			}

			if (sceneMat.HasTextureNormal)
			{
				var filePath = sceneMat.TextureNormal.FilePath;
				foreach (var textureDirectory in textureDirectories)
				{
					var textureFullPath = Path.Combine(textureDirectory, filePath);
					if (File.Exists(textureFullPath))
					{
						mat.SetTexture("_BumpMap", GetTexture(textureFullPath));
						// Debug.Log(sceneMat.Name + ": HasTextureNormal -> " + filePath);
						break;
					}
				}
				// Debug.Log(sceneMat.Name + ": HasTextureNormal but not support. " + sceneMat.TextureNormal.FilePath);
			}

			if (sceneMat.HasTextureOpacity)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureOpacity but not support. " + sceneMat.TextureOpacity.FilePath);
#endif
			}

			if (sceneMat.HasTextureReflection)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureReflection but not support. " + sceneMat.TextureReflection.FilePath);
#endif
			}

			if (sceneMat.HasTextureLightMap)
			{
#if UNITY_EDITOR
				Debug.Log(sceneMat.Name + ": HasTextureLightMap but not support. " + sceneMat.TextureLightMap.FilePath);
#endif
			}

			materials.Add(mat);
		}

		return materials;
	}

	private static MeshMaterialList LoadMeshes(in List<Assimp.Mesh> sceneMeshes)
	{
		var meshMatList = new MeshMaterialList();

		foreach (var sceneMesh in sceneMeshes)
		{
			var newMesh = new Mesh();
			newMesh.name = sceneMesh.Name;

			if (sceneMesh.VertexCount < 3)
			{
				Debug.LogWarning($"{sceneMesh.Name}: Vertex count is less thean 3");
				meshMatList.Add(new MeshMaterial(newMesh, sceneMesh.MaterialIndex));
				continue;
			}

			// Vertices
			if (sceneMesh.HasVertices)
			{
				newMesh.indexFormat = (sceneMesh.VertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;

				var vertices = new Queue<Vector3>();
				foreach (var v in sceneMesh.Vertices)
				{
					vertices.Enqueue(new Vector3(v.X, v.Y, v.Z));
				}

				newMesh.vertices = vertices.ToArray();
			}

			// UV (texture coordinate)
			for (var channelIndex = 0; channelIndex < sceneMesh.TextureCoordinateChannelCount; channelIndex++)
			{
				if (sceneMesh.HasTextureCoords(channelIndex))
				{
					var uvs = new Queue<Vector2>();
					foreach (var uv in sceneMesh.TextureCoordinateChannels[channelIndex])
					{
						uvs.Enqueue(new Vector2(uv.X, uv.Y));
					}

					switch (channelIndex)
					{
						case 0:
							newMesh.uv = uvs.ToArray();
							break;
						case 1:
							newMesh.uv2 = uvs.ToArray();
							break;
						case 2:
							newMesh.uv3 = uvs.ToArray();
							break;
						case 3:
							newMesh.uv4 = uvs.ToArray();
							break;
						case 4:
							newMesh.uv5 = uvs.ToArray();
							break;
						case 5:
							newMesh.uv6 = uvs.ToArray();
							break;
						case 6:
							newMesh.uv7 = uvs.ToArray();
							break;
						case 7:
							newMesh.uv8 = uvs.ToArray();
							break;
						default:
							Debug.LogWarning("Invalid channelIndex: " + channelIndex);
							break;
					}
				}
			}

			// Vertex Color
			for (var channelIndex = 0; channelIndex < sceneMesh.VertexColorChannelCount; channelIndex++)
			{
				if (sceneMesh.HasVertexColors(channelIndex))
				{
					Debug.LogWarning("Has vertex color : " + channelIndex);
				}
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
					else if (face.IndexCount == 2)
					{
						indices.Enqueue(face.Indices[0]);
						indices.Enqueue(face.Indices[1]);
						indices.Enqueue(face.Indices[0]);
					}
					else
					{
						Debug.LogWarning("invalid face index count=" + face.IndexCount);
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
					// Debug.Log(t);
					tangents.Enqueue(new Vector4(t.X, t.Y, t.Z, 1));
				}

				// foreach (var t in sceneMesh.BiTangents)
				// {
				// 	// Debug.Log(t);
				// 	tangents.Enqueue(new Vector4(t.X, t.Y, t.Z, 1));
				// }

				newMesh.tangents = tangents.ToArray();
			}

			// Debug.Log("Done - " + sceneMesh.Name + ", " + newMesh.vertexCount + " : " + sceneMesh.MaterialIndex + ", " + newMesh.bindposes.LongLength);
			meshMatList.Add(new MeshMaterial(newMesh, sceneMesh.MaterialIndex));
		}

		return meshMatList;
	}

	private static GameObject ToUnityMeshObject(
		this Assimp.Node node,
		in MeshMaterialList meshMatList,
		out bool doFlip)
	{
		var nodeObject = new GameObject(node.Name);
		// Debug.Log($"ToUnityMeshObject : {node.Name}");

		// Set Mesh
		if (node.HasMeshes)
		{
			foreach (var index in node.MeshIndices)
			{
				var meshMat = meshMatList[index];

				var subObject = new GameObject(meshMat.mesh.name);

				var meshFilter = subObject.AddComponent<MeshFilter>();
				meshFilter.sharedMesh = meshMat.mesh;

				var meshRenderer = subObject.AddComponent<MeshRenderer>();
				meshRenderer.sharedMaterial = meshMat.material;
				meshRenderer.allowOcclusionWhenDynamic = true;
				meshRenderer.receiveShadows = true;

				subObject.transform.SetParent(nodeObject.transform, true);
				// Debug.Log("Sub Object: " + subObject.name);
			}
		}

		// Convert Assimp transfrom into Unity transform
		var nodeTransformMatrix = node.Transform.ToUnity();
		nodeObject.transform.localPosition = nodeTransformMatrix.GetPosition();
		nodeObject.transform.localRotation = nodeTransformMatrix.rotation;
		nodeObject.transform.localScale = nodeTransformMatrix.lossyScale;

		doFlip = (nodeObject.transform.localScale.x < 0 ||
				  nodeObject.transform.localScale.y < 0 ||
				  nodeObject.transform.localScale.z < 0) ? true : false;

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				if (child.ChildrenCount() == 0)
				{
					continue;
				}

				// Debug.Log(" => Child Object: " + child.Name);
				var childObject = child.ToUnityMeshObject(meshMatList, out var doFlipChild);
				childObject.transform.SetParent(nodeObject.transform, false);

				doFlip |= doFlipChild;
			}
		}

		return nodeObject;
	}

	private static int ChildrenCount(this Assimp.Node node)
	{
		var childrenCount = 0;
		foreach (var child in node.Children)
		{
		 	childrenCount += child.ChildrenCount();
		}
		return node.ChildCount + node.MeshCount + childrenCount;
	}

	public static GameObject CreateMeshObject(in string meshPath, in string subMesh = null)
	{
		GameObject meshObject = null;

		var cacheKey = meshPath + (string.IsNullOrEmpty(subMesh) ? "" : subMesh);

		if (!MeshCache.ContainsKey(cacheKey))
		{
			var scene = GetScene(meshPath, subMesh);
			if (scene == null)
			{
				meshObject = new GameObject("Empty Mesh");
				meshObject.SetActive(false);
				meshObject.tag = "Geometry";
				return meshObject;
			}

			// Meshes
			MeshMaterialList meshMatList = null;
			if (scene.HasMeshes)
			{
				// Materials
				List<Material> materials = null;
				if (scene.HasMaterials)
				{
					materials = LoadMaterials(meshPath, scene.Materials);
				}

				meshMatList = LoadMeshes(scene.Meshes);
				meshMatList.SetMaterials(materials);
			}

			// Create GameObjects from nodes
			var createdMeshObject = scene.RootNode.ToUnityMeshObject(meshMatList, out var doFlip);
			// Debug.Log(createdMeshObject.name + ": " + createdMeshObject.transform.localRotation.eulerAngles);

			if (doFlip)
			{
				createdMeshObject.transform.localScale = -createdMeshObject.transform.localScale;
			}

			createdMeshObject.SetActive(false);
			GameObject.DontDestroyOnLoad(createdMeshObject);

			MeshCache.Add(cacheKey, createdMeshObject);
		}
		else
		{
			Debug.Log($"Use cached mesh({cacheKey}) for {meshPath}");
		}

		meshObject = new GameObject("Non-Primitive Mesh");
		meshObject.SetActive(true);
		meshObject.tag = "Geometry";

		var sceneMeshObject = GameObject.Instantiate(MeshCache[cacheKey]);
		sceneMeshObject.SetActive(true);
		sceneMeshObject.transform.SetParent(meshObject.transform, false);

		return meshObject;
	}
}