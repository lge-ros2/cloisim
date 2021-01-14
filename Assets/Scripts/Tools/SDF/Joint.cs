/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Xml;

namespace SDF
{
	public class Joints : Entities<Joint>
	{
		private const string TARGET_TAG = "joint";
		public Joints() : base(TARGET_TAG) {}
		public Joints(XmlNode _node) : base(_node, TARGET_TAG) {}

	}

	public class Axis
	{
		public double initial_position = 0.0;

		public Vector3<int> xyz = new Vector3<int>();

		public bool use_parent_model_frame = false;

		public double dynamics_damping = 0.0f;

		// public double dynamics_friction = 0.0;
		// dynamics_spring_reference
		public double dynamics_spring_stiffness = 0.0;

		public double limit_lower = -1e+16; // radians for revolute joints meters for prismatic joints
		public double limit_upper = 1e+16; // radians for revolute joints meters for prismatic joints

		// limit_effort
		// limit_velocity
		// limit_stiffness
		// limit_dissipation

		public bool UseLimit()
		{
			return (limit_lower.Equals(-1e+16) && limit_upper.Equals(1e+16))? false:true;
		}
	}

	public class OdePhysics
	{
		public double max_force = double.PositiveInfinity;
	}

	public class Joint : Entity
	{
		private string parent = string.Empty;
		private string child = string.Empty;

		private double gearbox_ratio = 0.0;
		private string gearbox_reference_body = string.Empty;
		private double thread_pitch = 0.0; // for screw joints

		private Axis axis = null; // for revolute/prismatic joints
		private Axis axis2 = null; // for revolute2(second axis)/universal joints

		// <physics> : TBD
		private OdePhysics odePhysics = null;

		// <sensor> : TBD, ???

		public string ParentLinkName => parent;

		public string ChildLinkName => child;

		public Axis Axis => axis;
		public Axis Axis2 => axis2;

		public OdePhysics OdePhysics => odePhysics;

		public Joint(XmlNode _node)
			: base(_node)
		{
			odePhysics = new OdePhysics();

			if (root != null)
			{
				ParseElements();
			}
		}

		protected override void ParseElements()
		{
			parent = GetValue<string>("parent");
			child = GetValue<string>("child");

			// Console.WriteLine("[{0}] P:{1} C:{2}", GetType().Name, parent, child);

			switch (Type)
			{
				case "gearbox":
					gearbox_ratio = GetValue<double>("gearbox_ratio");
					gearbox_reference_body = GetValue<string>("gearbox_reference_body");
					break;

				case "screw":
					thread_pitch = GetValue<double>("thread_pitch");
					break;

				case "revolute":
				case "revolute2":
				case "prismatic":
					axis = new Axis();
					var xyzStr = GetValue<string>("axis/xyz");
					axis.xyz.FromString(xyzStr);

					if (IsValidNode("axis/limit"))
					{
						axis.limit_lower = GetValue<double>("axis/limit/lower");
						axis.limit_upper = GetValue<double>("axis/limit/upper");
					}

					if (IsValidNode("axis/dynamics"))
					{
						axis.dynamics_damping = GetValue<double>("axis/dynamics/damping");
						axis.dynamics_spring_stiffness = GetValue<double>("axis/dynamics/spring_stiffness");
					}

					if (Type.Equals("revolute2"))
					{
						axis2 = new Axis();
						xyzStr = GetValue<string>("axis2/xyz");
						axis2.xyz.FromString(xyzStr);

						if (IsValidNode("axis2/limit"))
						{
							axis2.limit_lower = GetValue<double>("axis2/limit/lower");
							axis2.limit_upper = GetValue<double>("axis2/limit/upper");
						}
					}

					if (IsValidNode("physics/ode"))
					{
						if (IsValidNode("physics/ode/max_force"))
						{
							odePhysics.max_force = GetValue<double>("physics/ode/max_force");
						}
					}
					break;

				case "ball":
				case "fixed":
					// Console.WriteLine("[{0}] P:{1} C:{2}", Type, parent, child);
					break;

				default:
					Console.WriteLine("Invalid Type [{0}] P:{1} C:{2}", Type, parent, child);
					break;
			}
		}
	}
}