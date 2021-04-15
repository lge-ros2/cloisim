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
	private static List<Material> LoadMaterials(in string meshPath, in List<Assimp.Material> sceneMaterials)
	{
		var parentPath = Directory.GetParent(meshPath).FullName;
		var materials = new List<Material>();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = new Material(SDF2Unity.commonShader);

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
		// Debug.Log("RootObject: " + rootObject.name);

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
		var existingRotation = createdMeshObject.transform.localRotation.eulerAngles;
		// Debug.Log(createdMeshObject.transform.GetChild(0).name + ": " + meshRotation.eulerAngles.ToString("F6") + ", " + existingRotation.ToString("F6") );
		existingRotation += meshRotation.eulerAngles;

		createdMeshObject.transform.localRotation = Quaternion.Euler(existingRotation);
		return createdMeshObject;
	}
}