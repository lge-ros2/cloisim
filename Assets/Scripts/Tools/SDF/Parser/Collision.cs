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
			// Description: Parameters for torsional friction
			public class Torsional
			{
				// Description: Torsional friction coefficient, unitless maximum ratio of tangential stress to normal stress.
				public double coefficient = 1;


				// Description: If this flag is true, torsional friction is calculated using the "patch_radius" parameter. If this flag is set to false, "surface_radius" (R) and contact depth (d) are used to compute the patch radius as sqrt(R*d).
				public bool use_patch_radius = true;

				// Description: Radius of contact patch surface.
				public double patch_radius = 0;

				// Description: Surface radius on the point of contact.
				public double surface_radius = 0;

				// Description: Torsional friction parameters for ODE
				// Description: Force dependent slip for torsional friction, equivalent to inverse of viscous damping coefficient with units of rad/s/(Nm). A slip value of 0 is infinitely viscous.
				public double ode_slip = 0;
			}

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

			public Torsional torsional = null;
			public ODE ode = null;
			public Bullet bullet = null;
		}

		public class Contact
		{
			public class ODE
			{
				// Description: Soft constraint force mixing.
				public double soft_cfm = 0;
				// Description: Soft error reduction parameter
				public double soft_erp = 0.20000000000000001;
				// Description: dynamically "stiffness"-equivalent coefficient for contact joints
				public double kp = 1000000000000;
				// Description: dynamically "damping"-equivalent coefficient for contact joints
				public double kd = 1;

				// Description: maximum contact correction velocity truncation term.
				public double max_vel = 0.01;

				// Description: minimum allowable depth before contact correction impulse is applied
				public double min_depth = 0;
			}

			public class Bullet
			{
				// Description: Soft constraint force mixing.
				public double soft_cfm = 0;

				// Description: Soft error reduction parameter
				public double soft_erp = 0.20000000000000001;

				// Description: dynamically "stiffness"-equivalent coefficient for contact joints
				public double kp = 1000000000000;

				// Description: dynamically "damping"-equivalent coefficient for contact joints
				public double kd = 1;

				// Description: Similar to ODE's max_vel implementation. See http://bulletphysics.org/mediawiki-1.5.8/index.php/BtContactSolverInfo#Split_Impulse for more information.
				public bool split_impulse = true;

				// Description: Similar to ODE's max_vel implementation. See http://bulletphysics.org/mediawiki-1.5.8/index.php/BtContactSolverInfo#Split_Impulse for more information.
				public double split_impulse_penetration_threshold = -0.01;
			}

			// Description: Flag to disable contact force generation, while still allowing collision checks and contact visualization to occur.
			public bool collide_without_contact = false;

			// Description: Bitmask for collision filtering when collide_without_contact is on
			public uint collide_without_contact_bitmask = 1;

			// Description: Bitmask for collision filtering. This will override collide_without_contact. Parsed as 16-bit unsigned integer.
			public uint collide_bitmask = 65535;

			// Description: Bitmask for category of collision filtering. Collision happens if ((category1 & collision2) | (category2 & collision1)) is not zero. If not specified, the category_bitmask should be interpreted as being the same as collide_bitmask. Parsed as 16-bit unsigned integer.
			public uint category_bitmask = 65535;

			// Description: Poisson's ratio is the unitless ratio between transverse and axial strain. This value must lie between (-1, 0.5). Defaults to 0.3 for typical steel. Note typical silicone elastomers have Poisson's ratio near 0.49 ~ 0.50. For reference, approximate values for Material:(Young's Modulus, Poisson's Ratio) for some of the typical materials are: Plastic: (1e8 ~ 3e9 Pa, 0.35 ~ 0.41), Wood: (4e9 ~ 1e10 Pa, 0.22 ~ 0.50), Aluminum: (7e10 Pa, 0.32 ~ 0.35), Steel: (2e11 Pa, 0.26 ~ 0.31).
			public double poissons_ratio = 0.29999999999999999;

			// Description: Young's Modulus in SI derived unit Pascal. Defaults to -1. If value is less or equal to zero, contact using elastic modulus (with Poisson's Ratio) is disabled. For reference, approximate values for Material:(Young's Modulus, Poisson's Ratio) for some of the typical materials are: Plastic: (1e8 ~ 3e9 Pa, 0.35 ~ 0.41), Wood: (4e9 ~ 1e10 Pa, 0.22 ~ 0.50), Aluminum: (7e10 Pa, 0.32 ~ 0.35), Steel: (2e11 Pa, 0.26 ~ 0.31).
			public double elastic_modulus = -1;

			// Description: ODE contact parameters
			public ODE ode = null;
			// Description: Bullet contact parameters
			public Bullet bullet = null;
		}

		public class SoftContact
		{
			public class Dart
			{
				// Description: This is variable k_v in the soft contacts paper.Its unit is N/m.
				public double bone_attachment = 100;

				// Description: This is variable k_e in the soft contacts paper. Its unit is N/m.
				public double stiffness = 100;

				// Description: Viscous damping of point velocity in body frame. Its unit is N/m/s.
				public double damping = 10;

				// Description: Fraction of mass to be distributed among deformable nodes.
				public double flesh_mass_fraction = 0.050000000000000003;
			}

			// Description: soft contact pamameters based on paper: http://www.cc.gatech.edu/graphics/projects/Sumit/homepage/papers/sigasia11/jain_softcontacts_siga11.pdf
			public Dart dart = null;
		}

		public Bounce bounce = null;

		public Friction friction = null;

		public Contact contact = null;
		public SoftContact soft_contact = null;
	}

	/*
		The class for Collision element based on SDF Specification
		parent element : <link>
	*/
	public class Collision : Entity
	{
		// Description: intensity value returned by laser sensor.
		private double laser_retro = 0.0;

		// Description: Maximum number of contacts allowed between two entities. This value overrides the max_contacts element defined in physics.
		private int max_contacts = 10;

		private Geometry geometry = null;

		private Surface surface = null;

		public Collision(XmlNode _node)
			: base(_node)
		{
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
					surface.bounce.restitution_coefficient = Math.Clamp(GetValue<double>("surface/bounce/restitution_coefficient"), 0, 1);
					surface.bounce.threshold = GetValue<double>("surface/bounce/threshold");
				}

				if (IsValidNode("surface/friction"))
				{
					surface.friction = new Surface.Friction();

					if (IsValidNode("surface/friction/ode"))
					{
						surface.friction.ode = new Surface.Friction.ODE();
						surface.friction.ode.mu = Math.Clamp(GetValue<double>("surface/friction/ode/mu"), 0, 1);
						surface.friction.ode.mu2 = Math.Clamp(GetValue<double>("surface/friction/ode/mu2"), 0, 1);
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