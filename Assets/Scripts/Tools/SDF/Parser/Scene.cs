/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Scene
	{
		private XmlNode root = null;

		// <ambient> : TBD
		// <background> : TBD
		// <sky> : TBD
		// <shadows> : TBD
		// <fog> : TBD
		// <grid> : TBD
		// <origin_visual> : TBD

		public Scene(XmlNode _node)
		{
			root = _node;
		}
	}
}