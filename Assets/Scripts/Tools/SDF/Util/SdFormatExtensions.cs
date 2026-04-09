/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SDFormat
{
	/// <summary>
	/// Extension methods for SdFormat types to provide convenience accessors
	/// that were previously part of the SDF Parser wrapper layer.
	/// </summary>
	public static class Extensions
	{
		#region Element Path Navigation

		/// <summary>
		/// Navigate to an element by a slash-separated path, supporting XPath-like predicates:
		/// - "a/b/c" traverses children by name
		/// - "a/b[text()='value']" selects a child whose text matches
		/// - "a/b[@attr='value']" selects a child whose attribute matches
		/// - "a/b[@name='left']" selects a child whose 'name' attribute matches
		/// </summary>
		public static SDFormat.Element FindElementByPath(SDFormat.Element root, string path)
		{
			if (root == null || string.IsNullOrEmpty(path))
				return null;

			var current = root;
			var segments = SplitPath(path);

			foreach (var segment in segments)
			{
				if (current == null)
					return null;

				var (name, predicate) = ParseSegment(segment);

				if (predicate == null)
				{
					current = current.FindElement(name);
				}
				else
				{
					current = FindWithPredicate(current, name, predicate);
				}
			}

			return current;
		}

		/// <summary>
		/// Find all elements matching a slash-separated path (last segment may match multiple).
		/// </summary>
		public static List<SDFormat.Element> FindAllElementsByPath(SDFormat.Element root, string path)
		{
			var result = new List<SDFormat.Element>();
			if (root == null || string.IsNullOrEmpty(path))
				return result;

			var segments = SplitPath(path);
			if (segments.Length == 0)
				return result;

			// Navigate to parent of the last segment
			var current = root;
			for (var i = 0; i < segments.Length - 1; i++)
			{
				if (current == null)
					return result;

				var (name, predicate) = ParseSegment(segments[i]);
				current = predicate == null
					? current.FindElement(name)
					: FindWithPredicate(current, name, predicate);
			}

			if (current == null)
				return result;

			// Collect all matching children for the last segment
			var (lastName, lastPred) = ParseSegment(segments[segments.Length - 1]);
			foreach (var child in current.Children)
			{
				if (child.Name != lastName)
					continue;

				if (lastPred == null || MatchesPredicate(child, lastPred))
				{
					result.Add(child);
				}
			}

			return result;
		}

		private static string[] SplitPath(string path)
		{
			// Split on '/' but not inside brackets
			var parts = new List<string>();
			var depth = 0;
			var start = 0;
			for (var i = 0; i < path.Length; i++)
			{
				if (path[i] == '[') depth++;
				else if (path[i] == ']') depth--;
				else if (path[i] == '/' && depth == 0)
				{
					if (i > start)
						parts.Add(path.Substring(start, i - start));
					start = i + 1;
				}
			}
			if (start < path.Length)
				parts.Add(path.Substring(start));
			return parts.ToArray();
		}

		private static (string name, string predicate) ParseSegment(string segment)
		{
			var bracketIdx = segment.IndexOf('[');
			if (bracketIdx < 0)
				return (segment, null);

			var name = segment.Substring(0, bracketIdx);
			var pred = segment.Substring(bracketIdx);
			return (name, pred);
		}

		private static SDFormat.Element FindWithPredicate(SDFormat.Element parent, string childName, string predicate)
		{
			foreach (var child in parent.Children)
			{
				if (child.Name != childName)
					continue;

				if (MatchesPredicate(child, predicate))
					return child;
			}
			return null;
		}

		private static bool MatchesPredicate(SDFormat.Element elem, string predicate)
		{
			// [text()='value']
			var textMatch = Regex.Match(predicate, @"\[text\(\)\s*=\s*'([^']*)'\]");
			if (textMatch.Success)
			{
				var expected = textMatch.Groups[1].Value;
				var actual = elem.Value?.GetAsString()?.Trim() ?? string.Empty;
				return actual == expected;
			}

			// [@attr='value']
			var attrMatch = Regex.Match(predicate, @"\[@(\w+)\s*=\s*'([^']*)'\]");
			if (attrMatch.Success)
			{
				var attrName = attrMatch.Groups[1].Value;
				var expected = attrMatch.Groups[2].Value;
				var attr = elem.GetAttribute(attrName);
				var actual = attr?.GetAsString() ?? string.Empty;
				return actual == expected;
			}

			return true;
		}

		#endregion

		#region Element Extensions

		/// <summary>
		/// Get a typed attribute value from an Element.
		/// </summary>
		public static T GetAttribute<T>(this SDFormat.Element element, string attributeName, T defaultValue = default)
		{
			var attr = element?.GetAttribute(attributeName);
			if (attr == null)
				return defaultValue;
			return ConvertValue<T>(attr.GetAsString(), defaultValue);
		}

		/// <summary>
		/// Get child elements matching a tag name from an Element.
		/// </summary>
		public static List<SDFormat.Element> GetElements(this SDFormat.Element element, string tagName)
		{
			if (element == null)
				return new List<SDFormat.Element>();
			return element.Children.Where(c => c.Name == tagName).ToList();
		}

		#endregion

		#region Plugin Extensions

		public static string LibraryName(this SDFormat.Plugin plugin)
		{
			var pluginName = plugin.Filename ?? string.Empty;
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

		public static string RawXml(this SDFormat.Plugin plugin)
		{
			return plugin.Element?.ToString() ?? string.Empty;
		}

		public static string ParentRawXml(this SDFormat.Plugin plugin)
		{
			return plugin.Element?.Parent?.ToString() ?? string.Empty;
		}

		/// <summary>
		/// Get a typed value from a plugin's child element by path (supports nested paths).
		/// </summary>
		public static T GetValue<T>(this SDFormat.Plugin plugin, string path, T defaultValue = default)
		{
			var elem = FindElementByPath(plugin.Element, path);
			if (elem?.Value == null)
				return defaultValue;
			return ConvertValue<T>(elem.Value.GetAsString().Trim(), defaultValue);
		}

		/// <summary>
		/// Check if a plugin has a child element at the given path (supports nested paths).
		/// </summary>
		public static bool HasElement(this SDFormat.Plugin plugin, string path)
		{
			return FindElementByPath(plugin.Element, path) != null;
		}

		/// <summary>
		/// Check if a path is valid (element exists). Alias for HasElement.
		/// </summary>
		public static bool IsValidNode(this SDFormat.Plugin plugin, string path)
		{
			return FindElementByPath(plugin.Element, path) != null;
		}

		/// <summary>
		/// Get a child element from a plugin by path (supports nested paths).
		/// </summary>
		public static SDFormat.Element GetElement(this SDFormat.Plugin plugin, string path)
		{
			return FindElementByPath(plugin.Element, path);
		}

		/// <summary>
		/// Get an attribute value from a plugin element.
		/// </summary>
		public static T GetAttribute<T>(this SDFormat.Plugin plugin, string attributeName, T defaultValue = default)
		{
			var attr = plugin.Element?.GetAttribute(attributeName);
			if (attr == null)
				return defaultValue;
			return ConvertValue<T>(attr.GetAsString(), defaultValue);
		}

		/// <summary>
		/// Get child elements matching a tag name.
		/// </summary>
		public static List<SDFormat.Element> GetElements(this SDFormat.Plugin plugin, string tagName)
		{
			if (plugin.Element == null)
				return new List<SDFormat.Element>();
			return plugin.Element.Children.Where(c => c.Name == tagName).ToList();
		}

		/// <summary>
		/// Get all values of elements matching a path. The last segment is collected (all matching siblings).
		/// </summary>
		public static bool GetValues<T>(this SDFormat.Plugin plugin, string path, out List<T> values)
		{
			values = new List<T>();
			if (plugin.Element == null)
				return false;

			var elements = FindAllElementsByPath(plugin.Element, path);
			foreach (var elem in elements)
			{
				if (elem?.Value == null)
					continue;
				var converted = ConvertValue<T>(elem.Value.GetAsString().Trim());
				values.Add(converted);
			}

			return values.Count > 0;
		}

		/// <summary>
		/// Get an attribute value from an element found by path.
		/// </summary>
		public static T GetAttributeInPath<T>(this SDFormat.Plugin plugin, string path, string attributeName, T defaultValue = default)
		{
			var elem = FindElementByPath(plugin.Element, path);
			if (elem == null)
				return defaultValue;

			var attr = elem.GetAttribute(attributeName);
			if (attr == null)
				return defaultValue;
			return ConvertValue<T>(attr.GetAsString(), defaultValue);
		}

		/// <summary>
		/// Get an attribute value from an element found by path (without default).
		/// </summary>
		public static T GetAttributeInPath<T>(this SDFormat.Plugin plugin, string path, string attributeName)
		{
			return plugin.GetAttributeInPath<T>(path, attributeName, default);
		}

		#endregion

		#region Model Extensions

		/// <summary>
		/// Get the original model name stored during include resolution.
		/// Falls back to the model's Name if not set.
		/// </summary>
		public static string OriginalName(this SDFormat.Model model)
		{
			var attr = model.Element?.GetAttribute("original_name");
			return attr?.GetAsString() ?? model.Name;
		}

		/// <summary>
		/// Check if this model was nested via include resolution.
		/// </summary>
		public static bool IsNested(this SDFormat.Model model)
		{
			var attr = model.Element?.GetAttribute("is_nested");
			if (attr == null)
				return false;
			var val = attr.GetAsString();
			return val == "true" || val == "1";
		}

		#endregion

		#region Link Extensions

		/// <summary>
		/// Check if the link has self-collide enabled.
		/// </summary>
		public static bool SelfCollide(this SDFormat.Link link)
		{
			var elem = link.Element?.FindElement("self_collide");
			if (elem?.Value == null)
				return false;
			var val = elem.Value.GetAsString().Trim();
			return val == "true" || val == "1";
		}

		/// <summary>
		/// Get battery info from the link, if present.
		/// </summary>
		public static (string name, double voltage)? GetBattery(this SDFormat.Link link)
		{
			var batteryElem = link.Element?.FindElement("battery");
			if (batteryElem == null)
				return null;

			var nameAttr = batteryElem.GetAttribute("name");
			var name = nameAttr?.GetAsString() ?? string.Empty;

			var voltageElem = batteryElem.FindElement("voltage");
			var voltage = 0.0;
			if (voltageElem?.Value != null)
			{
				double.TryParse(voltageElem.Value.GetAsString().Trim(), out voltage);
			}

			return (name, voltage);
		}

		#endregion

		#region Visual Extensions

		/// <summary>
		/// Get the meta layer index for the visual.
		/// </summary>
		public static int GetMetaLayer(this SDFormat.Visual visual)
		{
			var metaElem = visual.Element?.FindElement("meta");
			if (metaElem == null)
				return -1;

			var layerElem = metaElem.FindElement("layer");
			if (layerElem?.Value == null)
				return -1;

			if (int.TryParse(layerElem.Value.GetAsString().Trim(), out var layer))
				return layer;
			return -1;
		}

		#endregion

		#region Sensor Extensions

		/// <summary>
		/// Check if the sensor should be visualized.
		/// </summary>
		public static bool Visualize(this SDFormat.Sensor sensor)
		{
			var elem = sensor.Element?.FindElement("visualize");
			if (elem?.Value == null)
				return false;
			var val = elem.Value.GetAsString().Trim();
			return val == "true" || val == "1";
		}

		/// <summary>
		/// Get contact sensor parameters from the sensor element.
		/// Returns null if this is not a contact sensor.
		/// </summary>
		public static ContactData GetContactData(this SDFormat.Sensor sensor)
		{
			var contactElem = sensor.Element?.FindElement("contact");
			if (contactElem == null)
				return null;

			var data = new ContactData();
			data.collision = GetElementValue(contactElem, "collision", string.Empty);
			data.topic = GetElementValue(contactElem, "topic", string.Empty);
			return data;
		}

		/// <summary>
		/// Get multiple camera sensor data (for multicamera/rgbd sensors).
		/// </summary>
		public static List<SDFormat.CameraSensor> GetCameras(this SDFormat.Sensor sensor)
		{
			var cameras = new List<SDFormat.CameraSensor>();

			if (sensor.Element == null)
				return cameras;

			var cameraElements = sensor.Element.Children.Where(c => c.Name == "camera").ToList();
			foreach (var camElem in cameraElements)
			{
				var cam = new SDFormat.CameraSensor();
				cam.Load(camElem);
				cameras.Add(cam);
			}

			return cameras;
		}

		#endregion

		#region Geometry Extensions

		/// <summary>
		/// Check if the geometry has no shape defined.
		/// </summary>
		public static bool IsEmpty(this SDFormat.Geometry geometry)
		{
			return geometry.Type == SDFormat.GeometryType.Empty;
		}

		#endregion

		#region Light Extensions

		/// <summary>
		/// Get the light type as a string ("point", "directional", "spot").
		/// </summary>
		public static string TypeString(this SDFormat.Light light)
		{
			return light.Type switch
			{
				SDFormat.LightType.Point => "point",
				SDFormat.LightType.Directional => "directional",
				SDFormat.LightType.Spot => "spot",
				_ => "point"
			};
		}

		#endregion

		#region Joint Extensions

		/// <summary>
		/// Get the joint type as a string.
		/// </summary>
		public static string TypeString(this SDFormat.Joint joint)
		{
			return joint.Type switch
			{
				SDFormat.JointType.Ball => "ball",
				SDFormat.JointType.Continuous => "continuous",
				SDFormat.JointType.Fixed => "fixed",
				SDFormat.JointType.Gearbox => "gearbox",
				SDFormat.JointType.Prismatic => "prismatic",
				SDFormat.JointType.Revolute => "revolute",
				SDFormat.JointType.Revolute2 => "revolute2",
				SDFormat.JointType.Screw => "screw",
				SDFormat.JointType.Universal => "universal",
				_ => "unknown"
			};
		}

		/// <summary>
		/// Check if a joint axis has meaningful limits set.
		/// </summary>
		public static bool HasJointLimits(this SDFormat.JointAxis axis)
		{
			return axis.Lower > -1e15 && axis.Upper < 1e15;
		}

		#endregion

		#region Element Value Helpers

		public static T GetElementValue<T>(SDFormat.Element parent, string childName, T defaultValue = default)
		{
			if (parent == null)
				return defaultValue;

			var child = parent.FindElement(childName);
			if (child?.Value == null)
				return defaultValue;

			return ConvertValue<T>(child.Value.GetAsString().Trim(), defaultValue);
		}

		private static T ConvertValue<T>(string value, T defaultValue = default)
		{
			try
			{
				if (string.IsNullOrEmpty(value))
					return defaultValue;

				var code = System.Type.GetTypeCode(typeof(T));
				if (code == System.TypeCode.Boolean)
				{
					if (char.IsNumber(value, 0))
					{
						value = value.Equals("1") ? "true" : "false";
					}
				}

				return (T)System.Convert.ChangeType(value, code);
			}
			catch
			{
				return defaultValue;
			}
		}

		#endregion
	}

	#region Custom Data Types (not in SdFormat library)

	/// <summary>
	/// Contact sensor parameters extracted from SDF element.
	/// </summary>
	public class ContactData
	{
		public string collision { get; set; } = string.Empty;
		public string topic { get; set; } = string.Empty;
	}

	#endregion
}
