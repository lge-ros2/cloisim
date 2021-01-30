/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	/*
		The class for handling multiple elemtent
		for Collision
	*/
	public class Collisions : Entities<Collision>
	{
		private const string TARGET_TAG = "collision";
		public Collisions() : base(TARGET_TAG) {}
		public Collisions(XmlNode _node) : base(_node, TARGET_TAG) {}
	}


	public class Surface
	{
		public double bounce_restitution_coefficient = 0;
		// <bounce/threshold> : TBD

		// <friction/torsional> : TBD
		// <friction/ode> : TBD
		// <friction/bullet> : TBD

		public double friction = 0;
		public double friction2 = 0;

		// <contact> : TBD
		// <soft_contact> : TBD

		public Surface()
		{
		}
	}

	/*
		The class for Collision element based on SDF Specification
		parent element : <link>
	*/
	public class Collision : Entity
	{
		private double laser_retro = 0.0;
		private int max_contacts = 10;
		private Geometry geometry = null;
		private Surface surface = null;

		public Collision(XmlNode _node)
			: base(_node)
		{
			if (root != null)
			{
				ParseElements();
			}
		}

		protected override void ParseElements()
		{
			XmlNode geomNode = GetNode("geometry");
			if (geomNode != null)
			{
				geometry = new Geometry(geomNode);
			}

			if (IsValidNode("surface"))
			{
				surface = new Surface();

				if (IsValidNode("surface/bounce"))
					surface.bounce_restitution_coefficient = GetValue<double>("surface/bounce/restitution_coefficient");

				if (IsValidNode("surface/friction"))
				{
					if (IsValidNode("surface/friction/ode"))
					{
						surface.friction = GetValue<double>("surface/friction/ode/mu");
						surface.friction2 = GetValue<double>("surface/friction/ode/mu2");
					}
				}
			}

			laser_retro = GetValue<double>("laser_retro");
			max_contacts = GetValue<int>("max_contacts", max_contacts);

			// Console.WriteLine("[{0}] P:{1} C:{2}", GetType().Name, parent, child);
		}

		public Geometry GetGeometry()
		{
			return geometry;
		}

		public Surface GetSurface()
		{
			return surface;
		}
	}
}