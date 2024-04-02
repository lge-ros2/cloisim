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

				newRoadObject.transform.localPosition = SDF2Unity.GetPosition(centerPosOfRoad);

				foreach (var point in road.points)
				{
					var offset = point - centerPosOfRoad;
					var knotPos = SDF2Unity.GetPosition(offset);
					var knot = new Splines.BezierKnot();
					knot.Position = knotPos;
					splineContainer.Spline.Add(knot, Splines.TangentMode.AutoSmooth);
				}

				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.Linear);
				splineContainer.Spline.SetTangentMode(road.points.Count - 1, Splines.TangentMode.Linear);

				var material = SDF2Unity.Material.Create(road.Name + "_Material");

				SDF.Implement.Visual.ApplyMaterial(road.material.script, material);

				var roadGenerator = newRoadObject.AddComponent<Unity.Splines.LoftRoadGenerator>();
				roadGenerator.Material = material;
				roadGenerator.LoftAllRoads();
				roadGenerator.Widths.Add(new Splines.SplineData<float>((float)road.width));

				return newRoadObject;
			}
		}
	}
}
