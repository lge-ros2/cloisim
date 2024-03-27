/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using System;

namespace SDF
{
	public class Inertial
	{
		public class Inertia
		{
			public double ixx;
			public double ixy;
			public double ixz;
			public double iyy;
			public double iyz;
			public double izz;
		}

		public double mass;

		public Inertia inertia;

		public Pose<double> pose = null;

		public Inertial(double _mass = 0.0)
		{
			mass = _mass;
			pose = new Pose<double>();
		}
	}

	public class Battery
	{
		public string name = "__default__";
		public double voltage = 0;
	}

	public class VelocityDecay
	{
		public double linear = 0;
		public double angular = 0;
	};

	public class Links : Entities<Link>
	{
		private const string TARGET_TAG = "link";
		public Links() : base(TARGET_TAG) { }
		public Links(XmlNode _node) : base(_node, TARGET_TAG) { }
	}

	public class Link : Entity
	{
		private bool gravity = true;
		private bool enable_wind = false;
		private bool self_collide = false;
		private bool kinematic = false;
		private bool must_be_base_link = false;

		private VelocityDecay _velocity_decay = null;

		private Inertial _inertial = null;
		private Collisions collisions;
		private Visuals visuals;
		private Sensors sensors;

		// <projector> : TBD
		// <audio_sink> : TBD
		// <audio_source> : TBD

		private Battery battery = null;

		private Light light = null;

		public bool Gravity => gravity;

		public bool Kinematic => kinematic;

		public bool SelfCollide => self_collide;

		public VelocityDecay VelocityDecay => _velocity_decay;

		public Inertial Inertial => _inertial;

		public Battery Battery => battery;

		public Link(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			collisions = new Collisions(root);
			visuals = new Visuals(root);
			sensors = new Sensors(root);

			gravity = GetValue<bool>("gravity", true);
			enable_wind = GetValue<bool>("enable_wind", false);
			self_collide = GetValue<bool>("self_collide", false);
			kinematic = GetValue<bool>("kinematic", false);
			must_be_base_link = GetValue<bool>("must_be_base_link", false);

			if (IsValidNode("velocity_decay"))
			{
				_velocity_decay = new VelocityDecay();
				_velocity_decay.linear = GetValue<double>("velocity_decay/linear", 0);
				_velocity_decay.angular = GetValue<double>("velocity_decay/angular", 0);
			}

			if (IsValidNode("inertial"))
			{
				_inertial = new Inertial();
				Inertial.mass = GetValue<double>("inertial/mass");

				if (IsValidNode("inertial/inertia"))
				{
					Inertial.inertia = new Inertial.Inertia();
					Inertial.inertia.ixx = GetValue<double>("inertial/inertia/ixx");
					Inertial.inertia.ixy = GetValue<double>("inertial/inertia/ixy");
					Inertial.inertia.ixz = GetValue<double>("inertial/inertia/ixz");
					Inertial.inertia.iyy = GetValue<double>("inertial/inertia/iyy");
					Inertial.inertia.iyz = GetValue<double>("inertial/inertia/iyz");
					Inertial.inertia.izz = GetValue<double>("inertial/inertia/izz");
				}

				var poseStr = GetValue<string>("inertial/pose");
				if (poseStr == null)
					Inertial.pose = null;
				else
					Inertial.pose.FromString(poseStr);
				// Console.WriteLine("Link Mass: " + inertial.mass);
			}

			if (IsValidNode("light"))
			{
				light = new Light(root);
			}

			if (IsValidNode("battery"))
			{
				battery = new Battery();
				battery.name = GetAttributeInPath<string>("battery", "name");
				battery.voltage = GetValue<double>("battery/voltage");
			}
		}

		public List<Collision> GetCollisions()
		{
			return collisions.GetData();
		}

		public List<Visual> GetVisuals()
		{
			return visuals.GetData();
		}

		public List<Sensor> GetSensors()
		{
			return sensors.GetData();
		}
	}
}