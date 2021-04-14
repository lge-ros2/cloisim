/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	public partial class Implement
	{
		public class Geometry
		{
			/// <summary>Set mesh from external source</summary>
			public static void SetMesh(in SDF.Mesh obj, in UE.GameObject targetObject)
			{
				var loadedObject = MeshLoader.CreateMeshObject(obj.uri);

				if (loadedObject == null)
				{
					Debug.LogError("Cannot load mesh: " + obj.uri);
				}
				else
				{
					loadedObject.name = "geometry(mesh)";
					loadedObject.transform.SetParent(targetObject.transform, false);
					var loadedObjectScale = loadedObject.transform.localScale;
					loadedObjectScale.Scale(SDF2Unity.GetScale(obj.scale));
					loadedObject.transform.localScale = loadedObjectScale;
					// Debug.Log(loadedObject.name + ", " + SDF2Unity.GetScale(obj.scale).ToString("F6") + " => " + loadedObject.transform.localScale.ToString("F6"));
				}
			}

			//
			// Summary: Set primitive mesh
			//
			public static void SetMesh(in SDF.ShapeType shape, in UE.GameObject targetObject)
			{
				UE.Mesh mesh = null;

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
					var meshFilter = targetObject.AddComponent<UE.MeshFilter>();
					meshFilter.mesh = mesh;

					var newMaterial = new UE.Material(SDF2Unity.commonShader);
					newMaterial.name = mesh.name;

					var meshRenderer = targetObject.AddComponent<UE.MeshRenderer>();
					meshRenderer.material = newMaterial;
				}
			}
		}
	}
}