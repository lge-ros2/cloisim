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
		private readonly Mesh _mesh;
		private readonly int _materialIndex;
		private Material _material;

		public MeshMaterialSet(in Mesh mesh, in int materialIndex)
		{
			_mesh = mesh;
			_materialIndex = materialIndex;
		}

		public void SetMaterial(in Material material)
		{
			_material = material;
		}

		public int MaterialIndex => _materialIndex;
		public Material Material => _material;
		public Mesh Mesh => _mesh;
	}

	private static readonly Assimp.AssimpContext importer = new Assimp.AssimpContext();

	private static readonly Assimp.LogStream logstream = new Assimp.LogStream(
		delegate (String msg, String userData)
		{
			Debug.Log(msg);
		});

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
}