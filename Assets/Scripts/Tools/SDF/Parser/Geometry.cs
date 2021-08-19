/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;
using System;

namespace SDF
{
	/*
		The class for Collision element based on SDF Specification
		parent element : <collision> or <visual>
	*/
	public class Geometry : Entity
	{
		private bool empty = false;

		private ShapeType shape = null;

		public bool IsEmpty => empty;

		public Geometry(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			if (IsValidNode("empty"))
			{
				empty = true;
			}
			else if (IsValidNode("box"))
			{
				Type = "box";
				shape = new Box();
				var sizeStr = GetValue<string>("box/size");
				(shape as Box).size.FromString(sizeStr);
			}
			else if (IsValidNode("mesh"))
			{
				Type = "mesh";
				shape = new Mesh();
				(shape as Mesh).uri = GetValue<string>("mesh/uri");
				var scale = GetValue<string>("mesh/scale");

				if (string.IsNullOrEmpty(scale))
				{
					(shape as Mesh).scale.Set(1.0f, 1.0f, 1.0f);
				}
				else
				{
					(shape as Mesh).scale.FromString(scale);
				}

				// Console.WriteLine("mesh uri : " + (shape as Mesh).uri + ", scale:" + scale);
			}
			else if (IsValidNode("sphere"))
			{
				Type = "sphere";
				shape = new Sphere();
				(shape as Sphere).radius = GetValue<double>("sphere/radius");
			}
			else if (IsValidNode("cylinder"))
			{
				Type = "cylinder";
				shape = new Cylinder();
				(shape as Cylinder).radius = GetValue<double>("cylinder/radius");
				(shape as Cylinder).length = GetValue<double>("cylinder/length");
			}
			else if (IsValidNode("plane"))
			{
				Type = "plane";
				shape = new Plane();
				var normal = GetValue<string>("plane/normal");
				(shape as Plane).normal.FromString(normal);

				var size = GetValue<string>("plane/size");
				(shape as Plane).size.FromString(size);
			}
			else if (IsValidNode("capsule"))
			{
				Type = "capsule";
				shape = new Capsule();
				(shape as Capsule).radius = GetValue<double>("capsule/radius");
				(shape as Capsule).length = GetValue<double>("capsule/length");
			}
			else if (IsValidNode("ellipsoid"))
			{
				Type = "ellipsoid";
				shape = new Ellipsoid();

				var radii = GetValue<string>("ellipsoid/radii");

				if (string.IsNullOrEmpty(radii))
				{
					(shape as Ellipsoid).radii.Set(1.0f, 1.0f, 1.0f);
				}
				else
				{
					(shape as Ellipsoid).radii.FromString(radii);
				}
			}
			else if (IsValidNode("image"))
			{
				Type = "image";
				shape = new Image();

				(shape as Image).uri = GetValue<string>("image/uri");
				(shape as Image).scale = GetValue<double>("image/scale");
				(shape as Image).threshold = GetValue<int>("image/threshold");
				(shape as Image).height = GetValue<double>("image/height");
				(shape as Image).granularity = GetValue<int>("image/granularity");
			}
			else if (IsValidNode("heightmap") ||
					 IsValidNode("polyline"))
			{
				Console.WriteLine("Currently not supported");
				empty = true;
			}
			else
			{
				empty = true;
				Console.WriteLine("missing mesh type");
			}
		}

		public ShapeType GetShape()
		{
			return shape;
		}

		public Type GetShapeType()
		{
			return shape?.GetType();
		}
	}
}