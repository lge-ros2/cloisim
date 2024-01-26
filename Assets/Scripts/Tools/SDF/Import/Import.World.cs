/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;
using System.Collections.Generic;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private void ImportRoad(in World.Road road)
			{
				const float UnitDistance = 0.5f;

				// Main.WorldRoot
				var newRoadObject = new UE.GameObject();
				newRoadObject.name = road.Name;
				newRoadObject.tag = "Road";
				newRoadObject.transform.SetParent(Main.RoadsRoot.transform);

				var splineContainer = newRoadObject.AddComponent<Splines.SplineContainer>();

				foreach (var point in road.points)
				{
					var knotPos = SDF2Unity.GetPosition(point);
					var knot = new Splines.BezierKnot();
					knot.Position = knotPos;
					splineContainer.Spline.Add(knot, Splines.TangentMode.Continuous);
				}
				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.AutoSmooth);
				// splineContainer.Spline.SetTangentMode(Splines.TangentMode.Continuous);

				// var texture = UE.Resources.Load<UE.Texture2D>("road1");
				var material = UE.Resources.Load<UE.Material>("RoadMaterial");

				var roadMeshObject = new UE.GameObject("RoadMesh");
				var normal = new UE.Vector3(0, 1, 0);
				var mesh = ProceduralMesh.CreatePlane(UnitDistance, (float)road.width, normal);
				var meshFilter = roadMeshObject.AddComponent<UE.MeshFilter>();
				var meshRenderer = roadMeshObject.AddComponent<UE.MeshRenderer>();
				var meshCollider = roadMeshObject.AddComponent<UE.MeshCollider>();

				meshFilter.sharedMesh = mesh;
				meshCollider.sharedMesh = mesh;
				meshRenderer.material = material;


				var itemsToInstantiate = new Splines.SplineInstantiate.InstantiableItem[1];
				var itemToInstantiate = new Splines.SplineInstantiate.InstantiableItem();
				itemToInstantiate.Prefab = roadMeshObject;
				itemToInstantiate.Probability = 100;
				itemsToInstantiate[0] = itemToInstantiate;


				var splineInstantiate = newRoadObject.AddComponent<Splines.SplineInstantiate>();
				splineInstantiate.Container = splineContainer;
				splineInstantiate.InstantiateMethod = Splines.SplineInstantiate.Method.SpacingDistance;
				splineInstantiate.MinSpacing = UnitDistance;
				splineInstantiate.MaxSpacing = UnitDistance;
				splineInstantiate.itemsToInstantiate = itemsToInstantiate;

				// roadMeshObject.transform.SetParent(newRoadObject.transform);

				UE.GameObject.DontDestroyOnLoad(roadMeshObject);
				// roadMeshObject.hideFlags = UE.HideFlags.HideAndDontSave;
			}

			private void ImportRoads(IReadOnlyList<World.Road> items)
			{
				foreach (var item in items)
				{
					ImportRoad(item);
				}
			}

			protected override System.Object ImportWorld(in World world)
			{
				if (world == null)
				{
					return null;
				}

				// Debug.Log("Import World");
				if (world.gui != null)
				{
					var mainCamera = UnityEngine.Camera.main;
					if (mainCamera != null)
					{
						var cameraPose = world.gui.camera.Pose;
						mainCamera.transform.localPosition = SDF2Unity.GetPosition(cameraPose.Pos);
						mainCamera.transform.localRotation = SDF2Unity.GetRotation(cameraPose.Rot);
					}
				}

				if (world.spherical_coordinates != null)
				{
					var sphericalCoordinatesCore = DeviceHelper.GetGlobalSphericalCoordinates();

					var sphericalCoordinates = world.spherical_coordinates;

					sphericalCoordinatesCore.SetSurfaceType(sphericalCoordinates.surface_model);

					sphericalCoordinatesCore.SetWorldOrientation(sphericalCoordinates.world_frame_orientation);

					sphericalCoordinatesCore.SetCoordinatesReference((float)sphericalCoordinates.latitude_deg, (float)sphericalCoordinates.longitude_deg, (float)sphericalCoordinates.elevation, (float)sphericalCoordinates.heading_deg);
				}

				ImportRoads(world.GetRoads());

				UnityEngine.Physics.gravity = SDF2Unity.GetDirection(world.gravity);

				ImportLights(world.GetLights());

				return Main.WorldRoot;
			}
		}
	}
}