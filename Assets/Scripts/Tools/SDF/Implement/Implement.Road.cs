/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;

namespace SDF
{
	namespace Implement
	{
		public class Road
		{
			public static UE.GameObject Generate(in SDF.World.Road road)
			{
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
					splineContainer.Spline.Add(knot, Splines.TangentMode.AutoSmooth);
				}

				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.Linear);
				splineContainer.Spline.SetTangentMode(road.points.Count - 1, Splines.TangentMode.Linear);

				var material = SDF2Unity.Material.Create(road.Name + "_Material");
				
				SDF.Implement.Visual.ApplyMaterial(road.material.script, material);

				var roadGenerator = newRoadObject.AddComponent<Unity.Splines.LoftRoadGenerator>();
				roadGenerator.Material = material;
				roadGenerator.LoftAllRoads();
				foreach ( var width in roadGenerator.Widths)
				{
					UE.Debug.Log(width);
				}
				roadGenerator.Widths.Add(new Splines.SplineData<float>((float)road.width));
				UE.Debug.Log("!!!!!!!!!!!!");
				foreach ( var width in roadGenerator.Widths)
				{
					UE.Debug.Log(width);
				}

				return newRoadObject;
			}
		}
	}
}
