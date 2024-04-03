/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

// using UnityEngine;
// using System;
// using UnityEngine.Rendering;
// using UENet = UnityEngine.Networking;
// using UnityEngine;
// using UnityEngine.Networking;
// using System.Collections;

// using UnityEngine;
// using System.Collections;
// using UnityEngine.Networking;


namespace SDF
{
	namespace Implement
	{
		public class Geometry
		{
			private static bool IsVisualObject(in UE.GameObject target)
			{
				return target.CompareTag("Visual");
			}

			/// <summary>Set mesh from external source</summary>
			public static void GenerateMeshObject(in SDF.Mesh obj, in UE.GameObject targetParentObject)
			{
				var loadedObject = MeshLoader.CreateMeshObject(obj.uri, obj.submesh_name);
				var isVisualMesh = IsVisualObject(targetParentObject);

				if (loadedObject == null)
				{
					Debug.LogWarning("Cannot load mesh: " + obj.uri);
				}
				else
				{
					loadedObject.transform.localScale = SDF2Unity.GetScale(obj.scale);
				}

				loadedObject.transform.SetParent(targetParentObject.transform, false);
			}

			public static void GenerateMeshObject(in SDF.Heightmap obj, in UE.GameObject targetParentObject)
			{
				var heightmapObject = new UE.GameObject("Heightmap");
				var isVisualMesh = IsVisualObject(targetParentObject);

				heightmapObject.transform.SetParent(targetParentObject.transform, false);

				ProceduralHeightmap.Generate(obj, heightmapObject, isVisualMesh);

				heightmapObject.transform.localPosition = SDF2Unity.Position(obj.pos);
			}

			//
			// Summary: Set primitive mesh
			//
			public static void GenerateMeshObject(in SDF.ShapeType shape, in UE.GameObject targetParentObject)
			{
				var createdObject = new UE.GameObject("Primitive Mesh");
				createdObject.tag = "Geometry";

				UE.Mesh mesh = null;

				if (shape is SDF.Box)
				{
					var box = shape as SDF.Box;
					var scale = SDF2Unity.GetScale(box.size);
					mesh = ProceduralMesh.CreateBox(scale.x, scale.y, scale.z);

					var boxCollider = createdObject.AddComponent<UE.BoxCollider>();
					boxCollider.size = scale;
				}
				else if (shape is SDF.Sphere)
				{
					var sphere = shape as SDF.Sphere;
					mesh = ProceduralMesh.CreateSphere((float)sphere.radius);

					var sphereCollider = createdObject.AddComponent<UE.SphereCollider>();
					sphereCollider.radius = (float)sphere.radius;
				}
				else if (shape is SDF.Capsule)
				{
					var capsule = shape as SDF.Capsule;
					mesh = ProceduralMesh.CreateCapsule((float)capsule.radius, (float)capsule.length);

					var capsuleCollider = createdObject.AddComponent<UE.CapsuleCollider>();
					capsuleCollider.radius = (float)capsule.radius;
					capsuleCollider.height = (float)capsule.length;
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
					mesh.RecalculateNormals();
					mesh.RecalculateTangents();
					mesh.RecalculateBounds();
					mesh.RecalculateUVDistributionMetrics();
					mesh.Optimize();

					var meshFilter = createdObject.AddComponent<UE.MeshFilter>();
					meshFilter.sharedMesh = mesh;

					var meshRenderer = createdObject.AddComponent<UE.MeshRenderer>();
					meshRenderer.sharedMaterial = SDF2Unity.Material.Create(mesh.name);
					meshRenderer.allowOcclusionWhenDynamic = true;
				}

				createdObject.transform.SetParent(targetParentObject.transform, false);
			}
		}
	}
}