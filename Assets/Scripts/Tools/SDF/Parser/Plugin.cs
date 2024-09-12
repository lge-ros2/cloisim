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
		private string filename = "__default__";

		public Plugin(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			filename = GetAttribute<string>("filename");
		}

		public string LibraryName()
		{
			var pluginName = filename;
			if (pluginName.StartsWith("lib"))
			{
				pluginName = pluginName.Substring(3);
			}

			if (pluginName.EndsWith(".so"))
			{
				var foundIndex = pluginName.IndexOf(".so");
				pluginName = pluginName.Remove(foundIndex);
			}

			return pluginName;
		}

		public string ParentRawXml()
		{
			return root.ParentNode.OuterXml;
		}

		public string RawXml()
		{
			return root.OuterXml;
		}
	}
}