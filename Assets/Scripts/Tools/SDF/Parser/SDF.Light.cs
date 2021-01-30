/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Light : Entity
	{
		private bool cast_shadow = false;
		public Color diffuse = new Color(1, 1, 1, 1);
		public Color specular = new Color(0.1, 0.1, 0.1, 1.0);

		// <attenuation> : TBD

		private Vector3<int> direction = new Vector3<int>();

		// <spot> : TBD

		public Light(XmlNode _node)
			: base(_node)
		{
			if (root != null)
			{
				ParseElements();
			}
		}


		protected override void ParseElements()
		{
			cast_shadow = GetValue<bool>("cast_shadows");

			if (IsValidNode("diffuse"))
				diffuse.FromString(GetValue<string>("diffuse"));

			if (IsValidNode("specular"))
				diffuse.FromString(GetValue<string>("specular"));

			if (IsValidNode("direction"))
				direction.FromString(GetValue<string>("direction"));
		}
	}
}