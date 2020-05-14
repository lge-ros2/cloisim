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
	/*
		The superclass for handling multiple entity.
	*/
	public class Entities<T>
	{
		private List<T> items = new List<T>();

		private string targetTag = string.Empty;

		protected Entities(string _tag)
		{
			targetTag = _tag;
		}

		protected Entities(XmlNode node, string _tag)
			: this(_tag)
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

			XmlNodeList nodeList = node.SelectNodes(targetTag);

			// Console.WriteLine("Num Of model nodes: " + nodeList.Count);
			foreach (XmlNode nodeItem in nodeList)
			{
				//Console.WriteLine("     NAME: " + node.Attributes["name"].Value);
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

		protected Pose<double> pose = new Pose<double>(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
		protected string relative_to = string.Empty; // TBD : for SDF 1.7

		protected Entity(XmlNode node)
		{
			// Console.WriteLine("[{0}] Name: {1}, Type: {2}", GetType().Name, name, type);
			root = node;
			attributes = root.Attributes;

			if (attributes["name"] != null)
			{
				Name = attributes["name"].Value;
			}

			if (attributes["type"] != null)
			{
				Type = attributes["type"].Value;
			}

			ParsePose();
		}

		public string Name
		{
			get => name;
			protected set => name = value;
		}


		public string Type
		{
			get => type;
			protected set => type = value;
		}

		public Pose<double> Pose => pose;

		protected bool IsValidNode(in string xpath)
		{
			var node = GetNode(xpath);
			return (node == null)? false:GetNode(xpath).HasChildNodes;
		}

		protected XmlNode GetNode(in string xpath)
		{
			return (root == null)? null:root.SelectSingleNode(xpath);
		}

		protected XmlNodeList GetNodes(in string xpath)
		{
			return (root == null)? null:root.SelectNodes(xpath);
		}

		protected T GetValue<T>(in string xpath, in T defaultValue = default(T))
		{
			return GetValue<T>(GetNode(xpath), defaultValue);
		}

		protected T GetAttribute<T>(in string attributeName, in T defaultValue = default(T))
		{
			var targetAttribute = attributes[attributeName];
			if (targetAttribute == null)
			{
				return defaultValue;
			}

			return ConvertValueType<T>(attributes[attributeName].Value);
		}

		protected T GetAttributeInPath<T>(in string xpath, in string attributeName, in T defaultValue = default(T))
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

			// if (typeof(T) == typeof(double))
			// 	code = TypeCode.Double;
			// if (typeof(T) == typeof(float))
			// 	code = TypeCode.Single;
			// else if (typeof(T) == typeof(int))
			// 	code = TypeCode.Int32;
			// else if (typeof(T) == typeof(uint))
			// 	code = TypeCode.UInt32;
			// else if (typeof(T) == typeof(string))
			// 	code = TypeCode.String;
			// else if (typeof(T) == typeof(bool))
			// {
			// 	code = TypeCode.Boolean;
			// 	value = (value == "1")? "true":"false";
			// }
			// else
			// 	return (T)defaultValue;

			if (code == TypeCode.Boolean)
			{
				if (Char.IsNumber(value, 0))
				{
					value = (value.Equals("1")) ? "true" : "false";
				}
			}

			return (T)Convert.ChangeType(value, code);
		}

		private static T ConvertXmlNodeToValue<T>(in XmlNode node)
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
				return ConvertXmlNodeToValue<T>(tagNode);
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

			// TBD : for SDF 1.7
			relative_to = GetAttributeInPath<string>("pose", "relative_to");

			if (value != null)
			{
				// x y z roll pitch yaw
				string[] poseStr = value.Split(' ');

				pose.Pos.X = Convert.ToDouble(poseStr[0]);
				pose.Pos.Y = Convert.ToDouble(poseStr[1]);
				pose.Pos.Z = Convert.ToDouble(poseStr[2]);
				pose.Rot.Roll = Convert.ToDouble(poseStr[3]);
				pose.Rot.Pitch = Convert.ToDouble(poseStr[4]);
				pose.Rot.Yaw = Convert.ToDouble(poseStr[5]);

				// Console.WriteLine("Pose {0} {1} {2} {3} {4} {5}",
				// 	pose.Pos.X, pose.Pos.Y, pose.Pos.Z,
				// 	pose.Rot.Roll, pose.Rot.Pitch, pose.Rot.Yaw
				// );
			}
		}

		protected virtual void ParseElements()
		{
			Console.WriteLine("[{0}] Nothing to parse", GetType().Name);
		}
	}
}