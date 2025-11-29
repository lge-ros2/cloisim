/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
// #define ENABLE_MESH_CACHE

using System.Collections.Generic;
using System.Text;
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
			if (textureFullPath.EndsWith(".tga"))
			{
				var texture = TextureUtil.LoadTGA(textureFullPath);
				if (texture != null)
				{
					return texture;
				}
				else
				{
					Debug.LogWarning($"Cannot Load TGA file: {textureFullPath}");
				}
			}
			else
			{
				var byteArray = File.ReadAllBytes(textureFullPath);
				if (byteArray != null && byteArray.Length > 0)
				{
					var texture = new Texture2D(0, 0);
					if (texture.LoadImage(byteArray))
					{
						return texture;
					}
					else
					{
						Debug.LogWarning($"Cannot load texture file: {textureFullPath}");
					}
				}
			}
		}

		return null;
	}

	private static Texture2D TryLoadTexture(string filePath, IEnumerable<string> textureDirectories)
	{
		if (string.IsNullOrEmpty(filePath))
			return null;

		if (filePath.Contains("model://"))
			filePath = filePath.Replace("model://", "");

		foreach (var dir in textureDirectories)
		{
			var fullPath = Path.Combine(dir, filePath);
			if (File.Exists(fullPath))
				return GetTexture(fullPath);
		}

		return null;
	}

	private static List<Material> ToUnity(this List<Assimp.Material> sceneMaterials, in string meshPath)
	{
		var parentPath = Directory.GetParent(meshPath).FullName;
		var textureDirectories = GetRootTexturePaths(parentPath);
		var materials = new List<Material>();
		var logs = new StringBuilder();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = SDF2Unity.Material.Create(sceneMat.Name);

			if (sceneMat.HasColorDiffuse)
			{
				SDF2Unity.Material.SetBaseColor(mat, sceneMat.ColorDiffuse.ToUnity());
				// logs.AppendLine($"HasColorDiffuse({sceneMat.ColorDiffuse.ToUnity()}) for {sceneMat.Name}");
			}

			if (sceneMat.HasColorEmissive)
			{
				SDF2Unity.Material.SetEmission(mat, sceneMat.ColorEmissive.ToUnity());
				// logs.AppendLine($"HasColorEmissive({sceneMat.ColorEmissive.ToUnity()}) for {sceneMat.Name}");
			}

			if (sceneMat.HasColorSpecular)
			{
				SDF2Unity.Material.SetSpecular(mat, sceneMat.ColorSpecular.ToUnity());
				// logs.AppendLine($"HasColorSpecular({sceneMat.ColorSpecular.ToUnity()}) for {sceneMat.Name}");
			}

			if (sceneMat.HasColorTransparent)
			{
#if false
				var baseColor = mat.GetColor("_BaseColor");
				baseColor.a = 1f - sceneMat.ColorTransparent.W;
				mat.SetColor("_BaseColor", baseColor);
				mat.SetFloat("_Surface", 1f);
				mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
				mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

				logs.AppendLine($"HasColorTransparent({sceneMat.ColorTransparent.ToUnity()}) experimentally support for {sceneMat.Name}");
#else
				logs.AppendLine($"HasColorTransparent({sceneMat.ColorSpecular.ToUnity()}) not support for {sceneMat.Name}");
#endif
			}

			if (sceneMat.HasShininess)
			{
				mat.SetFloat("_Smoothness", sceneMat.Shininess);
				logs.AppendLine($"HasShinines({sceneMat.Shininess}) for {sceneMat.Name}");
			}

			if (sceneMat.HasReflectivity)
			{
				mat.SetFloat("_Smoothness", Mathf.Clamp01((float)sceneMat.Reflectivity));
				logs.AppendLine($"HasReflectivity({sceneMat.Reflectivity}) for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureDiffuse)
			{
				var tex = TryLoadTexture(sceneMat.TextureDiffuse.FilePath, textureDirectories);
				if (tex != null)
					mat.SetTexture("_BaseMap", tex);
			}

			if (sceneMat.HasTextureNormal)
			{
				var tex = TryLoadTexture(sceneMat.TextureNormal.FilePath, textureDirectories);
				if (tex != null)
				{
					mat.SetTexture("_BumpMap", tex);
					mat.EnableKeyword("_NORMALMAP");
				}
				logs.AppendLine($"HasTextureNormal({sceneMat.TextureNormal.FilePath}) for {sceneMat.Name}");
			}

			if (sceneMat.HasBumpScaling)
			{
				mat.SetFloat("_BumpScale", sceneMat.BumpScaling);
				logs.AppendLine($"HasBumpScaling({sceneMat.BumpScaling}) for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureSpecular)
			{
				var tex = TryLoadTexture(sceneMat.TextureSpecular.FilePath, textureDirectories);
				if (tex != null)
				{
					mat.SetTexture("_SpecGlossMap", tex);
					mat.EnableKeyword("_SPECGLOSSMAP");
				}
			}

			if (sceneMat.HasTextureEmissive)
			{
				var tex = TryLoadTexture(sceneMat.TextureEmissive.FilePath, textureDirectories);
				if (tex != null)
				{
					mat.SetTexture("_EmissionMap", tex);
					mat.EnableKeyword("_EMISSION");
				}
			}

			if (sceneMat.HasTextureOpacity)
			{
				var tex = TryLoadTexture(sceneMat.TextureOpacity.FilePath, textureDirectories);
				if (tex != null)
				{
					mat.SetTexture("_BaseMap", tex);
					mat.SetFloat("_Surface", 1f);
					mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
					mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
				}
			}

#if UNITY_EDITOR
			if (sceneMat.HasColorAmbient)
			{
				logs.AppendLine($"HasColorAmbient({sceneMat.ColorAmbient.ToUnity()}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasColorReflective)
			{
				logs.AppendLine($"HasColorReflective({sceneMat.ColorReflective.ToUnity()}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureAmbient)
			{
				logs.AppendLine($"HasTextureAmbient({sceneMat.TextureAmbient.FilePath}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureDisplacement)
			{
				logs.AppendLine($"HasTextureDisplacement({sceneMat.TextureDisplacement.FilePath}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureHeight)
			{
				logs.AppendLine($"HasTextureHeight({sceneMat.TextureHeight.FilePath}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureReflection)
			{
				logs.AppendLine($"HasTextureReflection({sceneMat.TextureReflection.FilePath}) but not support for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureLightMap)
			{
				logs.AppendLine($"HasTextureLightMap({sceneMat.TextureLightMap.FilePath}) but not support for {sceneMat.Name}");
			}
#endif
			materials.Add(mat);
		}

		if (logs.Length > 0)
			Debug.LogWarning("LoadMaterials() - Implementation logs\n" + logs.ToString());

		return materials;
	}

	private static MeshMaterialList ToUnity(this IReadOnlyList<Assimp.Mesh> sceneMeshes)
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
					vertices.Enqueue(new Vector3(v.X, v.Y, v.Z));

				newMesh.vertices = vertices.ToArray();
			}

			// UV (texture coordinate)
			for (var channelIndex = 0; channelIndex < sceneMesh.TextureCoordinateChannelCount; channelIndex++)
			{
				if (sceneMesh.HasTextureCoords(channelIndex))
				{
					var uvs = new Queue<Vector2>();
					foreach (var uv in sceneMesh.TextureCoordinateChannels[channelIndex])
						uvs.Enqueue(new Vector2(uv.X, uv.Y));

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
					normals.Enqueue(new Vector3(n.X, n.Y, n.Z));

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

				newMesh.RecalculateNormals();
			}

			// Debug.Log("Done - " + sceneMesh.Name + ", " + newMesh.vertexCount + " : " + sceneMesh.MaterialIndex + ", " + newMesh.bindposes.LongLength);
			meshMatList.Add(new MeshMaterial(newMesh, sceneMesh.MaterialIndex));
		}

		return meshMatList;
	}

	private static GameObject ToUnityMeshObject(
		this Assimp.Node node,
		in MeshMaterialList meshMatList)
	{
		var nodeObject = new GameObject(node.Name);
		// Debug.Log($"ToUnityMeshObject : {node.Name} {node.Transform}");

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

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				if (child.ChildrenCount() == 0)
				{
					continue;
				}

				// Debug.Log(" => Child Object: " + child.Name + " " + child.Transform);
				var childObject = child.ToUnityMeshObject(meshMatList);
				childObject.transform.SetParent(nodeObject.transform, false);
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

#if !ENABLE_MESH_CACHE
		GameObject sceneMeshObject;
#else
		if (MeshCache.ContainsKey(cacheKey))
		{
			Debug.Log($"Use cached mesh({cacheKey}) for {meshPath}");
		}
		else
#endif
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
					materials = scene.Materials.ToUnity(meshPath);
				}

				meshMatList = scene.Meshes.ToUnity();
				meshMatList.SetMaterials(materials);
			}

			// Create GameObjects from nodes
			var createdMeshObject = scene.RootNode.ToUnityMeshObject(meshMatList);
			// Debug.Log(createdMeshObject.name + ": " + createdMeshObject.transform.localRotation.eulerAngles);

			createdMeshObject.SetActive(false);
#if ENABLE_MESH_CACHE
			GameObject.DontDestroyOnLoad(createdMeshObject);
			MeshCache.Add(cacheKey, createdMeshObject);
#else
 			sceneMeshObject = createdMeshObject;
#endif
		}

		meshObject = new GameObject("Non-Primitive Mesh");
		meshObject.SetActive(true);
		meshObject.tag = "Geometry";

#if ENABLE_MESH_CACHE
		var sceneMeshObject = GameObject.Instantiate(MeshCache[cacheKey]);
#endif
		sceneMeshObject.SetActive(true);
		sceneMeshObject.transform.SetParent(meshObject.transform, false);

		return meshObject;
	}
}