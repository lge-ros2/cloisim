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
		private bool always_on = false;
		private double update_rate = 0.0;
		private bool visualize = false;
		private string topic = "__default__";

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
				case "lidar":
				case "gpu_ray":
				case "gpu_lidar":
					if (IsValidNode("ray"))
					{
						sensor = ParseRay();
					}
					break;

				case "multicamera":
					if (IsValidNode("camera"))
					{
						var cameras = new Cameras();
						cameras.name = "multiple_camera";

						var nodes = GetNodes("camera");
						// Console.WriteLine("totalCamera: " + nodes.Count);

						for (var index = 1; index <= nodes.Count; index++)
						{
							cameras.list.Add(ParseCamera(index));
						}

						sensor = cameras;
					}
					break;

				case "camera":
				case "depth":
				case "wideanglecamera":
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
					if (IsValidNode("imu"))
					{
						sensor = ParseIMU();
					}
					break;

				case "gps":
					if (IsValidNode("gps"))
					{
						sensor = ParseGPS();
					}
					break;

				case "contact":
					if (IsValidNode("contact"))
					{
						sensor = ParseContact();
					}
					break;

				default:
					Console.WriteLine("Not supported sensor type!!!!! => " + Type);
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
				Console.WriteLine("sensor was not created!");
			}

			plugins = new Plugins(root);
		}
	}
}
