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

	public partial class Sensor : Entity
	{
		// Description: If true the sensor will always be updated according to the update rate.
		private bool always_on = false;

		// Description: The frequency at which the sensor data is generated. If left unspecified, the sensor will generate data every cycle.
		private double update_rate = 0.0;

		// Description: If true, the sensor is visualized in the GUI
		private bool visualize = false;

		// Description: Name of the topic on which data is published. This is necessary for visualization
		private string topic = "__default__";

		// Description: If true, the sensor will publish performance metrics
		// private bool enable_metrics = false;

		private SensorType sensor = null;
		private Plugins plugins = null;

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
		}

		protected override void ParseElements()
		{
			always_on = GetValue<bool>("always_on");
			update_rate = GetValue<double>("update_rate");
			visualize = GetValue<bool>("visualize");
			topic = GetValue<string>("topic");

			// Console.WriteLine("[{0}] P:{1} C:{2}", GetType().Name, parent, child);

			switch (Type)
			{
				case "ray":
				case "gpu_ray":
				case "lidar":
				case "gpu_lidar":
					if (IsValidNode("lidar"))
					{
						sensor = ParseLidar();
					}
					else if (IsValidNode("ray"))
					{
						sensor = ParseLidar("ray");
					}
					break;

				case "rgbd_camera":
				case "rgbd":
				case "multicamera":
					if (IsValidNode("camera"))
					{
						var cameras = new Cameras();
						cameras.name = Type;
						cameras.type = Type;

						var nodes = GetNodes("camera");
						// Console.WriteLine("totalCamera: " + nodes.Count);

						for (var index = 1; index <= nodes.Count; index++)
							cameras.Add(ParseCamera(index));

						sensor = cameras;
					}
					break;

				case "camera":
				case "depth_camera":
				case "depth":
				case "segmentation_camera":
				case "segmentation":
					if (IsValidNode("camera"))
					{
						sensor = ParseCamera();
					}
					break;

				case "sonar":
					if (IsValidNode("sonar"))
					{
						sensor = ParseSonar();
					}
					break;

				case "imu":
					sensor = ParseIMU();
					break;

				case "gps":
				case "navsat":
					sensor = ParseNavSat(Type);
					break;

				case "contact":
					sensor = ParseContact();
					break;

				case "air_pressure":
				case "altimeter":
				case "force_torque":
				case "logical_camera":
				case "boundingbox_camera":
				case "magnetometer":
				case "rfid":
				case "rfidtag":
				case "thermal_camera":
				case "thermal":
				case "wireless_receiver":
				case "wireless_transmitter":
				case "custom":
					Console.WriteLine("Not supported sensor type!!!!! => " + Type);
					break;

				default:
					Console.WriteLine("Invalid sensor type!!!!! => " + Type);
					break;
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
				Console.WriteLine("Sensor {0}::{1} was NOT created!", Name, Type);
			}

			plugins = new Plugins(root);
		}
	}
}
