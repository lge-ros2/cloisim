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
	public class World : Entity
	{
		// <audio> : TBD
		// <wind> : TBD
		Vector3<double> gravity = null;

		// <magnetic_field> : TBD
		// <atmosphere> : TBD
		// <gui> : TBD
		Pose<double> gui_camera_pose = null;

		// <physics> : TBD
		private Physics physics = null;
		// <scene> : TBD
		// <light> : TBD
		private Light light = null;
		private Models models = null;
		// <actor> : TBD
		// <road> : TBD
		// <spherical_coordinates> : TBD
		// <state> : TBD
		// <population> : TBD
		private Plugins plugins;

		public Pose<double> GuiCameraPose => gui_camera_pose;

		public World(XmlNode _node)
			: base(_node)
		{
			if (root != null)
			{
				ParseElements();
			}
		}

		protected override void ParseElements()
		{
			models = new Models(root);
			physics = new Physics(root);
			light = new Light(root);
			plugins = new Plugins(root);

			var gravityStr = GetValue<string>("static");

			gravity = new Vector3<double>();
			gravity.SetByString(gravityStr);

			// Console.WriteLine("{0}", GetType().Name);

			// Console.WriteLine("[{0}] {1} {2} {3} {4}", GetType().Name,
				// isStatic, isSelfCollide, allowAutoDisable, enableWind);

			if (IsValidNode("gui"))
			{
				if (IsValidNode("gui/camera"))
				{
					if (IsValidNode("gui/camera/pose"))
					{
						var value = GetValue<string>("gui/camera/pose").ToLower();
						// Console.WriteLine(value);
						// x y z roll pitch yaw
						string[] poseStr = value.Split(' ');

						// Convert Axis
						// gazebo x y z => unity x z y
						gui_camera_pose = new Pose<double>();
						gui_camera_pose.Pos.X = Convert.ToDouble(poseStr[0]);
						gui_camera_pose.Pos.Y = Convert.ToDouble(poseStr[1]);
						gui_camera_pose.Pos.Z = Convert.ToDouble(poseStr[2]);
						gui_camera_pose.Rot.Roll = Convert.ToDouble(poseStr[3]);
						gui_camera_pose.Rot.Pitch = Convert.ToDouble(poseStr[4]);
						gui_camera_pose.Rot.Yaw = Convert.ToDouble(poseStr[5]);

						// Console.WriteLine(gui_camera_pose);
					}
				}
			}

			if (IsValidNode("audio"))
			{
				Console.WriteLine("<audio> tag is NOT supported yet.");
			}

			if (IsValidNode("wind"))
			{
				Console.WriteLine("<wind> tag is NOT supported yet.");
			}

			if (IsValidNode("magnetic_field"))
			{
				Console.WriteLine("<magnetic_field> tag is NOT supported yet.");
			}

			if (IsValidNode("atmosphere"))
			{
				Console.WriteLine("<atmosphere> tag is NOT supported yet.");
			}

			if (IsValidNode("physics"))
			{
				Console.WriteLine("<physics> tag is NOT supported yet.");
			}

			if (IsValidNode("scene"))
			{
				Console.WriteLine("<scene> tag is NOT supported yet.");
			}

			if (IsValidNode("light"))
			{
				Console.WriteLine("<light> tag is NOT supported yet.");
			}

			if (IsValidNode("frame"))
			{
				Console.WriteLine("<frame> tag is NOT supported yet.");
			}

			if (IsValidNode("actor"))
			{
				Console.WriteLine("<actor> tag is NOT supported yet.");
			}

			if (IsValidNode("road"))
			{
				Console.WriteLine("<road> tag is NOT supported yet.");
			}

			if (IsValidNode("spherical_coordinates"))
			{
				Console.WriteLine("<spherical_coordinates> tag is NOT supported yet.");
			}

			if (IsValidNode("state"))
			{
				Console.WriteLine("<state> tag is NOT supported yet.");
			}

			if (IsValidNode("population"))
			{
				Console.WriteLine("<population> tag is NOT supported yet.");
			}
		}

		public List<Model> GetModels()
		{
			return models.GetData();
		}

		public List<Plugin> GetPlugins()
		{
			return plugins.GetData();
		}
	}
}