/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */


using System.IO;
using System;

namespace SDF
{
	public partial class Sensor : Entity
	{
		private Lidar ParseLidar(in string element_path = "lidar")
		{
			var lidar = new Lidar();

			if (IsValidNode(element_path + "/scan"))
			{
				lidar.horizontal.samples = GetValue<uint>(element_path + "/scan/horizontal/samples");
				lidar.horizontal.resolution = GetValue<double>(element_path + "/scan/horizontal/resolution");
				lidar.horizontal.min_angle = GetValue<double>(element_path + "/scan/horizontal/min_angle");
				lidar.horizontal.max_angle = GetValue<double>(element_path + "/scan/horizontal/max_angle");

				if (lidar.horizontal.max_angle < lidar.horizontal.min_angle)
				{
					Console.WriteLine("Must be greater or equal to min_angle");
				}

				if (IsValidNode(element_path + "/scan/vertical"))
				{
					lidar.vertical = new Lidar.Scan(1);
					lidar.vertical.samples = GetValue<uint>(element_path + "/scan/vertical/samples", 1);
					lidar.vertical.resolution = GetValue<double>(element_path + "/scan/vertical/resolution", 1);
					lidar.vertical.min_angle = GetValue<double>(element_path + "/scan/vertical/min_angle");
					lidar.vertical.max_angle = GetValue<double>(element_path + "/scan/vertical/max_angle");

					if (lidar.vertical.max_angle < lidar.vertical.min_angle)
					{
						Console.WriteLine("Must be greater or equal to min_angle");
					}
				}
			}

			if (IsValidNode(element_path + "/range"))
			{
				lidar.range.min = GetValue<double>(element_path + "/range/min");
				lidar.range.max = GetValue<double>(element_path + "/range/max"); ;
				lidar.range.resolution = GetValue<double>(element_path + "/range/resolution");
			}

			if (IsValidNode(element_path + "/noise"))
			{
				lidar.noise = new Noise();
				ParseNoise(ref lidar.noise, element_path);
			}

			// Console.WriteLine("[{0}] {1} ", GetType().Name, root.InnerXml);
			// Console.WriteLine("[{0}] samples: {1} ", GetType().Name, lidar.scan_horizontal_sample);

			return lidar;
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

			if (IsValidNode(cameraElement + "/clip/near"))
			{
				camera.clip.near = GetValue<double>(cameraElement + "/clip/near");
			}

			if (IsValidNode(cameraElement + "/clip/far"))
			{
				camera.clip.far = GetValue<double>(cameraElement + "/clip/far");
			}

			if (IsValidNode(cameraElement + "/save"))
			{
				camera.save_enabled = GetAttributeInPath<bool>(cameraElement + "/save", "enabled", false);
				camera.save_path = GetValue<string>(cameraElement + "/save/path");
			}

			if (IsValidNode(cameraElement + "/depth_camera"))
			{
				camera.depth_camera_output = GetValue<string>(cameraElement + "/depth_camera/output");

				if (IsValidNode(cameraElement + "/depth_camera/clip/near"))
				{
					camera.depth_camera_clip.near = GetValue<double>(cameraElement + "/depth_camera/clip/near");
				}

				if (IsValidNode(cameraElement + "/depth_camera/clip/far"))
				{
					camera.depth_camera_clip.far = GetValue<double>(cameraElement + "/depth_camera/clip/far");
				}
			}

			if (IsValidNode(cameraElement + "/noise"))
			{
				camera.noise = new Noise("gaussian");

				ParseNoise(ref camera.noise, cameraElement);
			}

			if (IsValidNode(cameraElement + "/distortion"))
			{
				camera.distortion = new Camera.Distortion();
				camera.distortion.k1 = GetValue<double>(cameraElement + "/distortion/k1");
				camera.distortion.k2 = GetValue<double>(cameraElement + "/distortion/k2");
				camera.distortion.k3 = GetValue<double>(cameraElement + "/distortion/k3");
				camera.distortion.p1 = GetValue<double>(cameraElement + "/distortion/p1");
				camera.distortion.p2 = GetValue<double>(cameraElement + "/distortion/p2");
				camera.distortion.center.FromString(GetValue<string>(cameraElement + "/distortion/center"));
			}

			if (IsValidNode(cameraElement + "/lens"))
			{
				camera.lens = new Camera.Lens();
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

			if (IsValidNode("imu/orientation_reference_frame"))
			{
				Console.WriteLine("Not supported imue orientation_reference_frame!!!");
			}

			if (IsValidNode("imu/angular_velocity"))
			{
				if (IsValidNode("imu/angular_velocity/x"))
				{
					imu.angular_velocity_x_noise = new Noise();
					ParseNoise(ref imu.angular_velocity_x_noise, "imu/angular_velocity/x");
				}

				if (IsValidNode("imu/angular_velocity/y"))
				{
					imu.angular_velocity_y_noise = new Noise();
					ParseNoise(ref imu.angular_velocity_y_noise, "imu/angular_velocity/y");
				}

				if (IsValidNode("imu/angular_velocity/z"))
				{
					imu.angular_velocity_z_noise = new Noise();
					ParseNoise(ref imu.angular_velocity_z_noise, "imu/angular_velocity/z");
				}
			}

			if (IsValidNode("imu/linear_acceleration"))
			{
				if (IsValidNode("imu/linear_acceleration/x"))
				{
					imu.linear_acceleration_x_noise = new Noise();
					ParseNoise(ref imu.linear_acceleration_x_noise, "imu/linear_acceleration/x");
				}

				if (IsValidNode("imu/linear_acceleration/y"))
				{
					imu.linear_acceleration_y_noise = new Noise();
					ParseNoise(ref imu.linear_acceleration_y_noise, "imu/linear_acceleration/y");
				}

				if (IsValidNode("imu/linear_acceleration/z"))
				{
					imu.linear_acceleration_z_noise = new Noise();
					ParseNoise(ref imu.linear_acceleration_z_noise, "imu/linear_acceleration/z");
				}
			}

			return imu;
		}

		private GPS ParseGPS()
		{
			var gps = new GPS();

			if (IsValidNode("gps/position_sensing"))
			{
				gps.position_sensing = new GPS.SensingNoise();

				if (IsValidNode("gps/position_sensing/horizontal/noise"))
				{
					gps.position_sensing.horizontal_noise = new Noise();
					ParseNoise(ref gps.position_sensing.horizontal_noise, "gps/position_sensing/horizontal");
				}

				if (IsValidNode("gps/position_sensing/vertical/noise"))
				{
					gps.position_sensing.vertical_noise = new Noise();
					ParseNoise(ref gps.position_sensing.vertical_noise, "gps/position_sensing/vertical");
				}
			}

			if (IsValidNode("gps/velocity_sensing"))
			{
				gps.velocity_sensing = new GPS.SensingNoise();

				if (IsValidNode("gps/velocity_sensing/horizontal/noise"))
				{
					gps.velocity_sensing.horizontal_noise = new Noise();
					ParseNoise(ref gps.velocity_sensing.horizontal_noise, "gps/velocity_sensing/horizontal");
				}

				if (IsValidNode("gps/velocity_sensing/vertical/noise"))
				{
					gps.velocity_sensing.vertical_noise = new Noise();
					ParseNoise(ref gps.velocity_sensing.vertical_noise, "gps/velocity_sensing/vertical");
				}
			}

			return gps;
		}

		private Contact ParseContact()
		{
			var contact = new Contact();

			if (GetValues<string>("contact/collision", out var collisionList))
			{
				contact.collision = collisionList;
			}

			contact.topic = GetValue<string>("contact/topic");

			return contact;
		}

		private void ParseNoise(ref Noise noise, in string targetPathName)
		{
			var noiseXPath = targetPathName + "/noise";

			var type = GetAttributeInPath<string>(noiseXPath, "type");
			noise.type = (type == null)? GetValue<string>(noiseXPath + "/type", "none"): type;
			noise.mean = GetValue<double>(noiseXPath + "/mean", 0);
			noise.stddev = GetValue<double>(noiseXPath + "/stddev", 0);
			noise.bias_mean = GetValue<double>(noiseXPath + "/bias_mean", 0);
			noise.bias_stddev = GetValue<double>(noiseXPath + "/bias_stddev", 0);
			noise.dynamic_bias_stddev = GetValue<double>(noiseXPath + "/dynamic_bias_stddev", 0);
			noise.dynamic_bias_correlation_time = GetValue<double>(noiseXPath + "/dynamic_bias_correlation_time", 0);

			if (IsValidNode(noiseXPath + "/precesion"))
			{
				noise.precision = GetValue<double>(noiseXPath + "/precesion", 0);
			}
		}
	}
}