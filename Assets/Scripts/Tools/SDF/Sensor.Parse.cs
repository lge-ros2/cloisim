/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

namespace SDF
{
	public partial class Sensor : Entity
	{
		private Ray ParseRay()
		{
			var ray = new Ray();

			if (IsValidNode("ray/scan"))
			{
				ray.horizontal.samples = GetValue<uint>("ray/scan/horizontal/samples");
				ray.horizontal.resolution = GetValue<double>("ray/scan/horizontal/resolution");
				ray.horizontal.min_angle = GetValue<double>("ray/scan/horizontal/min_angle");
				ray.horizontal.max_angle = GetValue<double>("ray/scan/horizontal/max_angle");

				if (ray.horizontal.max_angle < ray.horizontal.min_angle)
				{
					Console.WriteLine("Must be greater or equal to min_angle");
				}

				if (IsValidNode("ray/scan/vertical"))
				{
					ray.vertical.samples = GetValue<uint>("ray/scan/vertical/samples");
					ray.vertical.resolution = GetValue<double>("ray/scan/vertical/resolution");
					ray.vertical.min_angle = GetValue<double>("ray/scan/vertical/min_angle");
					ray.vertical.max_angle = GetValue<double>("ray/scan/vertical/max_angle");

					if (ray.vertical.samples == 0)
					{
						Console.WriteLine("vertical sample cannot be zero");
					}

					if (ray.vertical.max_angle < ray.vertical.min_angle)
					{
						Console.WriteLine("Must be greater or equal to min_angle");
					}
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
				imu.angular_velocity_x.type = GetAttributeInPath<string>("imu/angular_velocity/x/noise", "type");
				imu.angular_velocity_x.mean = 0;
				imu.angular_velocity_x.stddev = 0;
				imu.angular_velocity_x.bias_mean = 0;
				imu.angular_velocity_x.bias_stddev = 0;
				imu.angular_velocity_x.dynamic_bias_stddev = 0;
				imu.angular_velocity_x.dynamic_bias_correlation_time = 0;
				imu.angular_velocity_x.precision = 0;

				imu.angular_velocity_y.type = GetAttributeInPath<string>("imu/angular_velocity/y/noise", "type");
				imu.angular_velocity_z.type = GetAttributeInPath<string>("imu/angular_velocity/z/noise", "type");
			}

			if (IsValidNode("imu/linear_acceleration"))
			{
				imu.linear_acceleration_x.type = GetAttributeInPath<string>("imu/linear_acceleration/x/noise", "type");
				imu.linear_acceleration_x.mean = 0;
				imu.linear_acceleration_x.stddev = 0;
				imu.linear_acceleration_x.bias_mean = 0;
				imu.linear_acceleration_x.bias_stddev = 0;
				imu.linear_acceleration_x.dynamic_bias_stddev = 0;
				imu.linear_acceleration_x.dynamic_bias_correlation_time = 0;
				imu.linear_acceleration_x.precision = 0;

				imu.linear_acceleration_y.type = GetAttributeInPath<string>("imu/linear_acceleration/y/noise", "type");
				imu.linear_acceleration_z.type = GetAttributeInPath<string>("imu/linear_acceleration/z/noise", "type");
			}

			return imu;
		}

		private GPS ParseGPS()
		{
			var gps = new GPS();

			if (IsValidNode("gps/position_sensing/horizontal/noise"))
			{
				gps.position_sensing_horizontal_noise.type = GetAttributeInPath<string>("gps/position_sensing/horizontal/noise", "type");
				gps.position_sensing_horizontal_noise.mean = 0;
				gps.position_sensing_horizontal_noise.stddev = 0;
				gps.position_sensing_horizontal_noise.bias_mean = 0;
				gps.position_sensing_horizontal_noise.bias_stddev = 0;
				gps.position_sensing_horizontal_noise.dynamic_bias_stddev = 0;
				gps.position_sensing_horizontal_noise.dynamic_bias_correlation_time = 0;
				gps.position_sensing_horizontal_noise.precision = 0;
			}

			if (IsValidNode("gps/position_sensing/vertical/noise"))
			{
				gps.position_sensing_vertical_noise.type = GetAttributeInPath<string>("gps/position_sensing/vertical/noise", "type");
				gps.position_sensing_vertical_noise.mean = 0;
				gps.position_sensing_vertical_noise.stddev = 0;
				gps.position_sensing_vertical_noise.bias_mean = 0;
				gps.position_sensing_vertical_noise.bias_stddev = 0;
				gps.position_sensing_vertical_noise.dynamic_bias_stddev = 0;
				gps.position_sensing_vertical_noise.dynamic_bias_correlation_time = 0;
				gps.position_sensing_vertical_noise.precision = 0;
			}

			if (IsValidNode("gps/velocity_sensing/horizontal/noise"))
			{
				gps.velocity_sensing_horizontal_noise.type = GetAttributeInPath<string>("gps/velocity_sensing/horizontal/noise", "type");
				gps.velocity_sensing_horizontal_noise.mean = 0;
				gps.velocity_sensing_horizontal_noise.stddev = 0;
				gps.velocity_sensing_horizontal_noise.bias_mean = 0;
				gps.velocity_sensing_horizontal_noise.bias_stddev = 0;
				gps.velocity_sensing_horizontal_noise.dynamic_bias_stddev = 0;
				gps.velocity_sensing_horizontal_noise.dynamic_bias_correlation_time = 0;
				gps.velocity_sensing_horizontal_noise.precision = 0;
			}

			if (IsValidNode("gps/velocity_sensing/vertical/noise"))
			{
				gps.velocity_sensing_vertical_noise.type = GetAttributeInPath<string>("gps/velocity_sensing/vertical/noise", "type");
				gps.velocity_sensing_vertical_noise.mean = 0;
				gps.velocity_sensing_vertical_noise.stddev = 0;
				gps.velocity_sensing_vertical_noise.bias_mean = 0;
				gps.velocity_sensing_vertical_noise.bias_stddev = 0;
				gps.velocity_sensing_vertical_noise.dynamic_bias_stddev = 0;
				gps.velocity_sensing_vertical_noise.dynamic_bias_correlation_time = 0;
				gps.velocity_sensing_vertical_noise.precision = 0;
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
	}
}