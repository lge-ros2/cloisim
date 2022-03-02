/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System;

namespace SDF
{
	/*
		The superclass for handling multiple entity.
	*/
	public class Entities<T>
	{
		private List<T> items = new List<T>();

		private string targetTag = string.Empty;

		protected Entities(string tag)
		{
			targetTag = tag;
		}

		protected Entities(XmlNode node, in string tag)
			: this(tag)
		{
			LoadData(node);
		}

		protected void LoadData(XmlNode node)
		{
			if (node == null)
			{
				Console.WriteLine("Target Node is null");
				return;
			}

			//Console.WriteLine("Load {0} Info", typeof(T).Name);

			var nodeList = node.SelectNodes(targetTag);

			// Console.WriteLine("Num Of model nodes: " + nodeList.Count);
			foreach (var nodeItem in nodeList)
			{
				// Console.WriteLine("     NAME: " + node.Attributes["name"].Value);
				items.Add((T)Activator.CreateInstance(typeof(T), nodeItem));
			}
		}

		public List<T> GetData()
		{
			return items;
		}
	}

	/*
		The superclass for handling a common attributes in each SDF element
	*/
	public class Entity
	{
		protected XmlNode root = null;
		private XmlAttributeCollection attributes = null;
		private string name = string.Empty;
		private string type = string.Empty;

		protected Pose<double> pose = new Pose<double>();

		protected Entity(XmlNode node, in string default_name = "__default__", in string default_type = "__default__")
		{
			// Console.WriteLine("[{0}] Name: {1}, Type: {2}", GetType().Name, name, type);
			root = node;

			if (root != null)
			{
				attributes = root.Attributes;

				Name = (attributes["name"] != null) ? attributes["name"].Value : default_name;
				Type = (attributes["type"] != null) ? attributes["type"].Value : default_type;
			}
			else
			{
				Name = default_name;
				Type = default_type;
			}

			ParsePose();

			ParseElements();
		}

		public string Name
		{
			get => name;
			set => name = value;
		}

		public string Type
		{
			get => type;
			protected set => type = value;
		}

		public Pose<double> Pose => pose;

		public bool IsValidNode(in string xpath)
		{
			var node = GetNode(xpath);
			return (node == null) ? false : GetNode(xpath).HasChildNodes;
		}

		protected XmlNode GetNode(in string xpath)
		{
			return (root == null) ? null : root.SelectSingleNode(xpath);
		}

		protected XmlNodeList GetNodes(in string xpath)
		{
			return (root == null) ? null : root.SelectNodes(xpath);
		}

		public T GetValue<T>(in string xpath, in T defaultValue = default(T))
		{
			return GetValue<T>(GetNode(xpath), defaultValue);
		}

		public bool GetValues<T>(in string xpath, out List<T> valueList)
		{
			var nodeList = new List<XmlNode>(GetNodes(xpath).Cast<XmlNode>());
			if (nodeList == null)
			{
				valueList = null;
				return false;
			}

			valueList = nodeList.ConvertAll(node =>
			{
				if (node == null)
				{
					return default(T);
				}

				var value = node.InnerXml.Trim();
				return ConvertValueType<T>(value);
			});

			return true;
		}

		public T GetAttribute<T>(in string attributeName, in T defaultValue = default(T))
		{
			var targetAttribute = attributes[attributeName];
			if (targetAttribute == null)
			{
				return defaultValue;
			}

			return ConvertValueType<T>(attributes[attributeName].Value);
		}

		public T GetAttributeInPath<T>(in string xpath, in string attributeName, in T defaultValue = default(T))
		{
			var node = GetNode(xpath);
			if (node != null)
			{
				var attributes = node.Attributes;
				var attributeNode = attributes[attributeName];
				if (attributeNode != null)
				{
					var attrValue = attributeNode.Value;
					return ConvertValueType<T>(attrValue);
				}
			}

			return defaultValue;
		}

		public static T ConvertValueType<T>(string value)
		{
			var code = System.Type.GetTypeCode(typeof(T));

			if (code == TypeCode.Boolean)
			{
				if (Char.IsNumber(value, 0))
				{
					value = (value.Equals("1")) ? "true" : "false";
				}
			}

			return (T)Convert.ChangeType(value, code);
		}

		public static T ConvertXmlNodeToValue<T>(in XmlNode node)
		{
			if (node == null)
			{
				return default(T);
			}

			var value = node.InnerXml.Trim();

			return ConvertValueType<T>(value);
		}

		protected T GetValue<T>(in XmlNode tagNode, in T defaultValue = default(T))
		{
			try
			{
				return (tagNode == null) ? defaultValue : ConvertXmlNodeToValue<T>(tagNode);
			}
			catch (Exception ex)
			{
				Console.WriteLine("ERROR during converting...: " + ex.Message);
				return (T)defaultValue;
			}
		}

		private void ParsePose()
		{
			var value = GetValue<string>("pose");

			if (value != null)
			{
				pose.relative_to = GetAttributeInPath<string>("pose", "relative_to");

				// x y z roll pitch yaw
				value = value.Trim().Replace("    ", " ").Replace("   ", " ").Replace("  ", " ");
				var poseStr = value.Split(' ');

				try
				{
					pose.Pos.Set(poseStr[0], poseStr[1], poseStr[2]);
					pose.Rot.Set(poseStr[3], poseStr[4], poseStr[5]);
				}
				catch
				{
					Console.WriteLine("[{0}] failed to set pose {1}", name, poseStr);
				}

				// Console.WriteLine("Pose {0} {1} {2} {3} {4} {5}",
				// 	pose.Pos.X, pose.Pos.Y, pose.Pos.Z,
				// 	pose.Rot.Roll, pose.Rot.Pitch, pose.Rot.Yaw
				// );
			}
		}

		protected virtual void ParseElements()
		{
			// Console.WriteLine("[{0}] Nothing to parse", GetType().Name);
		}
	}
}