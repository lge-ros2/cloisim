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
		// Description: Global audio properties.
		public class Audio
		{
			// Description: Device to use for audio playback. A value of "default" will use the system's default audio device. Otherwise, specify a an audio device file"
			public string device = string.Empty;
		}

		// Description: The wind tag specifies the type and properties of the wind.
		public class Wind
		{
			// Description: Linear velocity of the wind.
			public Vector3<double> linear_velocity = new Vector3<double>();
		}

		// Description: The atmosphere tag specifies the type and properties of the atmosphere model.
		public class Atmosphere
		{
			// Description: The type of the atmosphere engine. Current options are adiabatic. Defaults to adiabatic if left unspecified.
			public string type = "adiabatic";

			// Description: Temperature at sea level in kelvins.
			public double temperature = 288.14999999999998;

			// Description: Pressure at sea level in pascals.
			public double pressure = 101325;

			// Description: Temperature gradient with respect to increasing altitude at sea level in units of K/m.
			public double temperature_gradient = -0.0064999999999999997;
		}

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

				public string view_controller = "orbit";

				// Description: Set the type of projection for the camera. Valid values are "perspective" and "orthographic".
				public string projection_type = "perspective";

				public TrackVisual track_visual = null;

				public Camera(XmlNode _node)
				: base(_node, "user_camera")
				{
				}

				protected override void ParseElements()
				{
					if (IsValidNode("projection_type"))
					{
						projection_type = GetValue<string>("projection_type");
					}
				}
			}

			public bool fullscreen;

			public Camera camera;

			// Description: A plugin is a dynamically loaded chunk of code. It can exist as a child of world, model, and sensor.
			private Plugins plugins;
		}

		public class Road : Entity
		{
			// Description: Width of the road
			public double width = 1;

			// Required: +
			// Description: A series of points that define the path of the road.
			// Default: 0 0 0
			public List<Vector3<double>> points = new List<Vector3<double>>();

			// Description: The material of the visual element.
			public Material material = null;

			public Road(in XmlNode _node)
				: base(_node)
			{
				// Description: Name of the road
				Name = GetValue<string>("name", "__default__");
				width = GetValue<double>("width", 1);
				// Console.Write(Name);
				// Console.Write(width);

				if (GetValues<string>("point", out var pointList))
				{
					foreach (var pointStr in pointList)
					{
						// Console.Write(pointStr);
						var point = new Vector3<double>(pointStr);
						points.Add(point);
					}
				}

				if (IsValidNode("material/script"))
				{
					material = new Material(GetNode("material"));
				}
			}
		}

		public class SphericalCoordinates
		{
			// Description: Name of planetary surface model, used to determine the surface altitude at a given latitude and longitude. The default is an ellipsoid model of the earth based on the WGS-84 standard. It is used in Gazebo's GPS sensor implementation.
			public string surface_model = "EARTH_WGS84";

			// Description: This field identifies how Gazebo world frame is aligned in Geographical sense. The final Gazebo world frame orientation is obtained by rotating a frame aligned with following notation by the field heading_deg (Note that heading_deg corresponds to positive yaw rotation in the NED frame, so it's inverse specifies positive Z-rotation in ENU or NWU). Options are: - ENU (East-North-Up) - NED (North-East-Down) - NWU (North-West-Up) For example, world frame specified by setting world_orientation="ENU" and heading_deg=-90° is effectively equivalent to NWU with heading of 0°.
			public string world_frame_orientation = "ENU";

			// Description: Geodetic latitude at origin of gazebo reference frame, specified in units of degrees.
			public double latitude_deg = 0;

			// Description: Longitude at origin of gazebo reference frame, specified in units of degrees.
			public double longitude_deg = 0;

			// Description: Elevation of origin of gazebo reference frame, specified in meters.
			public double elevation = 0;

			// Description: Heading offset of gazebo reference frame, measured as angle between Gazebo world frame and the world_frame_orientation type (ENU/NED/NWU). Rotations about the downward-vector (e.g. North to East) are positive. The direction of rotation is chosen to be consistent with compass heading convention (e.g. 0 degrees points North and 90 degrees points East, positive rotation indicates counterclockwise rotation when viewed from top-down direction). The angle is specified in degrees.
			public double heading_deg = 0;
		}

		// Required: *
		// Description: The population element defines how and where a set of models will be automatically populated in Gazebo.
		public class Population
		{
			// Description: Specifies the type of object distribution and its optional parameters.
			public class Distribution
			{
				// Description: Define how the objects will be placed in the specified region. - random: Models placed at random. - uniform: Models approximately placed in a 2D grid pattern with control over the number of objects. - grid: Models evenly placed in a 2D grid pattern. The number of objects is not explicitly specified, it is based on the number of rows and columns of the grid. - linear-x: Models evently placed in a row along the global x-axis. - linear-y: Models evently placed in a row along the global y-axis. - linear-z: Models evently placed in a row along the global z-axis.
				public string type = "random";

				// Description: Number of rows in the grid.
				public int rows = 1;

				// Description: Number of columns in the grid.
				public int cols = 1;

				// Description: Distance between elements of the grid.
				public Vector3<double> step = new Vector3<double>(0.5, 0.5, 0);
			}

			// Description: A unique name for the population. This name must not match another population in the world.
			public string name = "__default__";

			// Description: The number of models to place.
			public int model_count = 1;

			public Distribution distribution = new Distribution();

			// Description: Box shape
			public Box box;

			// Description: Cylinder shape
			public Cylinder cylinder;

			public Pose<double> pose = null;

			// Description: The model element defines a complete robot or any other physical object.
			// private Models models = null;
		}

		public Audio audio = null;
		public Wind wind = null;

		public Vector3<double> gravity = new Vector3<double>();

		// Description: The magnetic vector in Tesla, expressed in a coordinate frame defined by the spherical_coordinates tag.
		public Vector3<double> magnetic_field = new Vector3<double>(6e-06, 2.3e-05, -4.2e-05);

		public Atmosphere atmosphere = null;

		public Gui gui = null;

		private Physics physics = null;

		private Scene scene = null;

		private Lights lights = null;

		// <frame> : TBD

		private Models models = null;
		private Actors actors = null;

		private List<Road> _roads = new List<Road>();

		public SphericalCoordinates spherical_coordinates = null;

		// public List<State> states = null;

		// public List<Population> population = null;

		private Plugins plugins;

		public World(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			models = new Models(root);
			actors = new Actors(root);
			physics = new Physics(root);
			lights = new Lights(root);
			plugins = new Plugins(root);

			var gravityStr = (IsValidNode("gravity")) ? GetValue<string>("gravity") : "0 0 -9.8";
			gravity.FromString(gravityStr);

			// Console.WriteLine("{0}", GetType().Name);

			// Console.WriteLine("[{0}] {1} {2} {3} {4}", GetType().Name,
			// isStatic, isSelfCollide, allowAutoDisable, enableWind);

			if (IsValidNode("gui"))
			{
				gui = new Gui();
				gui.fullscreen = GetValue<bool>("gui/fullscreen", false);

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
				scene = new Scene(GetNode("scene"));
			}

			if (IsValidNode("frame"))
			{
				// Console.WriteLine("<frame> tag is NOT supported yet.");
			}

			foreach (XmlNode roadNode in GetNodes("road"))
			{
				var road = new Road(roadNode);
				_roads.Add(road);
			}

			if (IsValidNode("spherical_coordinates"))
			{
				spherical_coordinates = new SphericalCoordinates();

				if (IsValidNode("spherical_coordinates/surface_model"))
				{
					spherical_coordinates.surface_model = GetValue<string>("spherical_coordinates/surface_model");
				}

				if (IsValidNode("spherical_coordinates/world_frame_orientation"))
				{
					spherical_coordinates.world_frame_orientation = GetValue<string>("spherical_coordinates/world_frame_orientation");
				}

				if (IsValidNode("spherical_coordinates/latitude_deg"))
				{
					spherical_coordinates.latitude_deg = GetValue<double>("spherical_coordinates/latitude_deg");
				}

				if (IsValidNode("spherical_coordinates/longitude_deg"))
				{
					spherical_coordinates.longitude_deg = GetValue<double>("spherical_coordinates/longitude_deg");
				}

				if (IsValidNode("spherical_coordinates/elevation"))
				{
					spherical_coordinates.elevation = GetValue<double>("spherical_coordinates/elevation");
				}

				if (IsValidNode("spherical_coordinates/heading_deg"))
				{
					spherical_coordinates.heading_deg = GetValue<double>("spherical_coordinates/heading_deg");
				}
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

		public List<Light> GetLights()
		{
			return lights.GetData();
		}

		public List<Road> GetRoads()
		{
			return _roads;
		}
	}
}