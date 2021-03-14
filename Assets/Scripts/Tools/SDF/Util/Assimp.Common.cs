/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using System.IO;
using System;
using UnityEngine;

public partial class MeshLoader
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
		private readonly int _materialIndex;
		private readonly Mesh _mesh;
		private Material _material;

		public MeshMaterialSet(in Mesh mesh, in int materialIndex)
		{
			_mesh = mesh;
			_materialIndex = materialIndex;
		}

		public int MaterialIndex => _materialIndex;

		public Material Material
		{
			get => _material;
			set => _material = value;
		}

		public Mesh Mesh => _mesh;
	}

	class MeshMaterialList
	{
		private List<MeshMaterialSet> meshMatList = new List<MeshMaterialSet>();

		public int Count => meshMatList.Count;

		public void Add(in MeshMaterialSet meshMatSet)
		{
			// if (meshMatList.TryGetValue(materialIndex, out var value))
			// {
			// 	value.Add(meshMatSet);
			// }
			// else
			// {
			// 	meshMatList.Add(materialIndex, new List<MeshMaterialSet>(){meshMatSet});
			// }
			meshMatList.Add(meshMatSet);
		}

		public void SetMaterials(in List<Material> materials)
		{
			foreach (var meshMatSet in meshMatList)
			{
				meshMatSet.Material = materials[meshMatSet.MaterialIndex];
				// foreach (var meshMat in meshMatSet.Value)
				// {
				// 	meshMat.Material = materials[meshMatSet.Key];
				// }
			}
		}

		public MeshMaterialSet Get(in int index)
		{
			// return meshMatList[materialIndex][0];
			return meshMatList[index];
		}
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

	private static readonly Assimp.AssimpContext importer = new Assimp.AssimpContext();

	private static readonly Assimp.LogStream logstream = new Assimp.LogStream(
		delegate (String msg, String userData)
		{
			Debug.Log(msg);
		});

	private static Assimp.Scene GetScene(in string targetPath, out Quaternion meshRotation)
	{
		meshRotation = Quaternion.identity;

		if (!File.Exists(targetPath))
		{
			Debug.Log("File doesn't exist: " + targetPath);
			return null;
		}

		// var colladaIgnoreConfig = new Assimp.Configs.ColladaIgnoreUpDirectionConfig(true);
		// importer.SetConfig(colladaIgnoreConfig);

		// logstream.Attach();

		const Assimp.PostProcessSteps postProcessFlags =
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

		var scene = importer.ImportFile(targetPath, postProcessFlags);
		if (scene == null)
		{
			return null;
		}

		var fileExtension = Path.GetExtension(targetPath).ToLower();

		if (!CheckFileSupport(fileExtension))
		{
			Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + targetPath);
			return null;
		}

		// Rotate meshes for Unity world since all 3D object meshes are oriented to right handed coordinates
		var eulerRotation = GetRotationByFileExtension(fileExtension, targetPath);
		meshRotation = Quaternion.Euler(eulerRotation.x, eulerRotation.y, eulerRotation.z);

		return scene;
	}
}