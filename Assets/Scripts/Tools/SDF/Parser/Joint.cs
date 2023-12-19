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
		public Joints() : base(TARGET_TAG) { }
		public Joints(XmlNode _node) : base(_node, TARGET_TAG) { }
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
			public double lower = double.NegativeInfinity; // Specifies the lower joint limit (radians for revolute joints, meters for prismatic joints). Omit if joint is continuous.
			public double upper = double.PositiveInfinity; // Specifies the upper joint limit (radians for revolute joints, meters for prismatic joints). Omit if joint is continuous.
			public double effort = double.PositiveInfinity; // A value for enforcing the maximum joint effort applied. Limit is not enforced if value is negative.
			public double velocity = double.PositiveInfinity; // An attribute for enforcing the maximum joint velocity.
			public double stiffness = 1e+08; // Joint stop stiffness.
			public double dissipation = 1; // Joint stop dissipation.

			public bool HasJoint()
			{
				return (double.IsInfinity(lower) && double.IsInfinity(upper)) ? false : true;
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
			public class SimBody
			{
				// Description: Force cut in the multibody graph at this joint.
				public bool must_be_loop_joint = false;
			}

			public class ODE
			{
				// Description: If cfm damping is set to true, ODE will use CFM to simulate damping, allows for infinite damping, and one additional constraint row (previously used for joint limit) is always active.
				public bool cfm_damping = false;

				// Description: If implicit_spring_damper is set to true, ODE will use CFM, ERP to simulate stiffness and damping, allows for infinite damping, and one additional constraint row (previously used for joint limit) is always active. This replaces cfm_damping parameter in SDFormat 1.4.
				public bool implicit_spring_damper = false;

				// Description: Scale the excess for in a joint motor at joint limits. Should be between zero and one.
				public double fudge_factor = 0;

				// Description: Constraint force mixing for constrained directions
				public double cfm = 0;
				// Description: Error reduction parameter for constrained directions
				public double erp = 0.20000000000000001;

				// Description: Bounciness of the limits
				public double bounce = 0;

				// Description: Maximum force or torque used to reach the desired velocity.
				public double max_force = 0;

				// Description: The desired velocity of the joint. Should only be set if you want the joint to move on load.
				public double velocity = 0;

				// Description: Constraint force mixing parameter used by the joint stop
				public double limit_cfm = 0;

				// Description: Error reduction parameter used by the joint stop
				public double limit_erp = 0.20000000000000001;

				// Description: Suspension constraint force mixing parameter
				public double suspension_cfm = 0;
				// Description: Suspension error reduction parameter
				public double suspension_erp = 0.20000000000000001;
			}

			// Description: Simbody specific parameters
			public SimBody simbody = null;

			// Description: ODE specific parameters
			public ODE ode = null;

			// Description: If provide feedback is set to true, physics engine will compute the constraint forces at this joint.
			public bool provide_feedback = false;
		}

		private string parent = string.Empty;
		private string child = string.Empty;

		private double gearbox_ratio = 0.0;
		private string gearbox_reference_body = string.Empty;
		private double thread_pitch = 0.0; // for screw joints

		private Axis axis = null; // for revolute/prismatic joints
		private Axis axis2 = null; // for revolute2(second axis)/universal joints

		private Physics physics = new Physics();

		// private Sensors sensors = null;

		public string ParentLinkName => parent;

		public string ChildLinkName => child;

		public Axis Axis => axis;
		public Axis Axis2 => axis2;

		public Physics.ODE PhysicsODE => physics.ode;

		public Joint(XmlNode _node)
			: base(_node)
		{
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
				case "universal":
				case "prismatic":
					axis = new Axis();
					var xyzStr = GetValue<string>("axis/xyz");
					axis.xyz.FromString(xyzStr);

					if (IsValidNode("axis/limit"))
					{
						if (IsValidNode("axis/limit/lower"))
						{
							axis.limit.lower = GetValue<double>("axis/limit/lower");
						}

						if (IsValidNode("axis/limit/upper"))
						{
							axis.limit.upper = GetValue<double>("axis/limit/upper");
						}

						if (IsValidNode("axis/limit/effort"))
						{
							axis.limit.effort = GetValue<double>("axis/limit/effort");
						}

						if (IsValidNode("axis/limit/velocity"))
						{
							axis.limit.velocity = GetValue<double>("axis/limit/velocity");
						}

						if (IsValidNode("axis/limit/stiffness"))
						{
							axis.limit.stiffness = GetValue<double>("axis/limit/stiffness");
						}

						if (IsValidNode("axis/limit/dissipation"))
						{
							axis.limit.dissipation = GetValue<double>("axis/limit/dissipation");
						}
					}

					if (IsValidNode("axis/dynamics"))
					{
						axis.dynamics = new Axis.Dynamics();
						axis.dynamics.damping = GetValue<double>("axis/dynamics/damping");
						axis.dynamics.spring_reference = GetValue<double>("axis/dynamics/spring_reference");
						axis.dynamics.spring_stiffness = GetValue<double>("axis/dynamics/spring_stiffness");
						axis.dynamics.friction = GetValue<double>("axis/dynamics/friction");
					}

					if (Type.Equals("revolute2") || Type.Equals("universal"))
					{
						axis2 = new Axis();
						xyzStr = GetValue<string>("axis2/xyz");
						axis2.xyz.FromString(xyzStr);

						if (IsValidNode("axis2/dynamics"))
						{
							axis2.dynamics = new Axis.Dynamics();
							axis2.dynamics.damping = GetValue<double>("axis2/dynamics/damping");
							axis2.dynamics.spring_reference = GetValue<double>("axis2/dynamics/spring_reference");
							axis2.dynamics.spring_stiffness = GetValue<double>("axis2/dynamics/spring_stiffness");
							axis.dynamics.friction = GetValue<double>("axis2/dynamics/friction");
						}

						if (IsValidNode("axis2/limit"))
						{
							axis2.limit.lower = GetValue<double>("axis2/limit/lower");
							axis2.limit.upper = GetValue<double>("axis2/limit/upper");

							if (IsValidNode("axis2/limit/effort"))
							{
								axis2.limit.effort = GetValue<double>("axis2/limit/effort");
							}

							if (IsValidNode("axis2/limit/velocity"))
							{
								axis2.limit.velocity = GetValue<double>("axis2/limit/velocity");
							}

							if (IsValidNode("axis2/limit/stiffness"))
							{
								axis2.limit.stiffness = GetValue<double>("axis2/limit/stiffness");
							}

							if (IsValidNode("axis2/limit/dissipation"))
							{
								axis2.limit.dissipation = GetValue<double>("axis2/limit/dissipation");
							}
						}
					}

					if (IsValidNode("physics/ode"))
					{
						physics.ode = new Physics.ODE();

						if (IsValidNode("physics/ode/max_force"))
						{
							physics.ode.max_force = GetValue<double>("physics/ode/max_force");
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