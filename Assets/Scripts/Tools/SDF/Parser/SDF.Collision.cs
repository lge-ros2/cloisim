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
		public class Bounce
		{
			public double restitution_coefficient = 0;
			public double threshold = 100000;
		}

		public class Friction
		{
			// <friction/torsional> : TBD

			public class ODE
			{
				public double mu = 1; // Coefficient of friction in first friction pyramid direction, the unitless maximum ratio of force in first friction pyramid direction to normal force.
				public double mu2 = 1; // Coefficient of friction in second friction pyramid direction, the unitless maximum ratio of force in second friction pyramid direction to normal force.
				public Vector3<double> fdir1 = new Vector3<double>(0, 0, 0); // Unit vector specifying first friction pyramid direction in collision-fixed reference frame. If the friction pyramid model is in use, and this value is set to a unit vector for one of the colliding surfaces, the ODE Collide callback function will align the friction pyramid directions with a reference frame fixed to that collision surface. If both surfaces have this value set to a vector of zeros, the friction pyramid directions will be aligned with the world frame. If this value is set for both surfaces, the behavior is undefined.
				public double slip1 = 0; // Force dependent slip in first friction pyramid direction, equivalent to inverse of viscous damping coefficient with units of m/s/N. A slip value of 0 is infinitely viscous.
				public double slip2 = 0; // Force dependent slip in second friction pyramid direction, equivalent to inverse of viscous damping coefficient with units of m/s/N. A slip value of 0 is infinitely viscous.
			}

			public class Bullet
			{
				public double friction = 1; // Coefficient of friction in first friction pyramid direction, the unitless maximum ratio of force in first friction pyramid direction to normal force.
				public double friction2 = 1; // Coefficient of friction in second friction pyramid direction, the unitless maximum ratio of force in second friction pyramid direction to normal force.
				public Vector3<double> fdir1 = new Vector3<double>(0, 0, 0); // Unit vector specifying first friction pyramid direction in collision-fixed reference frame. If the friction pyramid model is in use, and this value is set to a unit vector for one of the colliding surfaces, the friction pyramid directions will be aligned with a reference frame fixed to that collision surface. If both surfaces have this value set to a vector of zeros, the friction pyramid directions will be aligned with the world frame. If this value is set for both surfaces, the behavior is undefined.
				public double rolling_friction = 1; // Coefficient of rolling friction
			}

			public ODE ode;
			public Bullet bullet;
		}

		public Bounce bounce;

		public Friction friction;

		// <contact> : TBD
		// <soft_contact> : TBD
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
				{
					surface.bounce = new Surface.Bounce();
					surface.bounce.restitution_coefficient = GetValue<double>("surface/bounce/restitution_coefficient");
					surface.bounce.threshold = GetValue<double>("surface/bounce/threshold");
				}

				if (IsValidNode("surface/friction"))
				{
					surface.friction = new Surface.Friction();

					if (IsValidNode("surface/friction/ode"))
					{
						surface.friction.ode = new Surface.Friction.ODE();
						surface.friction.ode.mu = GetValue<double>("surface/friction/ode/mu");
						surface.friction.ode.mu2 = GetValue<double>("surface/friction/ode/mu2");
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