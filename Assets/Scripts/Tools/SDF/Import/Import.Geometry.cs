/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportGeometry(in SDF.Geometry geometry, in System.Object parentObject)
			{
				if (geometry == null || geometry.IsEmpty)
				{
					return;
				}

				var targetObject = (parentObject as UE.GameObject);
				var shape = geometry.GetShape();
				var t = shape.GetType();

				if (t != null && t.Equals(typeof(SDF.Mesh)))
				{
					var mesh = shape as SDF.Mesh;
					targetObject.GenerateMesh(mesh);
				}
				else if (t != null && t.Equals(typeof(SDF.Heightmap)))
				{
					var heightmap = shape as SDF.Heightmap;
					targetObject.GenerateMesh(heightmap);
				}
				else if (t != null && typeof(SDF.ShapeType).IsAssignableFrom(t))
				{
					targetObject.GenerateMesh(shape);
				}
				else
				{
					UE.Debug.LogErrorFormat("[{0}] Not support type({1}) for geometry", geometry.Name, geometry.Type);
					return;
				}

				targetObject.SetActive(true);
			}
		}
	}
}