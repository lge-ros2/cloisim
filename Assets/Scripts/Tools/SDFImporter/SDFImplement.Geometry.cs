/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using System.Xml;
using UnityEngine;

public partial class SDFImplement
{
	public class Geometry
	{
		/// <summary>Set mesh from external source</summary>
		public static void SetMesh(in SDF.Mesh obj, in GameObject targetObject)
		{
			// file path
			if (!File.Exists(obj.uri))
			{
				Debug.Log("File doesn't exist. - " + obj.uri);
				return;
			}

			var isFileSupported = true;
			var fileExtension = Path.GetExtension(obj.uri).ToLower();
			var eulerRotation = Vector3.zero;

			switch (fileExtension)
            {
                case ".dae":
                    {
						var xmlDoc = new XmlDocument();
        				xmlDoc.Load(obj.uri);

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
					Debug.LogWarning("Unsupported file extension: " + fileExtension + " -> " + obj.uri);
					isFileSupported = false;
					break;
			}

			if (isFileSupported)
            {
                var loadedObject = SDF2Unity.LoadMeshObject(obj.uri, eulerRotation);
                loadedObject.transform.SetParent(targetObject.transform, false);

                foreach (var meshFilter in targetObject.GetComponentsInChildren<MeshFilter>())
                {
                    var mesh = meshFilter.sharedMesh;

                    // Scaling
                    var vertices = mesh.vertices;
                    var scaleFactor = SDF2Unity.GetScale(obj.scale);
                    for (var v = 0; v < mesh.vertexCount; v++)
                    {
                        vertices[v].x *= scaleFactor.x;
                        vertices[v].y *= scaleFactor.y;
                        vertices[v].z *= scaleFactor.z;
                    }

                    mesh.vertices = vertices;

                    mesh.RecalculateTangents();
                    mesh.RecalculateBounds();
                    mesh.RecalculateNormals();
                    mesh.Optimize();
                }
            }
		}

		//
		// Summary: Set primitive mesh
		//
		public static void SetMesh(in SDF.ShapeType shape, in GameObject targetObject)
		{
			Mesh mesh = null;

			if (shape is SDF.Box)
			{
				var box = shape as SDF.Box;
				var scale = SDF2Unity.GetScale(box.size);
				mesh = ProceduralMesh.CreateBox(scale.x, scale.y, scale.z);
			}
			else if (shape is SDF.Sphere)
			{
				var sphere = shape as SDF.Sphere;
				mesh = ProceduralMesh.CreateSphere((float)sphere.radius);
			}
			else if (shape is SDF.Cylinder)
			{
				var cylinder = shape as SDF.Cylinder;
				mesh = ProceduralMesh.CreateCylinder((float)cylinder.radius, (float)cylinder.length);

			}
			else if (shape is SDF.Plane)
			{
				var plane = shape as SDF.Plane;
				var normal = SDF2Unity.GetNormal(plane.normal);
				mesh = ProceduralMesh.CreatePlane((float)plane.size.X, (float)plane.size.Y, normal);
			}
			else
			{
				Debug.Log("Wrong ShapeType!!!");
			}

			if (mesh != null)
			{
				var meshFilter = targetObject.AddComponent<MeshFilter>();
				meshFilter.mesh = mesh;

				var newMaterial = new Material(SDF2Unity.commonShader);
				newMaterial.name = mesh.name;

				var meshRenderer = targetObject.AddComponent<MeshRenderer>();
				meshRenderer.material = newMaterial;
			}
		}
	}
}