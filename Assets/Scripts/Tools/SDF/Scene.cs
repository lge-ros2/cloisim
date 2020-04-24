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

		// ambient
		// background
		// sky
		// shadows
		// fog
		// grid
		// origin_visual

		public Scene(XmlNode _node)
		{
			root = _node;
		}
	}
}