/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Scene : Entity
	{
		public class Sky
		{
			public class Clouds
			{
				// Description: Speed of the clouds
				public double speed = 0.59999999999999998;

				// Description: Direction of the cloud movement
				public double direction = 0;

				// Description: Density of clouds
				public double humidity = 0.5;

				// Description: Average size of the clouds
				public double mean_size = 0.5;

				// Description: Ambient cloud color
				public Color ambient = new Color(0.8, 0.8, 0.8, 1);
			}
			public double time = 10;
			public double sunrise = 6;
			public double sunset = 20;

			public Clouds clouds = null;
		}

		public class Fog
		{
			// Description: Fog color
			public Color color = new Color(1, 1, 1, 1);

			// Description: Fog type: constant, linear, quadratic
			public string type = "none";

			// Description: Distance to start of fog
			public double start = 1;

			// Description: Distance to end of fog
			public double end = 100;

			// Description: Density of fog
			public double density = 1;
		}

		public Color ambient = new Color(0.4, 0.4, 0.4, 1);
		public Color background = new Color(0.7, 0.7, 0.7, 1);
		public Sky sky = null;
		public bool shadows = true;
		public Fog fog = null;
		public bool grid = true;
		public bool origin_visual = true;

		public Scene(XmlNode _node)
			: base(_node)
		{
		}
	}
}