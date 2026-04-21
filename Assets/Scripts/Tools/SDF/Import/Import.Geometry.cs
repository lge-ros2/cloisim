/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportGeometry(in Geometry geometry, in System.Object parentObject)
			{
				if (geometry == null || geometry.IsEmpty())
				{
					return;
				}

				var targetObject = (parentObject as UE.GameObject);

				switch (geometry.Type)
				{
					case GeometryType.Mesh:
						targetObject.GenerateMesh(geometry.MeshShape);
						break;

					case GeometryType.Heightmap:
						targetObject.GenerateMesh(geometry.HeightmapShape);
						break;

					case GeometryType.Box:
					case GeometryType.Cylinder:
					case GeometryType.Sphere:
					case GeometryType.Capsule:
					case GeometryType.Cone:
					case GeometryType.Plane:
					case GeometryType.Polyline:
					case GeometryType.Ellipsoid:
						targetObject.GenerateMesh(geometry);
						break;

					case GeometryType.Image:
						UE.Debug.LogWarningFormat("[Geometry] Image geometry type is not supported, skipping.");
						break;

					default:
						UE.Debug.LogErrorFormat("[{0}] Not support type({1}) for geometry", geometry.Type, geometry.Type);
						return;
				}

				targetObject.SetActive(true);
			}
		}
	}
}