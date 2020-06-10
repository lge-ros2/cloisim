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

		// <topic> : TBD

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
			ParseElements();
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
