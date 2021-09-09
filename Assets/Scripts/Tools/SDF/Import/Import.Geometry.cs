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

				UE.GameObject geometryObject = null;

				if (t != null && t.Equals(typeof(SDF.Mesh)))
				{
					var mesh = shape as SDF.Mesh;
					geometryObject = Implement.Geometry.GenerateMeshObject(mesh, targetObject.CompareTag("Visual"));
				}
				else if (t != null && typeof(SDF.ShapeType).IsAssignableFrom(t))
				{
					geometryObject = Implement.Geometry.GenerateMeshObject(shape);
				}
				else
				{
					UE.Debug.LogErrorFormat("[{0}] Not support type({1}) for geometry", geometry.Name, geometry.Type);
					return;
				}

				if (geometryObject != null)
				{
					geometryObject.transform.SetParent(targetObject.transform, false);
				}

				targetObject.SetActive(true);
			}
		}
	}
}