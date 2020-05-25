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
	public class Sensors : Entities<Sensor>
	{
		private const string TARGET_TAG = "sensor";
		public Sensors() : base(TARGET_TAG) { }
		public Sensors(XmlNode _node) : base(_node, TARGET_TAG) { }
	}

	public class Sensor : Entity
	{
		private bool always_on = false;
		private double update_rate = 0.0;
		private bool visualize = false;

		// <topic> : TBD

		private SensorType sensor = null;
		private Plugins plugins;

		public double UpdateRate()
		{
			return update_rate;
		}

		public bool Visualize()
		{
			return visualize;
		}

		public SensorType GetSensor()
		{
			return sensor;
		}

		public List<Plugin> GetPlugins()
		{
			return plugins.GetData();
		}

		public Sensor(XmlNode node)
			: base(node)
		{
			ParseElements();
		}

		private Ray ParseRay()
		{
			var ray = new Ray();

			var pose = GetValue<string>("ray/pose");
			var relative_to = GetAttributeInPath<string>("ray/pose", "relative_to");

			ray.ParsePose(pose, relative_to);

			if (IsValidNode("ray/scan"))
			{
				ray.horizontal.samples = GetValue<uint>("ray/scan/horizontal/samples");
				ray.horizontal.resolution = GetValue<double>("ray/scan/horizontal/resolution");
				ray.horizontal.min_angle = GetValue<double>("ray/scan/horizontal/min_angle");
				ray.horizontal.max_angle = GetValue<double>("ray/scan/horizontal/max_angle");

				if (ray.horizontal.max_angle < ray.horizontal.min_angle)
					Console.WriteLine("Must be greater or equal to min_angle");

				if (IsValidNode("ray/scan/vertical"))
				{
					ray.vertical.samples = GetValue<uint>("ray/scan/vertical/samples");
					ray.vertical.resolution = GetValue<double>("ray/scan/vertical/resolution");
					ray.vertical.min_angle = GetValue<double>("ray/scan/vertical/min_angle");
					ray.vertical.max_angle = GetValue<double>("ray/scan/vertical/max_angle");

					if (ray.vertical.samples == 0)
						Console.WriteLine("vertical sample cannot be zero");

					if (ray.vertical.max_angle < ray.vertical.min_angle)
						Console.WriteLine("Must be greater or equal to min_angle");
				}
			}

			ray.range_min = GetValue<double>("ray/range/min");
			ray.range_max = GetValue<double>("ray/range/max"); ;
			ray.range_resolution = GetValue<double>("ray/range/resolution"); ;

			if (IsValidNode("ray/noise"))
			{
				ray.noise_type = GetValue<string>("ray/noise/type");
				ray.noise_mean = GetValue<double>("ray/noise/mean");
				ray.noise_stddev = GetValue<double>("ray/noise/stddev"); ;
			}

			// Console.WriteLine("[{0}] {1} ", GetType().Name, root.InnerXml);
			// Console.WriteLine("[{0}] samples: {1} ", GetType().Name, ray.scan_horizontal_sample);

			return ray;
		}

		private Camera ParseCamera(in int index = 1)
		{
			var cameraElement = "camera["+ index +"]";
			var camera = new Camera();

			var pose = GetValue<string>(cameraElement + "/pose");
			var relative_to = GetAttributeInPath<string>(cameraElement + "/pose", "relative_to");

			camera.ParsePose(pose, relative_to);

			camera.name = GetAttributeInPath<string>(cameraElement, "name");
			if (string.IsNullOrEmpty(camera.name))
			{
				camera.name = Name;
			}

			camera.type = Type;
			camera.horizontal_fov = GetValue<double>(cameraElement + "/horizontal_fov");
			camera.image_width = GetValue<int>(cameraElement + "/image/width");
			camera.image_height = GetValue<int>(cameraElement + "/image/height");
			camera.image_format = GetValue<string>(cameraElement + "/image/format");
			camera.clip.near = GetValue<double>(cameraElement + "/clip/near");
			camera.clip.far = GetValue<double>(cameraElement + "/clip/far");

			if (IsValidNode(cameraElement + "/save"))
			{
				camera.save_enabled = GetAttributeInPath<bool>(cameraElement + "/save", "enabled", false);
				camera.save_path = GetValue<string>(cameraElement + "/save/path");
			}

			if (IsValidNode(cameraElement + "/depth_camera"))
			{
				camera.depth_camera_output = GetValue<string>(cameraElement + "/depth_camera/output");
				camera.depth_camera_clip.near = GetValue<double>(cameraElement + "/depth_camera/clip/near");
				camera.depth_camera_clip.far = GetValue<double>(cameraElement + "/depth_camera/clip/far");
			}

			if (IsValidNode(cameraElement + "/noise"))
			{
				camera.noise.type = GetValue<string>(cameraElement + "/noise/type");
				camera.noise.mean = GetValue<double>(cameraElement + "/noise/mean");
				camera.noise.stddev = GetValue<double>(cameraElement + "/noise/stddev");
			}

			if (IsValidNode(cameraElement + "/distortion"))
			{
				camera.distortion.k1 = GetValue<double>(cameraElement + "/distortion/k1");
				camera.distortion.k2 = GetValue<double>(cameraElement + "/distortion/k2");
				camera.distortion.k3 = GetValue<double>(cameraElement + "/distortion/k3");
				camera.distortion.p1 = GetValue<double>(cameraElement + "/distortion/p1");
				camera.distortion.p2 = GetValue<double>(cameraElement + "/distortion/p2");
				camera.distortion.center.SetByString(GetValue<string>(cameraElement + "/distortion/center"));
			}

			if (IsValidNode(cameraElement + "/lens"))
			{
				camera.lens.type = GetValue<string>(cameraElement + "/lens/type");
				camera.lens.scale_to_hfov = GetValue<bool>(cameraElement + "/lens/scale_to_hfov");

				if (IsValidNode(cameraElement + "/lens/custom_function"))
				{
					camera.lens.custom_function.c1 = GetValue<double>(cameraElement + "/lens/custom_function/c1");
					camera.lens.custom_function.c2 = GetValue<double>(cameraElement + "/lens/custom_function/c2");
					camera.lens.custom_function.c3 = GetValue<double>(cameraElement + "/lens/custom_function/c3");
					camera.lens.custom_function.f = GetValue<double>(cameraElement + "/lens/custom_function/f");
					camera.lens.custom_function.fun = GetValue<string>(cameraElement + "/lens/custom_function/fun");
				}

				camera.lens.cutoff_angle = GetValue<double>(cameraElement + "/lens/cutoff_angle");
				camera.lens.env_texture_size = GetValue<int>(cameraElement + "/lens/env_texture_size");

				if (IsValidNode(cameraElement + "/lens/intrinsics"))
				{
					camera.lens.intrinsics.fx = GetValue<double>(cameraElement + "/lens/intrinsics/fx");
					camera.lens.intrinsics.fy = GetValue<double>(cameraElement + "/lens/intrinsics/fy");
					camera.lens.intrinsics.cx = GetValue<double>(cameraElement + "/lens/intrinsics/cx");
					camera.lens.intrinsics.cy = GetValue<double>(cameraElement + "/lens/intrinsics/cy");
					camera.lens.intrinsics.s = GetValue<double>(cameraElement + "/lens/intrinsics/s");
				}
			}
			else
			{
				if (Type.Equals("wideanglecamera"))
				{
					Console.WriteLine("'wideanglecamera' type needs <lens> element!!!");
				}
			}

			return camera;
		}

		private Sonar ParseSonar()
		{
			var sonar = new Sonar();

			var pose = GetValue<string>("sonar/pose");
			var relative_to = GetAttributeInPath<string>("sonar/pose", "relative_to");

			sonar.ParsePose(pose, relative_to);

			if (IsValidNode("sonar/geometry"))
			{
				sonar.geometry = GetValue<string>("sonar/geometry");
			}

			sonar.min = GetValue<double>("sonar/min");
			sonar.max = GetValue<double>("sonar/max");

			if (IsValidNode("sonar/radius"))
			{
				sonar.radius = GetValue<double>("sonar/radius");
			}
			return sonar;
		}

		private IMU ParseIMU()
		{
			var imu = new IMU();

			var pose = GetValue<string>("imu/pose");
			var relative_to = GetAttributeInPath<string>("imu/pose", "relative_to");

			imu.ParsePose(pose, relative_to);

			if (IsValidNode("imu/orientation_reference_frame"))
			{
				Console.WriteLine("Not supported imue orientation_reference_frame!!!");
			}

			if (IsValidNode("imu/angular_velocity"))
			{
				imu.angular_velocity_x.type = GetAttributeInPath<string>("imu/angular_velocity/x/noise", "type");
				imu.angular_velocity_y.type = GetAttributeInPath<string>("imu/angular_velocity/y/noise", "type");
				imu.angular_velocity_z.type = GetAttributeInPath<string>("imu/angular_velocity/z/noise", "type");
			}

			if (IsValidNode("imu/linear_acceleration"))
			{
				imu.linear_acceleration_x.type = GetAttributeInPath<string>("imu/linear_acceleration/x/noise", "type");
				imu.linear_acceleration_y.type = GetAttributeInPath<string>("imu/linear_acceleration/y/noise", "type");
				imu.linear_acceleration_z.type = GetAttributeInPath<string>("imu/linear_acceleration/z/noise", "type");
			}
			return imu;
		}

		protected override void ParseElements()
		{
			always_on = GetValue<bool>("always_on");
			update_rate = GetValue<double>("update_rate");
			visualize = GetValue<bool>("visualize");

			// Console.WriteLine("[{0}] P:{1} C:{2}", GetType().Name, parent, child);

			if (IsValidNode("ray") && (Type.Equals("gpu_ray") || Type.Equals("ray")))
			{
				sensor = ParseRay();
			}
			else if (IsValidNode("camera"))
			{
				if (Type.Equals("multicamera"))
				{
					Cameras cameras = new Cameras();
					cameras.name = "multiple_camera";

					var nodes = GetNodes("camera");
					// Console.WriteLine("totalCamera: " + nodes.Count);

					for (int index = 1; index <= nodes.Count; index++)
					{
						cameras.list.Add(ParseCamera(index));
					}

					sensor = cameras;
				}
				else if (Type.Equals("camera") || Type.Equals("depth") || Type.Equals("wideanglecamera"))
				{
					sensor = ParseCamera();
				}
				else
				{
					Console.WriteLine("Not supported camera type !? => " + Type);
				}
			}
			else if (IsValidNode("sonar") && Type.Equals("sonar"))
			{
				sensor = ParseSonar();
			}
			else if (IsValidNode("imu") && Type.Equals("imu"))
			{
				sensor = ParseIMU();
			}
			else
			{
				Console.WriteLine("Not supported sensor type!!!!! => " + Type);
			}


			// Set common
			try
			{
				sensor.name = Name;
				sensor.type	= Type;
				// Console.WriteLine("Sensor {0}::{1} was created!", Name, Type);
			}
			catch
			{
				Console.WriteLine("sensor was not created!");
			}

			plugins = new Plugins(root);
		}
	}
}
