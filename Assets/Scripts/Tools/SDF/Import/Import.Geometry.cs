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
			protected override void ImportGeometry(in SDFormat.Geometry geometry, in System.Object parentObject)
			{
				if (geometry == null || geometry.IsEmpty())
				{
					return;
				}

				var targetObject = (parentObject as UE.GameObject);

				switch (geometry.Type)
				{
					case SDFormat.GeometryType.Mesh:
						targetObject.GenerateMesh(geometry.MeshShape);
						break;

					case SDFormat.GeometryType.Heightmap:
						targetObject.GenerateMesh(geometry.HeightmapShape);
						break;

					case SDFormat.GeometryType.Box:
					case SDFormat.GeometryType.Cylinder:
					case SDFormat.GeometryType.Sphere:
					case SDFormat.GeometryType.Capsule:
					case SDFormat.GeometryType.Cone:
					case SDFormat.GeometryType.Plane:
					case SDFormat.GeometryType.Polyline:
					case SDFormat.GeometryType.Ellipsoid:
						targetObject.GenerateMesh(geometry);
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