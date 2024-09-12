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
	public class Models : Entities<Model>
	{
		private const string TARGET_TAG = "model";
		public Models() : base(TARGET_TAG) {}
		public Models(XmlNode _node) : base(_node, TARGET_TAG) {}
	}

	public class Model : Entity
	{
		private Models models;
		private string canonical_link = string.Empty; // TODO: need to handle, from v1.7
		private string placement_frame = string.Empty; // TODO: need to handle,from v1.8
		private bool isStatic = false;
		private bool isSelfCollide = false;
		private bool allowAutoDisable = false;
		private bool enableWind = false;
		// <frame> : TBD

		private Links links;
		private Joints joints;
		private Plugins plugins;

		// <gripper> : TBD

		public bool IsStatic => isStatic;

		public bool IsSelfCollide => isSelfCollide;

		public bool AllowAutoDisable => allowAutoDisable;

		public bool IsWindEnabled => enableWind;

		#region SegmentationTag and SaveWorld
		private string _originalName = string.Empty;
		public string OriginalName => _originalName;
		#endregion

		#region Check nested model
		private bool _isNested = false;
		public bool IsNested => _isNested;
		#endregion

		public Model(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			models = new Models(root);
			links = new Links(root);
			joints = new Joints(root);
			plugins = new Plugins(root);

			canonical_link = GetAttribute<string>("canonical_link");
			placement_frame = GetAttribute<string>("placement_frame");
			isStatic = GetValue<bool>("static");
			isSelfCollide = GetValue<bool>("self_collide");
			allowAutoDisable = GetValue<bool>("allow_auto_disable");
			enableWind = GetValue<bool>("enable_wind");

			// Console.Write("[{0}] {1} {2} {3} {4}", GetType().Name,
			// 	isStatic, isSelfCollide, allowAutoDisable, enableWind);

			#region SegmentationTag and SaveWorld
			_originalName = GetAttribute<string>("original_name");
			// Console.Write($"Model::ParseElements original_name > [{_originalName}]");
			#endregion

			#region Check nested model
			_isNested = GetAttribute<bool>("is_nested", false);
			// Console.Write($"Model{Name}::ParseElements is_nested > [{_isNested}]");
			#endregion
		}

		public List<Model> GetModels()
		{
			return models.GetData();
		}

		public List<Link> GetLinks()
		{
			return links.GetData();
		}

		public List<Joint> GetJoints()
		{
			return joints.GetData();
		}

		public List<Plugin> GetPlugins()
		{
			return plugins.GetData();
		}
	}
}