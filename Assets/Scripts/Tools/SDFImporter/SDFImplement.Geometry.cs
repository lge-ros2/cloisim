/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
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
				Debug.Log("File doesn't exist.");
				return;
			}

			// var meshName = Path.GetFileNameWithoutExtension(obj.uri);
			var fileExtension = Path.GetExtension(obj.uri).ToLower();

			switch (fileExtension)
			{
				case ".obj":
					var mtlPath = obj.uri.Replace(fileExtension, ".mtl");
					SDF2Unity.LoadObjMesh(targetObject, obj.uri, mtlPath);
					break;

				case ".stl":
					SDF2Unity.LoadStlMesh(targetObject, obj.uri);
					break;

				default:
					Debug.LogWarning("Unknown file extension: " + fileExtension);
					break;
			}

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