/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

namespace SDF
{
	public class ShapeType
	{
	}

	public class Box : ShapeType
	{
		public Vector3<double> size = new Vector3<double>();
	}

	public class Cylinder : ShapeType
	{
		public double radius;
		public double length;
	}

	// <heightmap> : TBD
	// <image> : TBD

	public class Mesh : ShapeType
	{
		public string uri = null;
		public string submesh_name = null;
		public bool submesh_center = false;
		public Vector3<double> scale = new Vector3<double>();
	}

	public class Plane : ShapeType
	{
		public Vector3<int> normal = new Vector3<int>();
		public Vector2<double> size = new Vector2<double>();
	}

	// <polyline> : TBD

	public class Sphere : ShapeType
	{
		public double radius;
	}

}