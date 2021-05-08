/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;

namespace SDF
{
	public class Visuals : Entities<Visual>
	{
		private const string TARGET_TAG = "visual";
		public Visuals() : base(TARGET_TAG) {}
		public Visuals(XmlNode _node) : base(_node, TARGET_TAG) {}
	}

	public class Visual : Entity
	{
		public class Meta
		{
			public int layer;
		}

		private bool cast_shadows = true;
		private double laser_retro = 0.0;
		private double transparency = 0.0;

		private Meta meta;

		private Material material;
		private Geometry geometry;
		private Plugins plugins;

		public bool CastShadow => cast_shadows;

		public Visual(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			plugins = new Plugins(root);

			var matNode = GetNode("material");
			if (matNode != null)
			{
				material = new Material(matNode);
			}

			geometry = new Geometry( GetNode("geometry"));

			var metaNode = GetNode("meta");
			if (metaNode != null)
			{
				meta = new Meta();
				meta.layer = GetValue<int>("meta/layer");
			}

			cast_shadows = GetValue<bool>("cast_shadows");
			laser_retro = GetValue<double>("laser_retro");
			transparency = GetValue<double>("transparency");

			// Console.WriteLine("[{0}] P:{1} C:{2}", GetType().Name, parent, child);
		}

		public int GetMetaLayer()
		{
			return (meta == null) ? -1 : meta.layer;
		}

		public Geometry GetGeometry()
		{
			return geometry;
		}

		public Material GetMaterial()
		{
			return material;
		}

		public List<Plugin> GetPlugins()
		{
			return plugins.GetData();
		}
	}
}