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
			ParseElements();
		}


		protected override void ParseElements()
		{
			if (root == null)
				return;

			cast_shadow = GetValue<bool>("cast_shadows");

			if (IsValidNode("diffuse"))
				diffuse.SetByString(GetValue<string>("diffuse"));

			if (IsValidNode("specular"))
				diffuse.SetByString(GetValue<string>("specular"));

			if (IsValidNode("direction"))
				direction.SetByString(GetValue<string>("direction"));
		}
	}
}