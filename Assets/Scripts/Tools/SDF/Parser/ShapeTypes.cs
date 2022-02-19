/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

namespace SDF
{
	public interface ShapeType
	{
	}

	public class Box : ShapeType
	{
		public Vector3<double> size = new Vector3<double>(1, 1, 1);
	}

	public class Cylinder : ShapeType
	{
		// Description: Radius of the cylinder
		public double radius = 1;

		// Description: Length of the cylinder along the z axis
		public double length = 1;
	}

	public class Heightmap : ShapeType
	{
		public class Texture
		{
			public double size = 1;
			public string diffuse = "__default__";
			public string normal = "__default__";
		}

		public class Blend
		{
			public double min_height = 0;
			public double fade_dist = 0;
		}

		public string uri = "__default__";
		public Vector3<double> size = new Vector3<double>(1, 1, 1);
		public Vector3<double> pos = new Vector3<double>(0, 0, 0);
		public List<Texture> textures = new List<Texture>();
		public List<Blend> blends = new List<Blend>();
		public bool use_terrain_pagin = false;
		public uint sampling = 1;
	}

	public class Ellipsoid : ShapeType
	{
		public Vector3<double> radii = new Vector3<double>();
	}

	public class Image : ShapeType
	{
		public string uri = "__default__";
		public double scale = 1d;
		public int threshold = 200;
		public double height = 1d;
		public int granularity = 1;
	}

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

	public class Polyline : ShapeType
	{
		public List<Vector2<double>> point = new List<Vector2<double>>();
		public double height = 1;
	}

	public class Sphere : ShapeType
	{
		public double radius;
	}

	public class Capsule : ShapeType
	{
		public double radius;
		public double length;
	}
}