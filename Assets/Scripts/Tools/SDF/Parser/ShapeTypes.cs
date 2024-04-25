/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;

namespace SDF
{
	public interface ShapeType
	{
		public string Type();
	}

	public class Box : ShapeType
	{
		public Vector3<double> size = new Vector3<double>(1, 1, 1);

		public string Type()
		{
			return "box";
		}
	}

	public class Cylinder : ShapeType
	{
		// Description: Radius of the cylinder
		public double radius = 1;

		// Description: Length of the cylinder along the z axis
		public double length = 1;

		public string Type()
		{
			return "cylinder";
		}
	}

	public class Ellipsoid : ShapeType
	{
		public Vector3<double> radii = new Vector3<double>();

		public string Type()
		{
			return "ellipsoid";
		}
	}

	// Description: A heightmap based on a 2d grayscale image.
	public class Heightmap : ShapeType
	{
		// Description: The heightmap can contain multiple textures.
		// The order of the texture matters.
		// The first texture will appear at the lowest height,
		// and the last texture at the highest height.
		// Use blend to control the height thresholds and fade between textures.
		public class Texture
		{
			// Description: Size of the applied texture in meters.
			public double size = 10;

			// Description: Diffuse texture image filename
			public string diffuse = "__default__";

			// Description: Normalmap texture image filename
			public string normal = "__default__";
		}

		// Description: The blend tag controls how two adjacent textures are mixed. The number of blend elements should equal one less than the number of textures.
		public class Blend
		{
			// Description: Min height of a blend layer
			public double min_height = 0;
			// Description: Distance over which the blend occurs
			public double fade_dist = 0;
		}

		// Description: URI to a grayscale image file
		public string uri = "__default__";

		// Description: The size of the heightmap in world units. When loading an image: "size" is used if present, otherwise defaults to 1x1x1. When loading a DEM: "size" is used if present, otherwise defaults to true size of DEM.
		public Vector3<double> size = new Vector3<double>(1, 1, 1);

		// Description: A position offset.
		public Vector3<double> pos = new Vector3<double>(0, 0, 0);

		public List<Texture> texture = new List<Texture>();

		public List<Blend> blend = new List<Blend>();

		// Description: Set if the rendering engine will use terrain paging
		public bool use_terrain_paging = false;

		// Description: Samples per heightmap datum. For rasterized heightmaps, this indicates the number of samples to take per pixel. Using a higher value, e.g. 2, will generally improve the quality of the heightmap but lower performance.
		public uint sampling = 1;

		public string Type()
		{
			return "heightmap";
		}
	}

	public class Image : ShapeType
	{
		public string uri = "__default__";
		public double scale = 1d;
		public int threshold = 200;
		public double height = 1d;
		public int granularity = 1;

		public string Type()
		{
			return "image";
		}
	}

	public class Mesh : ShapeType
	{
		public string uri = null;
		public string submesh_name = null;
		public bool submesh_center = false;
		public Vector3<double> scale = new Vector3<double>();

		public string Type()
		{
			return "mesh";
		}
	}

	public class Plane : ShapeType
	{
		public Vector3<int> normal = new Vector3<int>();
		public Vector2<double> size = new Vector2<double>();

		public string Type()
		{
			return "plane";
		}
	}

	public class Polyline : Entity
	{
		public List<Vector2<double>> point = new List<Vector2<double>>();
		public double height = 1;

		public Polyline(XmlNode _node)
			: base(_node)
		{
		}
	}

	public class Polylines : List<Polyline>, ShapeType
	{
		public string Type()
		{
			return "polyline";
		}
	}

	public class Sphere : ShapeType
	{
		public double radius;

		public string Type()
		{
			return "sphere";
		}
	}

	public class Capsule : ShapeType
	{
		public double radius;
		public double length;

		public string Type()
		{
			return "capsule";
		}
	}
}