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
			ParseElements();
		}

		protected override void ParseElements()
		{
			if (root == null)
				return;

			if (IsValidNode("box"))
			{
				Type = "box";
				shape = new Box();
				string sizeStr = GetValue<string>("box/size");
				(shape as Box).size.SetByString(sizeStr);
			}
			else if (IsValidNode("mesh"))
			{
				Type = "mesh";
				shape = new Mesh();
				(shape as Mesh).uri = GetValue<string>("mesh/uri");
				string scale = GetValue<string>("mesh/scale");

				if (string.IsNullOrEmpty(scale))
					(shape as Mesh).scale.Set(1.0f, 1.0f, 1.0f);
				else
					(shape as Mesh).scale.SetByString(scale);

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
				(shape as Cylinder).length = GetValue<double>("cylinder/length");
				(shape as Cylinder).radius = GetValue<double>("cylinder/radius");
			}
			else if (IsValidNode("plane"))
			{
				Type = "plane";
				shape = new Plane();
				string normal = GetValue<string>("plane/normal");
				(shape as Plane).normal.SetByString(normal);

				string size = GetValue<string>("plane/size");
				(shape as Plane).size.SetByString(size);
			}
			else if (IsValidNode("height") ||
					 IsValidNode("image") ||
					 IsValidNode("polyline"))
			{
				Console.WriteLine("Currently not supported");
			}
			else if (IsValidNode("empty"))
			{
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
			if (Type.Equals("box"))
				return typeof(Box);
			else if (Type.Equals("mesh"))
				return typeof(Mesh);
			else if (Type.Equals("sphere"))
				return typeof(Sphere);
			else if (Type.Equals("cylinder"))
				return typeof(Cylinder);
			else if (Type.Equals("plane"))
				return typeof(Plane);
			else
				return null;
		}
	}
}