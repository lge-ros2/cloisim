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
#if ENABLE_MESH_CACHE
	private static Dictionary<string, GameObject> MeshCache = new Dictionary<string, GameObject>();
#endif

	public static Texture2D GetTexture(in string textureFullPath)
	{
		if (!string.IsNullOrEmpty(textureFullPath) && File.Exists(textureFullPath))
		{
			if (textureFullPath.EndsWith(".tga"))
			{
				var texture = TextureUtil.LoadTGA(textureFullPath);
				if (texture != null)
				{
					texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
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
						texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
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

	/// <summary>
	/// Pre-load all embedded textures from an Assimp scene.
	/// FBX files can embed textures directly; Assimp references them with "*N" paths.
	/// </summary>
	private static Dictionary<string, Texture2D> LoadEmbeddedTextures(Assimp.Scene scene)
	{
		var textures = new Dictionary<string, Texture2D>();

		if (scene == null || !scene.HasTextures)
			return textures;

		for (var i = 0; i < scene.TextureCount; i++)
		{
			var embeddedTex = scene.Textures[i];
			if (embeddedTex.IsCompressed)
			{
				var texture = new Texture2D(2, 2);
				if (texture.LoadImage(embeddedTex.CompressedData))
				{
					texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
					textures[$"*{i}"] = texture;
				}
				else
					Debug.LogWarning($"Failed to load embedded texture at index {i}");
			}
		}

		return textures;
	}

	private static Texture2D TryLoadTexture(
		string filePath,
		IEnumerable<string> textureDirectories,
		Dictionary<string, Texture2D> embeddedTextures = null)
	{
		if (string.IsNullOrEmpty(filePath))
			return null;

		// Check pre-loaded embedded textures first (Assimp "*N" convention)
		if (embeddedTextures != null && embeddedTextures.TryGetValue(filePath, out var embeddedTex))
			return embeddedTex;

		if (filePath.Contains("model://"))
			filePath = filePath.Replace("model://", "");

		// Normalize path separators (Windows backslashes from cross-platform FBX files)
		filePath = filePath.Replace('\\', '/');

		// Strip Blender's "//" relative path prefix
		if (filePath.StartsWith("//"))
			filePath = filePath.Substring(2);

		// Strip leading slash from absolute paths so Path.Combine works with search directories
		if (Path.IsPathRooted(filePath))
			filePath = Path.GetFileName(filePath);

		// Try the (cleaned) path against all search directories
		foreach (var dir in textureDirectories)
		{
			var fullPath = Path.Combine(dir, filePath);
			if (File.Exists(fullPath))
				return GetTexture(fullPath);
		}

		// If the path contained subdirectories and didn't match, try just the filename
		var fileName = Path.GetFileName(filePath);
		if (fileName != filePath)
		{
			foreach (var dir in textureDirectories)
			{
				var fullPath = Path.Combine(dir, fileName);
				if (File.Exists(fullPath))
					return GetTexture(fullPath);
			}
		}

		Debug.LogWarning($"Texture not found: {filePath}");
		return null;
	}

	private static List<Material> ToUnity(this List<Assimp.Material> sceneMaterials, in string meshPath, Assimp.Scene scene = null)
	{
		var parentPath = Directory.GetParent(meshPath).FullName;
		var textureDirectories = GetRootTexturePaths(parentPath);
		var embeddedTextures = LoadEmbeddedTextures(scene);
		var materials = new List<Material>();
		var logs = new StringBuilder();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = SDF2Unity.CreateMaterial(sceneMat.Name);

			if (sceneMat.HasColorDiffuse)
			{
				// Force alpha to 1.0 when setting diffuse color.
				// Blender FBX exports may store Principled BSDF Alpha in ColorDiffuse.W,
				// which incorrectly triggers transparent mode for opaque materials.
				// Actual transparency is handled by the HasOpacity check below.
				var diffuseColor = sceneMat.ColorDiffuse.ToUnity();
				diffuseColor.a = 1.0f;
				mat.SetBaseColor(diffuseColor);
				// logs.AppendLine($"HasColorDiffuse({sceneMat.ColorDiffuse.ToUnity()}) for {sceneMat.Name}");
			}

			// Blender FBX exporter stores Principled BSDF Alpha as Opacity (separate float).
			// Only apply transparency when the explicit Opacity property indicates it.
			if (sceneMat.HasOpacity && sceneMat.Opacity < 1.0f)
			{
				var baseColor = mat.GetColor("_BaseColor");
				baseColor.a = sceneMat.Opacity;
				mat.SetBaseColor(baseColor);
				logs.AppendLine($"HasOpacity({sceneMat.Opacity}) applied transparency for {sceneMat.Name}");
			}

			if (sceneMat.HasColorEmissive)
			{
				mat.SetEmission(sceneMat.ColorEmissive.ToUnity());
				// logs.AppendLine($"HasColorEmissive({sceneMat.ColorEmissive.ToUnity()}) for {sceneMat.Name}");
			}

			if (sceneMat.HasColorSpecular)
			{
				mat.SetSpecular(sceneMat.ColorSpecular.ToUnity());
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
				// Blender FBX exports Principled BSDF Roughness as Shininess.
				// Unity uses Smoothness (inverse of Roughness): Smoothness = 1 - Roughness
				var smoothness = Mathf.Clamp01(1.0f - sceneMat.Shininess);
				mat.SetFloat("_Smoothness", smoothness);
				logs.AppendLine($"HasShininess({sceneMat.Shininess}) -> Smoothness({smoothness}) for {sceneMat.Name}");
			}

			if (sceneMat.HasReflectivity)
			{
				var smoothness = Mathf.Clamp01(1.0f - (float)sceneMat.Reflectivity);
				mat.SetFloat("_Smoothness", smoothness);
				logs.AppendLine($"HasReflectivity({sceneMat.Reflectivity}) -> Smoothness({smoothness}) for {sceneMat.Name}");
			}

			if (sceneMat.HasTextureDiffuse)
			{
				var tex = TryLoadTexture(sceneMat.TextureDiffuse.FilePath, textureDirectories, embeddedTextures);
				if (tex != null)
					mat.SetTexture("_BaseMap", tex);
			}

			if (sceneMat.HasTextureNormal)
			{
				var tex = TryLoadTexture(sceneMat.TextureNormal.FilePath, textureDirectories, embeddedTextures);
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
				var tex = TryLoadTexture(sceneMat.TextureSpecular.FilePath, textureDirectories, embeddedTextures);
				if (tex != null)
				{
					mat.SetTexture("_SpecGlossMap", tex);
					mat.EnableKeyword("_SPECGLOSSMAP");
				}
			}

			if (sceneMat.HasTextureEmissive)
			{
				var tex = TryLoadTexture(sceneMat.TextureEmissive.FilePath, textureDirectories, embeddedTextures);
				if (tex != null)
				{
					mat.SetTexture("_EmissionMap", tex);
					mat.EnableKeyword("_EMISSION");
				}
			}

			if (sceneMat.HasTextureOpacity)
			{
				// Blender FBX often exports TextureOpacity even for fully opaque materials.
				// Transparency is already handled via the HasOpacity scalar check above,
				// so we only log this for diagnostics and do NOT enable transparent mode here.
				logs.AppendLine($"HasTextureOpacity({sceneMat.TextureOpacity.FilePath}) ignored for {sceneMat.Name} (transparency handled via HasOpacity)");
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
		{
			logs.Insert(0, "MeshLoader.LoadMaterials() - Implementation logs\n");
			Debug.LogWarning(logs.ToString());
		}

		return materials;
	}

	private static MeshMaterialList ToUnity(this IReadOnlyList<Assimp.Mesh> sceneMeshes)
	{
		var meshMatList = new MeshMaterialList();

		foreach (var sceneMesh in sceneMeshes)
		{
			var newMesh = new Mesh();
			newMesh.name = sceneMesh.Name;
			// Debug.Log(newMesh.name + ": " + sceneMesh.VertexCount + " vertices, " + sceneMesh.FaceCount + " faces, " + sceneMesh.MaterialIndex + " material index");

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
			}

			// Debug.Log("Done - " + sceneMesh.Name + ", " + newMesh.vertexCount + " : " + sceneMesh.MaterialIndex + ", " + newMesh.bindposes.LongLength);
			meshMatList.Add(new MeshMaterial(newMesh, sceneMesh.MaterialIndex));
		}

		return meshMatList;
	}

	private static GameObject ToUnityMeshObject(
		this Assimp.Node node,
		in MeshMaterialList meshMatList,
		in Dictionary<string, Assimp.Light> lightMap = null)
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
				// Debug.Log($"[MeshAssign] Node='{node.Name}' MeshIndex={index} Mesh='{meshMat.mesh.name}' MatIdx={meshMat.materialIndex} Mat='{meshMat.material?.name}'");
			}
		}

		// Set Light
		if (lightMap != null && lightMap.ContainsKey(node.Name))
		{
			lightMap[node.Name].ApplyLightToNode(nodeObject);
		}

		// Convert Assimp transform into Unity transform
		// Use proper decomposition that preserves negative scale (reflection)
		var nodeTransformMatrix = node.Transform.ToUnity();
		nodeTransformMatrix.DecomposeTransformMatrix(out var localPos, out var localRot, out var localScale);
		nodeObject.transform.localPosition = localPos;
		nodeObject.transform.localRotation = localRot;
		nodeObject.transform.localScale = localScale;

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				var hasLight = (lightMap != null && lightMap.ContainsKey(child.Name));
				if (!hasLight && child.ChildrenCount() == 0)
				{
					continue;
				}

				// Debug.Log(" => Child Object: " + child.Name + " " + child.Transform);
				var childObject = child.ToUnityMeshObject(meshMatList, lightMap);
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
					materials = scene.Materials.ToUnity(meshPath, scene);
				}

				meshMatList = scene.Meshes.ToUnity();
				meshMatList.SetMaterials(materials);
			}

			// Build light map from scene
			var lightMap = scene.HasLights ? scene.BuildLightMap() : null;

			// Create GameObjects from nodes (including lights)
			var createdMeshObject = scene.RootNode.ToUnityMeshObject(meshMatList, lightMap);
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