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
		const string defaultShaderName = "Standard (Specular setup)";
		private static Shader commonShader = Shader.Find(defaultShaderName);

		/// <summary>Set mesh from external source</summary>
		public static void SetMesh(in SDF.Mesh obj, in GameObject targetObject)
		{
			string error = string.Empty;

			// file path
			if (!File.Exists(obj.uri))
			{
				Debug.Log("File doesn't exist.");
			}
			else
			{
				var meshName = Path.GetFileNameWithoutExtension(obj.uri);
				var fileExtension = Path.GetExtension(obj.uri);

				if (fileExtension.ToLower().Equals(".obj"))
				{
					var mtlPath = obj.uri.Replace(fileExtension, ".mtl");
					SDF2Unity.LoadObjMesh(targetObject, obj.uri, mtlPath);
				}
				else if (fileExtension.ToLower().Equals(".stl"))
				{
					SDF2Unity.LoadStlMesh(targetObject, obj.uri);
				}
				else
				{
					Debug.Log("Unknown file extension");
				}

				foreach (var meshFilter in targetObject.GetComponentsInChildren<MeshFilter>())
				{
					var mesh = meshFilter.sharedMesh;

					// Scaling
					Vector3[] vertices = mesh.vertices;
					var scaleFactor = obj.scale;
					for (var v = 0; v < mesh.vertexCount; v++)
					{
						vertices[v].x *= (float)scaleFactor.X;
						vertices[v].y *= (float)scaleFactor.Y;
						vertices[v].z *= (float)scaleFactor.Z;
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
				mesh = ProceduralMesh.CreateBox((float)box.size.X, (float)box.size.Z, (float)box.size.Y);
				mesh.name = "Box";

				// Scaling
				// Vector3[] vertices = mesh.vertices;
				// for (var v = 0; v < mesh.vertexCount; v++)
				// {
				// 	vertices[v].x *= (float)box.size.X;
				// 	vertices[v].y *= (float)box.size.Z;
				// 	vertices[v].z *= (float)box.size.Y;
				// }
				// mesh.vertices = vertices;
			}
			else if (shape is SDF.Sphere)
			{
				var sphere = shape as SDF.Sphere;
				mesh = ProceduralMesh.CreateSphere((float)sphere.radius);
				mesh.name = "Sphere";

				// // Scaling
				// Vector3[] vertices = mesh.vertices;
				// for (var v = 0; v < mesh.vertexCount; v++)
				// {
				// 	vertices[v] *= (float)sphere.radius;
				// }
				// mesh.vertices = vertices;
			}
			else if (shape is SDF.Cylinder)
			{
				var cylinder = shape as SDF.Cylinder;
				mesh = ProceduralMesh.CreateCylinder((float)cylinder.radius, (float)cylinder.length);
				mesh.name = "Cylinder";

				// Scaling
				// Vector3[] vertices = mesh.vertices;
				// for (var v = 0; v < mesh.vertexCount; v++)
				// {
				// 	vertices[v].x *= (float)cylinder.radius;
				// 	vertices[v].y *= (float)cylinder.length;
				// 	vertices[v].z *= (float)cylinder.radius;
				// }
				// mesh.vertices = vertices;
			}
			else if (shape is SDF.Plane)
			{
				var plane = shape as SDF.Plane;

				Vector3 normal = new Vector3((float)plane.normal.X, (float)plane.normal.Z, (float)plane.normal.Y);
				mesh = ProceduralMesh.CreatePlane((float)plane.size.X, (float)plane.size.Y, normal);
				mesh.name = "Plane";

				// Vector3 normal = new Vector3((float)plane.normal.X, (float)plane.normal.Z, (float)plane.normal.Y);
				// Vector3[] normals = mesh.normals;
				// // edit the normals in an external array
				// for (int i = 0; i < normals.Length; i++)
				// {
				// 	normals[i] = normal;
				// }
				// mesh.normals = normals;

				// // Scaling
				// Vector3[] vertices = mesh.vertices;
				// for (var v = 0; v < mesh.vertexCount; v++)
				// {
				// 	vertices[v].x *= (float)plane.size.X;
				// 	vertices[v].z *= (float)plane.size.Y;
				// }
				// mesh.vertices = vertices;
			}
			else
			{
				Debug.Log("Wrong ShapeType!!!");
			}

			if (mesh != null)
			{
				mesh.RecalculateTangents();
				mesh.RecalculateBounds();
				mesh.RecalculateNormals();
				mesh.Optimize();

				var meshFilter = targetObject.AddComponent<MeshFilter>();
				meshFilter.mesh = mesh;

				var newMaterial = new Material(commonShader);
				newMaterial.name = mesh.name;

				var meshRenderer = targetObject.AddComponent<MeshRenderer>();
				meshRenderer.material = newMaterial;
			}
		}
	}
}