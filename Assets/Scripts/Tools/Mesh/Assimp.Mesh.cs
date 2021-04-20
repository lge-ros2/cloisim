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

public partial class MeshLoader
{
	private static Texture2D GetTexture(in string textureFullPath)
	{
		if (!string.IsNullOrEmpty(textureFullPath))
		{
			var byteArray = File.ReadAllBytes(textureFullPath);
			if (byteArray != null)
			{
				var texture = new Texture2D(2, 2);
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
			var mat = SDF2Unity.GetNewMaterial(sceneMat.Name);

			var colorAmbientAndDiffuse = Color.clear;

			if (sceneMat.HasColorAmbient)
			{
				colorAmbientAndDiffuse += MeshLoader.GetColor(sceneMat.ColorAmbient);
				Debug.Log(sceneMat.Name + ": HasColorAmbient " + MeshLoader.GetColor(sceneMat.ColorAmbient));
			}

			if (sceneMat.HasColorDiffuse)
			{
				colorAmbientAndDiffuse += MeshLoader.GetColor(sceneMat.ColorDiffuse);
				Debug.Log(sceneMat.Name + ": HasColorDiffuse " + MeshLoader.GetColor(sceneMat.ColorDiffuse));
			}

			// mat.SetColor("_BaseColor", colorAmbientAndDiffuse);
			mat.color = colorAmbientAndDiffuse;
			Debug.Log(sceneMat.Name + ": colorAmbientAndDiffuse " + colorAmbientAndDiffuse);

			// Emission
			if (sceneMat.HasColorEmissive)
			{
				mat.SetColor("_EmissionColor", MeshLoader.GetColor(sceneMat.ColorEmissive));
			}

			if (sceneMat.HasColorSpecular)
			{
				mat.SetColor("_SpecColor", MeshLoader.GetColor(sceneMat.ColorSpecular));
			}

			if (sceneMat.HasColorTransparent)
			{
				// Debug.Log(sceneMat.Name + ": HasColorTransparent but not support. " + sceneMat.ColorTransparent);
				mat.SetColor("_TransparentColor", MeshLoader.GetColor(sceneMat.ColorTransparent));
				// Debug.Log("=>" + mat.GetColor("_TransparentColor"));
			}

			// Reflectivity
			if (sceneMat.HasReflectivity)
			{
				if (sceneMat.HasColorReflective)
				{
					// Debug.Log(sceneMat.Name + ": HasColorReflective but not support. " + sceneMat.ColorReflective);
					mat.SetColor("_ReflectColor", MeshLoader.GetColor(sceneMat.ColorReflective));
				}
			}

			if (sceneMat.HasShininess)
			{
				mat.SetFloat("_Glossiness", sceneMat.Shininess);
			}

			// Texture
			if (sceneMat.HasTextureAmbient)
			{
				Debug.Log(sceneMat.Name + ": HasTextureAmbient but not support. " + sceneMat.TextureAmbient.FilePath);
			}

			if (sceneMat.HasTextureDiffuse)
			{
				var filePath = sceneMat.TextureDiffuse.FilePath;

				foreach (var textureDirectory in textureDirectories)
				{
					var textureFullPath = Path.Combine(textureDirectory, filePath);
					if (File.Exists(textureFullPath))
					{
						// mat.SetTexture("_BaseMap", GetTexture(textureFullPath));
						mat.mainTexture = GetTexture(textureFullPath);
					}
				}
			}

			if (sceneMat.HasTextureDisplacement)
			{
				Debug.Log(sceneMat.Name + ": HasTextureDisplacement but not support. " + sceneMat.TextureDisplacement.FilePath);
			}

			if (sceneMat.HasTextureEmissive)
			{
				Debug.Log(sceneMat.Name + ": HasTextureEmissive but not support. " + sceneMat.TextureEmissive.FilePath);
			}

			if (sceneMat.HasTextureHeight)
			{
				Debug.Log(sceneMat.Name + ": HasTextureHeight but not support. " + sceneMat.TextureHeight.FilePath);
			}

			if (sceneMat.HasTextureLightMap)
			{
				Debug.Log(sceneMat.Name + ": HasTextureLightMap but not support. " + sceneMat.TextureLightMap.FilePath);
			}

			if (sceneMat.HasTextureNormal)
			{
				Debug.Log(sceneMat.Name + ": HasTextureNormal but not support. " + sceneMat.TextureNormal.FilePath);
			}

			if (sceneMat.HasTextureOpacity)
			{
				Debug.Log(sceneMat.Name + ": HasTextureOpacity but not support. " + sceneMat.TextureOpacity.FilePath);
			}

			if (sceneMat.HasTextureReflection)
			{
				Debug.Log(sceneMat.Name + ": HasTextureReflection but not support. " + sceneMat.TextureReflection.FilePath);
			}

			if (sceneMat.HasTextureSpecular)
			{
				Debug.Log(sceneMat.Name + ": HasTextureSpecular but not support. " + sceneMat.TextureSpecular.FilePath);
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

			// Debug.Log("Done - " + sceneMesh.Name + ", " + newMesh.vertexCount + " : " + sceneMesh.MaterialIndex + ", " + newMesh.bindposes.LongLength);
			meshMatList.Add(new MeshMaterialSet(newMesh, sceneMesh.MaterialIndex));
		}

		return meshMatList;
	}

	private static GameObject ConvertAssimpNodeToMeshObject(in Assimp.Node node, in MeshMaterialList meshMatList)
	{
		var rootObject = new GameObject(node.Name);

		// Set Mesh
		if (node.HasMeshes)
		{
			foreach (var index in node.MeshIndices)
			{
				var meshMat = meshMatList.Get(index);

				var subObject = new GameObject(meshMat.Mesh.name);
				var meshFilter = subObject.AddComponent<MeshFilter>();
				var meshRenderer = subObject.AddComponent<MeshRenderer>();

				meshFilter.mesh = meshMat.Mesh;
				meshRenderer.material = meshMat.Material;

				subObject.transform.SetParent(rootObject.transform, true);
				// Debug.Log("Sub Object: " + subObject.name);
			}
		}

		// Convert Assimp transfrom into Unity transform
		var nodeTransform = ConvertAssimpMatrix4x4ToUnity(node.Transform);
		rootObject.transform.localPosition = nodeTransform.GetColumn(3);
		rootObject.transform.localRotation = nodeTransform.rotation;
		rootObject.transform.localScale = nodeTransform.lossyScale;
		// Debug.Log("RootObject: " + rootObject.name + "; " + rootObject.transform.localScale.ToString("F7"));

		if (node.HasChildren)
		{
			foreach (var child in node.Children)
			{
				// Debug.Log(" => Child Object: " + child.Name);
				var childObject = ConvertAssimpNodeToMeshObject(child, meshMatList);
				childObject.transform.SetParent(rootObject.transform, false);
			}
		}

		return rootObject;
	}

	public static GameObject CreateMeshObject(in string meshPath)
	{
		var scene = GetScene(meshPath, out var meshRotation);
		if (scene == null)
		{
			return null;
		}

		// Materials
		List<Material> materials = null;
		if (scene.HasMaterials)
		{
			materials = LoadMaterials(meshPath, scene.Materials);
		}

		// Meshes
		MeshMaterialList meshMatList = null;
		if (scene.HasMeshes)
		{
			meshMatList = LoadMeshes(scene.Meshes);
			meshMatList.SetMaterials(materials);
		}

		// Create GameObjects from nodes
		var createdMeshObject = ConvertAssimpNodeToMeshObject(scene.RootNode, meshMatList);
		createdMeshObject.name = "geometry(mesh)";

		// rotate final mesh object
		createdMeshObject.transform.localRotation = meshRotation * createdMeshObject.transform.localRotation;
		// Debug.Log(createdMeshObject.transform.GetChild(0).name + ": " + meshRotation.eulerAngles.ToString("F6") + " =>" + createdMeshObject.transform.localRotation.eulerAngles);

		// change axis of position (y <-> z)
		var existingPosition = createdMeshObject.transform.localPosition;
		createdMeshObject.transform.localPosition = new Vector3(-existingPosition.y, -existingPosition.z, existingPosition.x);
		// Debug.Log(createdMeshObject.transform.GetChild(0).name + ": " + createdMeshObject.transform.localPosition.ToString("F6") + ", " + existingPosition.ToString("F6") );

		// change axis of scale (y <-> z)
		var existingScale = createdMeshObject.transform.localScale;
		createdMeshObject.transform.localScale = new Vector3(existingScale.x, existingScale.z, existingScale.y);

		return createdMeshObject;
	}
}