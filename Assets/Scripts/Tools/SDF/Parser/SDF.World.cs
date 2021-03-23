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

		public class Gui
		{
			public class Camera : Entity
			{
				public class TrackVisual
				{
					// Description: Name of the tracked visual. If no name is provided, the remaining settings will be applied whenever tracking is triggered in the GUI.
					public string name = "__default__";

					// Description: Minimum distance between the camera and the tracked visual. This parameter is only used if static is set to false.
					public double min_dist = 0;

					// Description: Maximum distance between the camera and the tracked visual. This parameter is only used if static is set to false.
					public double max_dist = 0;

					// Description: If set to true, the position of the camera is fixed relatively to the model or to the world, depending on the value of the use_model_frame element. Otherwise, the position of the camera may vary but the distance between the camera and the model will depend on the value of the min_dist and max_dist elements. In any case, the camera will always follow the model by changing its orientation.
					public bool _static = false;

					// Description: If set to true, the position of the camera is relative to the model reference frame, which means that its position relative to the model will not change. Otherwise, the position of the camera is relative to the world reference frame, which means that its position relative to the world will not change. This parameter is only used if static is set to true.
					public bool use_model_frame = true;

					// Description: The position of the camera's reference frame. This parameter is only used if static is set to true. If use_model_frame is set to true, the position is relative to the model reference frame, otherwise it represents world coordinates.
					public Vector3<double> xyz = new Vector3<double>(-5, 0, 3);

					// Description: If set to true, the camera will inherit the yaw rotation of the tracked model. This parameter is only used if static and use_model_frame are set to true.
					public bool inherit_yaw = false;
				}

 				// defualt name "user_camera"
				public string view_controller = "orbit";

				// Description: Set the type of projection for the camera. Valid values are "perspective" and "orthographic".
				public string projection_type= "perspective";

				public TrackVisual track_visual = null;

				public Camera(XmlNode _node)
				: base(_node, "user_camera")
				{
					if (root != null)
					{
						ParseElements();
					}
				}

				protected override void ParseElements()
				{
				}
			}

			public bool fullscreen;

			public Camera camera;

			// Description: A plugin is a dynamically loaded chunk of code. It can exist as a child of world, model, and sensor.
			private Plugins plugins;
		}

		private Gui gui = null;

		// <physics> : TBD
		private Physics physics = null;
		// <scene> : TBD
		// <light> : TBD
		private Light light = null;
		private Models models = null;
		private Actors actors = null;

		// <road> : TBD
		// <spherical_coordinates> : TBD
		// <state> : TBD
		// <population> : TBD

		private Plugins plugins;

		// public Pose<double> GuiCameraPose => gui_camera_pose;

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
			actors = new Actors(root);
			physics = new Physics(root);
			light = new Light(root);
			plugins = new Plugins(root);

			var gravityStr = GetValue<string>("static");

			gravity = new Vector3<double>();
			gravity.FromString(gravityStr);

			// Console.WriteLine("{0}", GetType().Name);

			// Console.WriteLine("[{0}] {1} {2} {3} {4}", GetType().Name,
				// isStatic, isSelfCollide, allowAutoDisable, enableWind);

			if (IsValidNode("gui"))
			{
				gui = new Gui();
				gui.fullscreen = GetAttributeInPath<bool>("gui", "fullscreen");

				if (IsValidNode("gui/camera"))
				{
					gui.camera = new Gui.Camera(GetNode("gui/camera"));
				}
			}

			if (IsValidNode("audio"))
			{
				// Console.WriteLine("<audio> tag is NOT supported yet.");
			}

			if (IsValidNode("wind"))
			{
				// Console.WriteLine("<wind> tag is NOT supported yet.");
			}

			if (IsValidNode("magnetic_field"))
			{
				// Console.WriteLine("<magnetic_field> tag is NOT supported yet.");
			}

			if (IsValidNode("atmosphere"))
			{
				// Console.WriteLine("<atmosphere> tag is NOT supported yet.");
			}

			if (IsValidNode("physics"))
			{
				// Console.WriteLine("<physics> tag is NOT supported yet.");
			}

			if (IsValidNode("scene"))
			{
				// Console.WriteLine("<scene> tag is NOT supported yet.");
			}

			if (IsValidNode("light"))
			{
				// Console.WriteLine("<light> tag is NOT supported yet.");
			}

			if (IsValidNode("frame"))
			{
				// Console.WriteLine("<frame> tag is NOT supported yet.");
			}

			if (IsValidNode("road"))
			{
				// Console.WriteLine("<road> tag is NOT supported yet.");
			}

			if (IsValidNode("spherical_coordinates"))
			{
				// Console.WriteLine("<spherical_coordinates> tag is NOT supported yet.");
			}

			if (IsValidNode("state"))
			{
				// Console.WriteLine("<state> tag is NOT supported yet.");
			}

			if (IsValidNode("population"))
			{
				// Console.WriteLine("<population> tag is NOT supported yet.");
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

		public List<Actor> GetActors()
		{
			return actors.GetData();
		}

		public Gui GUI => gui;
	}
}