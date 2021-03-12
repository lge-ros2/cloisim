/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System.Xml;
using System;
using UnityEngine;
using UnityEngine.Rendering;

public partial class SDF2Unity
{
	private static List<string> possibleMaterialPaths = new List<string>()
	{
		"",
		"/textures/",
		"../",
		"../materials/", "../materials/textures/",
		"../../materials/", "../../materials/textures/"
	};

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

	private static List<MeshMaterialSet> LoadMeshes(in List<Assimp.Mesh> sceneMeshes, in List<Material> materials)
	{
		return LoadMeshes(sceneMeshes, materials, Quaternion.identity);
	}


	private static List<MeshMaterialSet> LoadMeshes(in List<Assimp.Mesh> sceneMeshes, in List<Material> materials, in Quaternion meshRotation)
	{
		if (materials == null)
		{
			Debug.LogWarning("material list is empty");
			return null;
		}

		var meshMats = new List<MeshMaterialSet>();

		foreach (var sceneMesh in sceneMeshes)
		{
			var newMesh = new Mesh();

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
				var bones = new BoneWeight[newMesh.vertexCount];
				for (var i = 0; i < bones.LongLength; ++i)
				{
					bones[i].boneIndex0 = -1;
					bones[i].boneIndex1 = -1;
					bones[i].boneIndex2 = -1;
					bones[i].boneIndex3 = -1;
				}

				var bindPoses = new List<Matrix4x4>();

				Debug.Log(newMesh.name + ": vertexCount=" + newMesh.vertexCount + " or " + sceneMesh.VertexCount + ", boneCount=" + sceneMesh.BoneCount);

				foreach (var bone in sceneMesh.Bones)
				{
					var bindPose = new Matrix4x4();
					bone.OffsetMatrix.Decompose(out var scaling, out var rotation, out var translation);
					var pos = new Vector3(translation.X, translation.Y, translation.Z);
					var q = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
					var s = new Vector3(scaling.X, scaling.Y, scaling.Z);

					bindPose.SetTRS(pos, q, s);

					bindPoses.Add(bindPose);

					if (bone.HasVertexWeights)
					{
						Debug.Log(bone.Name + " - " + bone.VertexWeightCount);

						boneNameIndexMap.TryGetValue(bone.Name, out var boneIndex);

						foreach (var vertexWeight in bone.VertexWeights)
						{
							// Debug.Log("vertexID=" + vertexWeight.VertexID + ", weight=" + vertexWeight.Weight);
							if (vertexWeight.VertexID >= bones.LongLength)
							{
								Debug.LogWarning("exceeded vertex id = " + vertexWeight.VertexID + ", length=" + bones.LongLength);
							}
							else
							{
								var boneWeight = bones[vertexWeight.VertexID];

								if (boneWeight.boneIndex0 == -1)
								{
									boneWeight.boneIndex0 = boneIndex;
									boneWeight.weight0 = vertexWeight.Weight;
								}
								else if (boneWeight.boneIndex1 == -1)
								{
									boneWeight.boneIndex1 = boneIndex;
									boneWeight.weight1 = vertexWeight.Weight;
								}
								else if (boneWeight.boneIndex2 == -1)
								{
									boneWeight.boneIndex2 = boneIndex;
									boneWeight.weight2 = vertexWeight.Weight;
								}
								else if (boneWeight.boneIndex3 == -1)
								{
									boneWeight.boneIndex3 = boneIndex;
									boneWeight.weight3 = vertexWeight.Weight;
								}
								bones[vertexWeight.VertexID] = boneWeight;
							}
						}
					}
				}

				if (bones.LongLength != newMesh.vertexCount)
				{
					Debug.Log("different!!!, bone count:" + bones.LongLength + ", vertexCount: " + newMesh.vertexCount);
				}
				else
				{
					newMesh.bindposes = bindPoses.ToArray();
					newMesh.boneWeights = bones;
				}
			}

			// newMesh.Optimize();
			// newMesh.RecalculateNormals();
			// newMesh.RecalculateTangents();
			// newMesh.RecalculateBounds();

			meshMats.Add(new MeshMaterialSet(sceneMesh.Name, newMesh, materials[sceneMesh.MaterialIndex]));
			// Debug.Log("Done - " + sceneMesh.Name + ", " + vertices.Count + " : " + sceneMesh.MaterialIndex);
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

				var subObject = new GameObject(meshMat.MeshName);
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

	private static bool CheckFileSupport(in string fileExtension)
	{
		var isFileSupported = true;

		switch (fileExtension)
		{
			case ".dae":
			case ".obj":
			case ".stl":
				break;

			default:
				isFileSupported = false;
				break;
		}

		return isFileSupported;
	}

	private static Vector3 GetRotationByFileExtension(in string fileExtension, in string meshPath)
	{
		var eulerRotation = Vector3.zero;

		switch (fileExtension)
		{
			case ".dae":
				{
					var xmlDoc = new XmlDocument();
					xmlDoc.Load(meshPath);

					var nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
					nsmgr.AddNamespace("ns", xmlDoc.DocumentElement.NamespaceURI);

					var up_axis_node = xmlDoc.SelectSingleNode("/ns:COLLADA/ns:asset/ns:up_axis", nsmgr);
					// var unit_node = xmlDoc.SelectSingleNode("/ns:COLLADA/ns:asset/ns:unit", nsmgr);
					var up_axis = up_axis_node.InnerText.ToUpper();

					// Debug.Log("up_axis: "+ up_axis + ", unit meter: " + unit_node.Attributes["meter"].Value + ", name: " + unit_node.Attributes["name"].Value);
					if (up_axis.Equals("Y_UP"))
					{
						eulerRotation.Set(90f, -90f, 0f);
					}
				}
				break;

			case ".obj":
			case ".stl":
				eulerRotation.Set(90f, -90f, 0f);
				break;

			default:
				break;
		}

		return eulerRotation;
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

		var postProcessFlags =
			Assimp.PostProcessSteps.OptimizeGraph |
			Assimp.PostProcessSteps.OptimizeMeshes |
			Assimp.PostProcessSteps.JoinIdenticalVertices |
			Assimp.PostProcessSteps.SortByPrimitiveType |
			Assimp.PostProcessSteps.RemoveRedundantMaterials |
			Assimp.PostProcessSteps.ImproveCacheLocality |
			Assimp.PostProcessSteps.Triangulate |
			Assimp.PostProcessSteps.MakeLeftHanded;

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

			meshMats = LoadMeshes(scene.Meshes, materials, meshRotation);
		}

		// Create GameObjects from nodes
		var nodeObject = ConvertAssimpNodeToMeshObject(scene.RootNode, meshMats, scale.x, scale.y, scale.z);

		return nodeObject;
	}
}