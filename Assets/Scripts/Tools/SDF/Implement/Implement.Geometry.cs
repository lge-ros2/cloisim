/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDFormat
{
	namespace Implement
	{
		public static class Geometry
		{
			private static bool IsVisualObject(in UE.GameObject target)
			{
				return target.CompareTag("Visual");
			}

			/// <summary>Set mesh from external source</summary>
			public static void GenerateMesh(this UE.GameObject targetParentObject, in Mesh obj)
			{
				var loadedObject = MeshLoader.CreateMeshObject(obj.Uri, obj.Submesh);
				var isVisualMesh = IsVisualObject(targetParentObject);

				if (loadedObject == null)
				{
					Debug.LogWarning($"Cannot load mesh: {obj.Uri}");
				}
				else
				{
					loadedObject.transform.localScale = SDF2Unity.Scale(obj.Scale);
					loadedObject.transform.SetParent(targetParentObject.transform, false);
				}
			}

			public static void GenerateMesh(this UE.GameObject targetParentObject, in Heightmap obj)
			{
				var heightmapObject = new UE.GameObject("Heightmap");
				var isVisualMesh = IsVisualObject(targetParentObject);

				heightmapObject.transform.SetParent(targetParentObject.transform, false);
				heightmapObject.GenerateHeightMap(obj, isVisualMesh);
				heightmapObject.transform.localPosition = obj.Position.ToUnity();
			}

			//
			// Summary: Set primitive mesh from SdFormat geometry
			//
			public static void GenerateMesh(this UE.GameObject targetParentObject, in SDFormat.Geometry geometry)
			{
				var isVisualMesh = IsVisualObject(targetParentObject);
				var createdObject = new UE.GameObject("Primitive Mesh");
				createdObject.tag = "Geometry";

				UE.Mesh mesh = null;

				switch (geometry.Type)
				{
					case GeometryType.Box:
					{
						var scale = SDF2Unity.Scale(geometry.BoxShape.Size);
						mesh = ProceduralMesh.CreateBox(scale.x, scale.y, scale.z);
						break;
					}
					case GeometryType.Sphere:
					{
						var resolution = isVisualMesh ? 30 : 15;
						mesh = ProceduralMesh.CreateSphere((float)geometry.SphereShape.Radius, resolution, resolution);
						break;
					}
					case GeometryType.Capsule:
					{
						mesh = ProceduralMesh.CreateCapsule((float)geometry.CapsuleShape.Radius, (float)geometry.CapsuleShape.Length);
						break;
					}
					case GeometryType.Cylinder:
					{
						mesh = ProceduralMesh.CreateCylinder((float)geometry.CylinderShape.Radius, (float)geometry.CylinderShape.Length, 72);
						break;
					}
					case GeometryType.Plane:
					{
						var normal = geometry.PlaneShape.Normal.ToUnity();
						var size = SDF2Unity.Size(geometry.PlaneShape.Size);
						mesh = ProceduralMesh.CreatePlane(size.x, size.y, normal);
						break;
					}
					case GeometryType.Polyline:
					{
						mesh = ProceduralMesh.CreatePolylines(geometry.PolylineShape);
						break;
					}
					case GeometryType.Ellipsoid:
					{
						var radii = SDF2Unity.Scale(geometry.EllipsoidShape.Radii);
						mesh = ProceduralMesh.CreateSphere(radii);
						break;
					}
					case GeometryType.Cone:
					{
						mesh = ProceduralMesh.CreateCone(0f, (float)geometry.ConeShape.Radius, (float)geometry.ConeShape.Length, 72);
						break;
					}
					case GeometryType.Image:
						Debug.LogWarning("Image geometry type is not supported.");
						break;
					default:
						Debug.Log("Wrong GeometryType!!!");
						break;
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
				}

				createdObject.transform.SetParent(targetParentObject.transform, false);
			}
		}
	}
}