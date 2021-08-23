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

		public Pose<double> pose;

		public Inertial(double _mass = 0.0)
		{
			mass = _mass;
			pose = new Pose<double>();
		}
	}

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

		// <velocity decay> : TBD

		private Inertial inertial = null;

		private Collisions collisions;
		private Visuals visuals;
		private Sensors sensors;

		// <projector> : TBD
		// <audio_sink> : TBD
		// <audio_source> : TBD
		// <battery> : TBD
		// <light> : TBD

		public bool Gravity => gravity;

		public bool Kinematic => kinematic;

		public bool SelfCollide => self_collide;

		public Inertial Inertial => inertial;

		public Link(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			collisions = new Collisions(root);
			visuals = new Visuals(root);
			sensors = new Sensors(root);

			gravity = GetValue<bool>("gravity");
			enable_wind = GetValue<bool>("enable_wind");
			self_collide = GetValue<bool>("self_collide");
			kinematic = GetValue<bool>("kinematic");
			must_be_base_link = GetValue<bool>("must_be_base_link");

			if (IsValidNode("inertial"))
			{
				inertial = new Inertial();
				inertial.mass = GetValue<double>("inertial/mass");

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
				inertial.pose.FromString(poseStr);
				// Console.WriteLine("Link Mass: " + inertial.mass);
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