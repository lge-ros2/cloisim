/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

namespace SDF
{
	public class SensorType
	{
		public string name = string.Empty;
		public string type = string.Empty;
	}

	public class Noise
	{
		public Noise(in string type = "none")
		{
			this.type = type;
		}

		public string type;
		public double mean = 0;
		public double stddev = 0;
		public double bias_mean = 0;
		public double bias_stddev = 0;
		public double dynamic_bias_stddev = 0;
		public double dynamic_bias_correlation_time = 0;
		public double precision = double.NaN;
	}

	// <altimeter> : TBD

	public class Cameras : SensorType
	{
		public List<Camera> cameras = new List<Camera>();

		public void	Add(in Camera item)
		{
			cameras.Add(item);
		}
	}

	public class Camera : SensorType
	{
		protected Pose<double> pose = new Pose<double>();

		public Pose<double> Pose => pose;

		public void ParsePose(in string poseString, in string relativeTo = "")
		{
			pose.relative_to = (string.IsNullOrEmpty(relativeTo)) ? "__model__" : relativeTo;

			if (!string.IsNullOrEmpty(poseString))
			{
				// x y z roll pitch yaw
				var poseInfo = poseString.Split(' ');

				pose.Pos.Set(poseInfo[0], poseInfo[1], poseInfo[2]);
				pose.Rot.Set(poseInfo[3], poseInfo[4], poseInfo[5]);

				// Console.WriteLine("Pose {0} {1} {2} {3} {4} {5}",
				// 	pose.Pos.X, pose.Pos.Y, pose.Pos.Z,
				// 	pose.Rot.Roll, pose.Rot.Pitch, pose.Rot.Yaw
				// );
			}
		}

		public struct Clip
		{
			public Clip(in double near = 0.1d, in double far = 100d)
			{
				this.near = near;
				this.far = far;
			}

			public double near;
			public double far;
		}

		public class Distortion
		{
			public double k1 = 0;
			public double k2 = 0;
			public double k3 = 0;
			public double p1 = 0;
			public double p2 = 0;
			public SDF.Vector2<double> center = new SDF.Vector2<double>();
		}

		public class Lens
		{
			public class CustomFunction
			{
				public double c1 = 1;
				public double c2 = 1;
				public double c3 = 0;
				public double f = 1;
				public string fun = "tan";
			}

			public class Intrinsics
			{
				public double fx = 277;
				public double fy = 277;
				public double cx = 160;
				public double cy = 120;
				public double s = 0;
			}

			public string type = "stereographic";
			public bool scale_to_hfov = true;
			public CustomFunction custom_function = new CustomFunction();
			public double cutoff_angle = 1.5707f;
			public int env_texture_size = 256;

			public Intrinsics intrinsics = new Intrinsics();
		}

		public double horizontal_fov = 1.047f;

		public int image_width = 320;
		public int image_height = 240;
		public string image_format = "R8G8B8";

		public Clip clip = new Clip();

		public bool save_enabled = false;
		public string save_path = string.Empty;

		public string depth_camera_output = string.Empty;
		public Clip depth_camera_clip = new Clip();

		public Noise noise = null;

		public Distortion distortion = null;

		public Lens lens = null;
	}

	public class Contact : SensorType
	{
		public List<string> collision = new List<string>();
		public string topic;
	}

	public class GPS : SensorType
	{
		public class SensingNoise
		{
			public Noise horizontal_noise = null;
			public Noise vertical_noise = null;
		}

		public SensingNoise position_sensing = null;

		public SensingNoise velocity_sensing = null;
	}

	public class IMU : SensorType
	{
		public class OrientationReferenceFrame
		{
			public string localization = "CUSTOM";
			public SDF.Vector3<double> custom_rpy;
			public string custom_rpy_parent_frame;
			public SDF.Vector3<int> grav_dir_x;
			public string grav_dir_x_parent_frame;
		}

		public OrientationReferenceFrame orientation_reference_frame = new OrientationReferenceFrame();

		public Noise angular_velocity_x_noise = null;
		public Noise angular_velocity_y_noise = null;
		public Noise angular_velocity_z_noise = null;
		public Noise linear_acceleration_x_noise = null;
		public Noise linear_acceleration_y_noise = null;
		public Noise linear_acceleration_z_noise = null;
	}


	// <logical_camera> : TBD

	public class Magnetometer : SensorType
	{
		public Noise x = null;
		public Noise y = null;
		public Noise z = null;
	}

	public class Lidar : SensorType
	{
		public class Scan
		{
			public Scan(uint samples = 640)
			{
				this.samples = samples;
			}
			public uint samples;
			public double resolution = 1;
			public double min_angle = 0.0;
			public double max_angle = 0.0;
		}

		public class Range
		{
			public double min = 0;
			public double max = 0;
			public double resolution = 0;
		}

		public Scan horizontal = new Scan(640);
		public Scan vertical = null;
		public Range range = new Range();
		public Noise noise = null;
	}

	// <rfidtag> : TBD
	// <rfid> : TBD

	public class Sonar: SensorType
	{
		public string geometry = "cone";
		public double min = 0;
		public double max = 1;
		public double radius = 0.5f;
	}

	// <transceiver> : TBD
	// <force_torque> : TBD
}