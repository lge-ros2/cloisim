/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;
using System.Linq;
using System.Collections.Generic;

namespace SDFormat
{
	namespace Implement
	{
		public class Road
		{
			public static UE.GameObject Generate(in SDFormat.Element roadElement)
			{
				if (roadElement == null)
				{
					return null;
				}

				var roadName = roadElement.GetAttribute<string>("name", "road");
				var roadWidth = SDFormat.Extensions.GetElementValue(roadElement, "width", 1.0);

				var newRoadObject = new UE.GameObject();
				newRoadObject.transform.SetParent(Main.RoadsRoot.transform);
				newRoadObject.name = roadName;
				newRoadObject.tag = "Road";

				var splineContainer = newRoadObject.AddComponent<Splines.SplineContainer>();

				var points = new List<SDFormat.Math.Vector3d>();
				foreach (var pointElement in roadElement.GetElements("point"))
				{
					var pointStr = pointElement.Value?.GetAsString();
					if (!string.IsNullOrEmpty(pointStr))
					{
						points.Add(SDFormat.Math.Vector3d.Parse(pointStr));
					}
				}

				if (points.Count == 0)
				{
					return newRoadObject;
				}

				var centerX = points.Average(p => p.X);
				var centerY = points.Average(p => p.Y);
				var centerZ = points.Average(p => p.Z);
				var centerPos = new SDFormat.Math.Vector3d(centerX, centerY, centerZ);

				newRoadObject.transform.localPosition = centerPos.ToUnity();

				foreach (var point in points)
				{
					var offset = new SDFormat.Math.Vector3d(point.X - centerPos.X, point.Y - centerPos.Y, point.Z - centerPos.Z);
					splineContainer.Spline.Add(offset.ToUnity(), Splines.TangentMode.AutoSmooth);
				}

				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.Linear);
				splineContainer.Spline.SetTangentMode(points.Count - 1, Splines.TangentMode.Linear);

				var material = SDF2Unity.CreateMaterial(roadName + "_Material");

				// Try to apply material script from road element
				var materialElement = roadElement.FindElement("material");
				if (materialElement != null)
				{
					var scriptElement = materialElement.FindElement("script");
					if (scriptElement != null)
					{
						var scriptUri = scriptElement.FindElement("uri")?.Value?.GetAsString() ?? string.Empty;
						var scriptName = scriptElement.FindElement("name")?.Value?.GetAsString() ?? string.Empty;
						material = Implement.Material.ApplyScript(scriptUri, scriptName, material);
					}
				}

				var roadGenerator = newRoadObject.AddComponent<global::Unity.Splines.LoftRoadGenerator>();
				roadGenerator.Material = material;
				roadGenerator.Width = (float)roadWidth;
				roadGenerator.LoftAllRoads();

				return newRoadObject;
			}
		}
	}
}
