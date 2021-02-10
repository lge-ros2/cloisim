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
		/// <summary>An element specifying physical properties of the joint. These values are used to specify modeling properties of the joint, particularly useful for simulation.</summary>
		public class Dynamics
		{
			public double damping; // The physical velocity dependent viscous damping coefficient of the joint.
			public double friction; // The physical static friction value of the joint.
			public double spring_reference; // The spring reference position for this joint axis.
			public double spring_stiffness; // The spring stiffness for this joint axis.
		}

		/// <summary>specifies the limits of this joint</summary>
		public class Limit
		{
			public double lower = -1e+16; // Specifies the lower joint limit (radians for revolute joints, meters for prismatic joints). Omit if joint is continuous.
			public double upper = 1e+16; // Specifies the upper joint limit (radians for revolute joints, meters for prismatic joints). Omit if joint is continuous.
			public double effort = -1; // A value for enforcing the maximum joint effort applied. Limit is not enforced if value is negative.
			public double velocity = -1; // A value for enforcing the maximum joint velocity.
			public double stiffness = 1e+08; // Joint stop stiffness.
			public double dissipation = 1; // Joint stop dissipation.
			public bool Use()
			{
				return (lower.Equals(-1e+16) && upper.Equals(1e+16))? false:true;
			}
		}

		public double initial_position = 0.0;

		public Vector3<int> xyz = new Vector3<int>(0, 0, 1);

		public bool use_parent_model_frame = false;

		public Dynamics dynamics = null;

		public Limit limit = new Limit();
	}

	public class Joint : Entity
	{
		public class Physics
		{
			public class ODE
			{
				public double max_force = double.PositiveInfinity;
			}
		}

		private string parent = string.Empty;
		private string child = string.Empty;

		private double gearbox_ratio = 0.0;
		private string gearbox_reference_body = string.Empty;
		private double thread_pitch = 0.0; // for screw joints

		private Axis axis = null; // for revolute/prismatic joints
		private Axis axis2 = null; // for revolute2(second axis)/universal joints

		// <physics> : TBD
		private Physics.ODE ode = null;

		// <sensor> : TBD, ???

		public string ParentLinkName => parent;

		public string ChildLinkName => child;

		public Axis Axis => axis;
		public Axis Axis2 => axis2;

		public Physics.ODE PhysicsODE => ode;

		public Joint(XmlNode _node)
			: base(_node)
		{
			ode = new Physics.ODE();

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
						axis.limit.lower = GetValue<double>("axis/limit/lower");
						axis.limit.upper = GetValue<double>("axis/limit/upper");
					}

					if (IsValidNode("axis/dynamics"))
					{
						axis.dynamics = new Axis.Dynamics();
						axis.dynamics.damping = GetValue<double>("axis/dynamics/damping");
						axis.dynamics.spring_stiffness = GetValue<double>("axis/dynamics/spring_stiffness");
						axis.dynamics.friction = GetValue<double>("axis/dynamics/friction");
					}

					if (Type.Equals("revolute2"))
					{
						axis2 = new Axis();
						xyzStr = GetValue<string>("axis2/xyz");
						axis2.xyz.FromString(xyzStr);

						if (IsValidNode("axis2/dynamics"))
						{
							axis2.dynamics = new Axis.Dynamics();
							axis2.dynamics.damping = GetValue<double>("axis2/dynamics/damping");
							axis2.dynamics.spring_stiffness = GetValue<double>("axis2/dynamics/spring_stiffness");
							axis.dynamics.friction = GetValue<double>("axis2/dynamics/friction");
						}

						if (IsValidNode("axis2/limit"))
						{
							axis2.limit.lower = GetValue<double>("axis2/limit/lower");
							axis2.limit.upper = GetValue<double>("axis2/limit/upper");
						}
					}

					if (IsValidNode("physics/ode"))
					{
						if (IsValidNode("physics/ode/max_force"))
						{
							ode.max_force = GetValue<double>("physics/ode/max_force");
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