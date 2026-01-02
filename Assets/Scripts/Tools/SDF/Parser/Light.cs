/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Lights : Entities<Light>
	{
		private const string TARGET_TAG = "light";
		public Lights() : base(TARGET_TAG) { }
		public Lights(XmlNode _node) : base(_node, TARGET_TAG) { }
	}

	public class Light : Entity
	{
		// Description: Light attenuation
		public class Attenuation
		{
			// Description: Range of the light
			public double range = 10;

			// Description: The linear attenuation factor: 1 means attenuate evenly over the distance.
			public double linear = 1;

			// Description: The constant attenuation factor: 1.0 means never attenuate, 0.0 is complete attenutation.
			public double constant = 1;

			// Description: The quadratic attenuation factor: adds a curvature to the attenuation.
			public double quadratic = 0;
		}

		// Description: Spot light parameters
		public class Spot
		{
			// Description: Angle covered by the bright inner cone
			public double inner_angle = 0;

			// Description: Angle covered by the outer cone
			public double outer_angle = 0;

			// Description: The rate of falloff between the inner and outer cones. 1.0 means a linear falloff, less means slower falloff, higher means faster falloff.
			public double falloff = 0;
		}

		public bool cast_shadow = false;
		
		// TODO: Since <intensity> element was introduced from SDF 1.8, the intensity may not exist.		
		// Description: Scale factor to set the relative power of a light.
		public double intensity = 1;
		public Color diffuse = new Color(1, 1, 1, 1);
		public Color specular = new Color(0.1, 0.1, 0.1, 1.0);

		public Attenuation attenuation = null;

		public Vector3<double> direction = new Vector3<double>();

		public Spot spot = null;

		// attributes: name
		// Description: A unique name for the light.
		// attributes: type
		// Description: The light type: point, directional, spot.

		public Light(XmlNode _node)
			: base(_node, "__default__", "point")
		{
		}

		protected override void ParseElements()
		{
			cast_shadow = GetValue<bool>("cast_shadows");

			if (IsValidNode("intensity"))
			{
				intensity = GetValue<double>("intensity");
			}

			if (IsValidNode("diffuse"))
			{
				diffuse.FromString(GetValue<string>("diffuse"));
			}

			if (IsValidNode("specular"))
			{
				specular.FromString(GetValue<string>("specular"));
			}

			if (IsValidNode("attenuation"))
			{
				attenuation = new Attenuation();
				attenuation.range = GetValue<double>("attenuation/range");
				attenuation.linear = GetValue<double>("attenuation/linear");
				attenuation.constant = GetValue<double>("attenuation/constant");
				attenuation.quadratic = GetValue<double>("attenuation/quadratic");
			}

			if (IsValidNode("direction"))
			{
				direction.FromString(GetValue<string>("direction"));
			}

			if (IsValidNode("spot"))
			{
				spot = new Spot();
				spot.inner_angle = GetValue<double>("spot/inner_angle");
				spot.outer_angle = GetValue<double>("spot/outer_angle");
				spot.falloff = GetValue<double>("spot/falloff");
			}
		}
	}
}