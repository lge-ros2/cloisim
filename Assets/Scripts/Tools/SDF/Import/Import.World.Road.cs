/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;
using System.Collections.Generic;
using System.IO;
using System;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private void ImportRoad(in World.Road road)
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
					splineContainer.Spline.Add(knot, Splines.TangentMode.Continuous);
				}
				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.AutoSmooth);

				var material = SDF2Unity.GetNewMaterial(road.Name + "_Material");

				SDF.Implement.Visual.ApplyMaterial(road.material.script, material);

				var roadGenerator = newRoadObject.AddComponent<Unity.Splines.LoftRoadGenerator>();
				roadGenerator.Material = material;
				roadGenerator.LoftAllRoads();
				roadGenerator.Widths.Add(new Splines.SplineData<float>((float)road.width));

				// UE.Debug.Log("AfterImportModel: " + model.OriginalName + ", " + modelObject.name);
				SegmentationManager.AttachTag(newRoadObject.name, newRoadObject);
				Main.SegmentationManager.UpdateTags();
			}

			private void ImportRoads(IReadOnlyList<World.Road> items)
			{
				foreach (var item in items)
				{
					ImportRoad(item);
				}
			}
		}
	}
}