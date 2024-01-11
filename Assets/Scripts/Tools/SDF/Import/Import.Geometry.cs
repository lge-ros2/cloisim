/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
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
				var t = geometry.GetShapeType();
				var shape = geometry.GetShape();

				if (t != null && t.Equals(typeof(SDF.Mesh)))
				{
					var mesh = shape as SDF.Mesh;
					Implement.Geometry.GenerateMeshObject(mesh, targetObject);
				}
				else if (t != null && t.Equals(typeof(SDF.Heightmap)))
				{
					var heightmap = shape as SDF.Heightmap;
					Implement.Geometry.GenerateMeshObject(heightmap, targetObject);
				}
				else if (t != null && typeof(SDF.ShapeType).IsAssignableFrom(t))
				{
					Implement.Geometry.GenerateMeshObject(shape, targetObject);
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