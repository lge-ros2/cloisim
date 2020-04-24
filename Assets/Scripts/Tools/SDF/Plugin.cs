/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Plugins : Entities<Plugin>
	{
		private const string TARGET_TAG = "plugin";
		public Plugins() : base(TARGET_TAG) { }
		public Plugins(XmlNode _node) : base(_node, TARGET_TAG) { }
	}


	public class Plugin : Entity
	{
		public XmlNode GetNode()
		{
			return GetNode(".");
		}

		public Plugin(XmlNode _node)
			: base(_node)
		{
			ParseElements();
		}

		protected override void ParseElements()
		{
			if (root == null)
				return;
		}
	}
}