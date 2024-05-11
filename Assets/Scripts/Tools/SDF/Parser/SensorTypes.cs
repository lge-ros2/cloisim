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

		// Description: For type "gaussian*", the mean of the Gaussian distribution from which noise values are drawn.
		public string type;

		// Description: For type "gaussian*", the standard deviation of the Gaussian distribution from which noise values are drawn.
		public double mean = 0;

		// Description: For type "gaussian*", the mean of the Gaussian distribution from which bias values are drawn.
		public double stddev = 0;

		// Description: For type "gaussian*", the standard deviation of the Gaussian distribution from which bias values are drawn.
		public double bias_mean = 0;

		// Description: For type "gaussian*", the standard deviation of the noise used to drive a process to model slow variations in a sensor bias.
		public double bias_stddev = 0;

		// Description: The type of noise. Currently supported types are: "none" (no noise). "gaussian" (draw noise values independently for each measurement from a Gaussian distribution). "gaussian_quantized" ("gaussian" plus quantization of outputs (ie. rounding))
		public double dynamic_bias_stddev = 0;

		// Description: For type "gaussian*", the correlation time in seconds of the noise used to drive a process to model slow variations in a sensor bias. A typical value, when used, would be on the order of 3600 seconds (1 hour).
		public double dynamic_bias_correlation_time = 0;

		// Description: For type "gaussian_quantized", the precision of output signals. A value of zero implies infinite precision / no quantization.
		public double precision = double.NaN;
	}

	// Description: These elements are specific to an air pressure sensor.
	public class AirPressure
	{
		// Description: The initial altitude in meters. This value can be used by a sensor implementation to augment the altitude of the sensor. For example, if you are using simulation instead of creating a 1000 m mountain model on which to place your sensor, you could instead set this value to 1000 and place your model on a ground plane with a Z height of zero.
		public double reference_altitude = 0;

		// Description: Noise parameters for the pressure data.
		public Noise pressure_noise = null;
	}

	// Description: These elements are specific to an altimeter sensor.
	public class Altimeter
	{
		// Description: Noise parameters for vertical position
		public Noise vertical_position_noise = null;

		// Description: Noise parameters for vertical velocity
		public Noise vertical_velocity_noise = null;
	}

	public class Cameras : SensorType
	{
		public List<Camera> cameras = new List<Camera>();

		public void Add(in Camera item)
		{
			cameras.Add(item);
		}
	}

	public class Camera : SensorType
	{
		public Pose<double> Pose => pose;

		public void ParsePose(in string poseString, in string relativeTo = "")
		{
			pose.relative_to = (string.IsNullOrEmpty(relativeTo)) ? "__model__" : relativeTo;

			if (!string.IsNullOrEmpty(poseString))
			{
				// x y z roll pitch yaw
				var poseInfo = poseString.Replace('\t', ' ').Split(' ', System.StringSplitOptions.RemoveEmptyEntries);

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

		// Description: The image size in pixels and format.
		public class Image
		{
			// Description: Width in pixels
			public int width = 320;
			// Description: Height in pixels
			public int height = 240;
			// Description: (L8|L16|R_FLOAT16|R_FLOAT32|R8G8B8|B8G8R8|BAYER_RGGB8|BAYER_BGGR8|BAYER_GBRG8|BAYER_GRBG8)
			public string format = "R8G8B8";
		}

		public double horizontal_fov = 1.047f;

		public Image image = new Image();

		public Clip clip = new Clip();

		public bool save_enabled = false;
		public string save_path = string.Empty;

		public string depth_camera_output = string.Empty;
		public Clip depth_camera_clip = new Clip();

		#region SDF 1.9 feature
		// Description: The segmentation type of the segmentation camera. Valid options are: - semantic: Semantic segmentation, which provides 2 images: 1. A grayscale image, with the pixel values representing the label of an object 2. A colored image, with the pixel values being a unique color for each label - panoptic | instance: Panoptic segmentation, which provides an image where each pixel has 1 channel for label value of the object and 2 channels for the number of the instances of that label, and a colored image which its pixels have a unique color for each instance.
		public string segmentation_type = "semantic";

		// Description: The boundingbox type of the boundingbox camera. Valid options are: - 2d | visible_2d | visible_box_2d: a visible 2d box mode which provides axis aligned 2d boxes on the visible parts of the objects - full_2d | full_box_2d: a full 2d box mode which provides axis aligned 2d boxes that fills the object dimentions, even if it has an occluded part - 3d: a 3d mode which provides oriented 3d boxes
		public string box_type = "2d";
		#endregion

		public Noise noise = null;

		public Distortion distortion = null;

		public Lens lens = null;

		// Description: Visibility mask of a camera. When (camera's visibility_mask & visual's visibility_flags) evaluates to non-zero, the visual will be visible to the camera.
		public uint visibility_mask = 4294967295;

		protected Pose<double> pose = new Pose<double>();
	}

	public class Contact : SensorType
	{
		public string collision;
		public string topic;
	}

	// same as <GPS>
	public class NavSat : SensorType
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

		public class NoiseDirection
		{
			public Noise x = null;
			public Noise y = null;
			public Noise z = null;
		}

		public OrientationReferenceFrame orientation_reference_frame = new OrientationReferenceFrame();

		public NoiseDirection noise_angular_velocity = new NoiseDirection();
		public NoiseDirection noise_linear_acceleration = new NoiseDirection();

		// Descripotion: Some IMU sensors rely on external filters to produce orientation estimates. True to generate and output orientation data, false to disable orientation data generation.
		public bool enable_orientation = true;
	}


	// Description: These elements are specific to logical camera sensors. A logical camera reports objects that fall within a frustum. Computation should be performed on the CPU.
	public class LogicalCamera : SensorType
	{
		// Description: Near clipping distance of the view frustum
		public double near = 0;
		// Description: Far clipping distance of the view frustum
		public double far = 1;
		// Description: Aspect ratio of the near and far planes. This is the width divided by the height of the near or far planes.
		public double aspect_ratio = 1;
		// Description: Horizontal field of view of the frustum, in radians. This is the angle between the frustum's vertex and the edges of the near or far plane.
		public double horizontal_fov = 1;
	}

	public class Magnetometer : SensorType
	{
		public Noise x = null;
		public Noise y = null;
		public Noise z = null;
	}

	// same as <Ray>
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

	public class RfidTag : SensorType
	{
	}

	public class Rfid : SensorType
	{
	}

	public class Sonar : SensorType
	{
		public string geometry = "cone";
		public double min = 0;
		public double max = 1;
		public double radius = 0.5f;
	}

	// Description: These elements are specific to a wireless transceiver.
	public class transceiver : SensorType
	{

		// Description: Service set identifier (network name)
		public string essid = "wireless";

		// Description: Specifies the frequency of transmission in MHz
		public double frequency = 2442;

		// Description: Only a frequency range is filtered. Here we set the lower bound (MHz).
		public double min_frequency = 2412;

		// Description: Only a frequency range is filtered. Here we set the upper bound (MHz).
		public double max_frequency = 2484;

		// Description: Specifies the antenna gain in dBi
		public double gain = 2.5;

		// Description: Specifies the transmission power in dBm
		public double power = 14.5;

		// Description: Mininum received signal power in dBm
		public double sensitivity = -90;
	}

	// Description: These elements are specific to the force torque sensor.
	public class ForceTorque
	{
		// Description: Frame in which to report the wrench values. Currently supported frames are: "parent" report the wrench expressed in the orientation of the parent link frame, "child" report the wrench expressed in the orientation of the child link frame, "sensor" report the wrench expressed in the orientation of the joint sensor frame. Note that for each option the point with respect to which the torque component of the wrench is expressed is the joint origin.
		public string frame = "child";

		// Description: Direction of the wrench measured by the sensor. The supported options are: "parent_to_child" if the measured wrench is the one applied by the parent link on the child link, "child_to_parent" if the measured wrench is the one applied by the child link on the parent link.
		public string measure_direction = "child_to_parent";
	}
}