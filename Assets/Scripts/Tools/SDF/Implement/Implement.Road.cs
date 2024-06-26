/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;
using System.Linq;

namespace SDF
{
	namespace Implement
	{
		public class Road
		{
			public static UE.GameObject Generate(in SDF.World.Road road)
			{
				var newRoadObject = new UE.GameObject();
				newRoadObject.transform.SetParent(Main.RoadsRoot.transform);
				newRoadObject.name = road.Name;
				newRoadObject.tag = "Road";

				var splineContainer = newRoadObject.AddComponent<Splines.SplineContainer>();

				var centerPosOfRoad = new Vector3<double>(
										road.points.Average(x => x.X),
										road.points.Average(x => x.Y),
										road.points.Average(x => x.Z));

				newRoadObject.transform.localPosition = SDF2Unity.Position(centerPosOfRoad);

				foreach (var point in road.points)
				{
					var offset = point - centerPosOfRoad;
					splineContainer.Spline.Add(SDF2Unity.Position(offset), Splines.TangentMode.AutoSmooth);
				}

				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.Linear);
				splineContainer.Spline.SetTangentMode(road.points.Count - 1, Splines.TangentMode.Linear);

				var material = SDF2Unity.Material.Create(road.Name + "_Material");

				material = Material.ApplyScript(road.material.script, material);

				var roadGenerator = newRoadObject.AddComponent<Unity.Splines.LoftRoadGenerator>();
				roadGenerator.SdfMaterial = road.material;
				roadGenerator.Material = material;
				roadGenerator.Width = (float)road.width;
				roadGenerator.LoftAllRoads();

				return newRoadObject;
			}
		}
	}
}
