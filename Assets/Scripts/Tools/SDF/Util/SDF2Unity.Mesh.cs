/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public partial class SDF2Unity
{
	class MeshMaterialSet
	{
		private readonly string _meshName;
		private readonly Mesh _mesh;
		private readonly Material _material;

		public MeshMaterialSet(in string meshName, in Mesh mesh, in Material material)
		{
			_meshName = meshName;
			_mesh = mesh;
			_material = material;
		}

		public Mesh Mesh => _mesh;
		public Material Material => _material;
		public string MeshName => _meshName;
	}

	private static readonly Assimp.AssimpContext importer = new Assimp.AssimpContext();

	private static readonly Assimp.PostProcessSteps postProcessFlags =
				Assimp.PostProcessSteps.OptimizeGraph |
				Assimp.PostProcessSteps.OptimizeMeshes |
				Assimp.PostProcessSteps.SortByPrimitiveType |
				Assimp.PostProcessSteps.RemoveRedundantMaterials |
				Assimp.PostProcessSteps.ImproveCacheLocality |
				// Assimp.PostProcessSteps.SplitLargeMeshes |
				// Assimp.PostProcessSteps.GenerateSmoothNormals |
				Assimp.PostProcessSteps.Triangulate |
				Assimp.PostProcessSteps.MakeLeftHanded;

	private static List<Material> LoadMaterials(in string parentPath, in List<Assimp.Material> sceneMaterials)
	{
		var materials = new List<Material>();

		foreach (var sceneMat in sceneMaterials)
		{
			var mat = new Material(commonShader);

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
				texturePaths.Add(Path.Combine(parentPath, filePath));
				texturePaths.Add(Path.Combine(parentPath, "../", filePath));
				texturePaths.Add(Path.Combine(parentPath, "../materials/", filePath));
				texturePaths.Add(Path.Combine(parentPath, "../materials/textures/", filePath));

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

	private static List<MeshMaterialSet> LoadMeshes(in List<Assimp.Mesh> sceneMeshes, in List<Material> materials)
	{
		if (materials == null)
		{
			Debug.LogWarning("material list is empty");
			return null;
		}

		var meshMats = new List<MeshMaterialSet>();

		foreach (var sceneMesh in sceneMeshes)
		{
			var vertices = new Queue<Vector3>();
			var normals = new Queue<Vector3>();
			var uvs = new Queue<Vector2>();
			var indices = new Queue<int>();

			// Vertices
			if (sceneMesh.HasVertices)
			{
				foreach (var v in sceneMesh.Vertices)
				{
					vertices.Enqueue(new Vector3(v.X, v.Y, v.Z));
				}
			}

			// Normals
			if (sceneMesh.HasNormals)
			{
				foreach (var n in sceneMesh.Normals)
				{
					normals.Enqueue(new Vector3(n.X, n.Y, n.Z));
				}
			}

			// Triangles
			if (sceneMesh.HasFaces)
			{
				foreach (var face in sceneMesh.Faces)
				{
					if (face.IndexCount == 3)
					{
						indices.Enqueue(face.Indices[2]);
						indices.Enqueue(face.Indices[1]);
						indices.Enqueue(face.Indices[0]);
					}
				}
			}

			// UV (texture coordinate)
			if (sceneMesh.HasTextureCoords(0))
			{
				foreach (var uv in sceneMesh.TextureCoordinateChannels[0])
				{
					uvs.Enqueue(new Vector2(uv.X, uv.Y));
				}
			}

			var newMesh = new Mesh();

			if (vertices.Count >= UInt16.MaxValue)
			{
				newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
			}
			else
			{
				newMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
			}

			newMesh.vertices = vertices.ToArray();
			newMesh.normals = normals.ToArray();
			newMesh.triangles = indices.ToArray();
			newMesh.uv = uvs.ToArray();
			newMesh.Optimize();

			meshMats.Add(new MeshMaterialSet(sceneMesh.Name, newMesh, materials[sceneMesh.MaterialIndex]));
			// Debug.Log("Done - " + sceneMesh.Name + ", " + vertices.Count + " : " + sceneMesh.MaterialIndex);
		}

		return meshMats;
	}

	private static GameObject ConvertAssimpNodeToGameObject(in Assimp.Node node, in List<MeshMaterialSet> meshMats, in float scaleX = 1, in float scaleY = 1, in float scaleZ = 1)
	{
		var rootObject = new GameObject(node.Name);
		// Debug.Log("RootObject: " + rootObject.name);

		// Set Mesh
		if (node.HasMeshes)
		{
			foreach (var index in node.MeshIndices)
			{
				var meshMat = meshMats[index];

				var subObject = new GameObject(meshMat.MeshName);
				subObject.AddComponent<MeshFilter>();
				subObject.AddComponent<MeshRenderer>();

				subObject.GetComponent<MeshFilter>().mesh = meshMat.Mesh;
				subObject.GetComponent<MeshRenderer>().material = meshMat.Material;

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
				var childObject = ConvertAssimpNodeToGameObject(child, meshMats, scaleX, scaleY, scaleZ);
				childObject.transform.SetParent(rootObject.transform, false);
			}
		}

		return rootObject;
	}

	public static GameObject LoadMeshObject(in string meshPath, in Vector3 eulerRotation)
	{
		return LoadMeshObject(meshPath, eulerRotation, Vector3.one);
	}

	public static GameObject LoadMeshObject(in string meshPath, in Vector3 eulerRotation, in Vector3 scale)
	{
		if (!File.Exists(meshPath))
		{
			return null;
		}

		var scene = importer.ImportFile(meshPath, postProcessFlags);
		if (scene == null)
		{
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
			meshMats = LoadMeshes(scene.Meshes, materials);
		}

		// Create GameObjects from nodes
		var nodeObject = ConvertAssimpNodeToGameObject(scene.RootNode, meshMats, scale.x, scale.y, scale.z);

		// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
		var meshRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, eulerRotation.z);
		foreach (var meshFilter in nodeObject.GetComponentsInChildren<MeshFilter>())
		{
			var sharedMesh = meshFilter.sharedMesh;

			var vertices = sharedMesh.vertices;
			for (var index = 0; index < sharedMesh.vertexCount; index++)
			{
				vertices[index] = meshRotation * vertices[index];
			}

			sharedMesh.vertices = vertices;
		}

		return nodeObject;
	}
}