/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using UnityEngine;

namespace SDFormat
{
	public static class SDF2URDF
	{
		public static string ConvertModelXmlToUrdf(string parentRawXml, string defaultRobotName = "robot")
		{
			var sdfXml = $"<?xml version='1.0' ?><sdf>{parentRawXml}</sdf>";

			var doc = new XmlDocument();
			try
			{
				doc.LoadXml(sdfXml);
			}
			catch (XmlException ex)
			{
				Debug.LogWarning($"[SDF2URDF] Failed to parse SDF for robot_description: {ex.Message}");
				return sdfXml;
			}

			var modelNode = doc.SelectSingleNode("/sdf/model");
			if (modelNode == null)
			{
				return sdfXml;
			}

			var robotName = GetAttributeOrDefault(modelNode, "name", defaultRobotName);
			var urdf = new StringBuilder();
			urdf.Append("<?xml version='1.0'?>\n");
			urdf.Append($"<robot name=\"{EscapeXml(robotName)}\">\n");

			AppendModelUrdf(urdf, modelNode, string.Empty);

			urdf.Append("</robot>");
			return urdf.ToString();
		}

		private static void AppendModelUrdf(StringBuilder urdf, XmlNode modelNode, string scopePrefix)
		{
			foreach (XmlNode linkNode in modelNode.SelectNodes("link"))
			{
				AppendLinkUrdf(urdf, linkNode, scopePrefix);
			}

			foreach (XmlNode jointNode in modelNode.SelectNodes("joint"))
			{
				AppendJointUrdf(urdf, jointNode, scopePrefix);
			}

			foreach (XmlNode childModelNode in modelNode.SelectNodes("model"))
			{
				var childModelName = GetAttributeOrDefault(childModelNode, "name", string.Empty);
				var childScopePrefix = CombineScope(scopePrefix, childModelName);
				AppendModelUrdf(urdf, childModelNode, childScopePrefix);
			}
		}

		private static void AppendLinkUrdf(StringBuilder urdf, XmlNode linkNode, string scopePrefix)
		{
			var linkName = NormalizeScopedName(scopePrefix, GetAttributeOrDefault(linkNode, "name", "link"));
			urdf.Append($"  <link name=\"{EscapeXml(linkName)}\">\n");

			var inertialNode = linkNode.SelectSingleNode("inertial");
			if (inertialNode != null)
			{
				urdf.Append("    <inertial>\n");
				AppendOriginFromPoseNode(urdf, inertialNode.SelectSingleNode("pose"), "      ");
				var mass = GetNodeTextOrDefault(inertialNode, "mass", "0");
				urdf.Append($"      <mass value=\"{EscapeXml(mass)}\"/>\n");

				var inertiaNode = inertialNode.SelectSingleNode("inertia");
				if (inertiaNode != null)
				{
					urdf.Append("      <inertia");
					urdf.Append($" ixx=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "ixx", "0"))}\"");
					urdf.Append($" ixy=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "ixy", "0"))}\"");
					urdf.Append($" ixz=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "ixz", "0"))}\"");
					urdf.Append($" iyy=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "iyy", "0"))}\"");
					urdf.Append($" iyz=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "iyz", "0"))}\"");
					urdf.Append($" izz=\"{EscapeXml(GetNodeTextOrDefault(inertiaNode, "izz", "0"))}\"/>");
					urdf.Append("\n");
				}

				urdf.Append("    </inertial>\n");
			}

			foreach (XmlNode visualNode in linkNode.SelectNodes("visual"))
			{
				AppendGeometryElement(urdf, visualNode, "visual");
			}

			foreach (XmlNode collisionNode in linkNode.SelectNodes("collision"))
			{
				AppendGeometryElement(urdf, collisionNode, "collision");
			}

			urdf.Append("  </link>\n");
		}

		private static void AppendJointUrdf(StringBuilder urdf, XmlNode jointNode, string scopePrefix)
		{
			var jointName = GetAttributeOrDefault(jointNode, "name", "joint");
			var axisNode = jointNode.SelectSingleNode("axis");
			var limitNode = axisNode?.SelectSingleNode("limit");
			var jointType = NormalizeJointType(GetAttributeOrDefault(jointNode, "type", "fixed"), limitNode != null);

			urdf.Append($"  <joint name=\"{EscapeXml(jointName)}\" type=\"{EscapeXml(jointType)}\">\n");
			AppendOriginFromPoseNode(urdf, jointNode.SelectSingleNode("pose"), "    ");

			var parent = GetNodeTextOrDefault(jointNode, "parent", string.Empty);
			if (!string.IsNullOrEmpty(parent))
			{
				urdf.Append($"    <parent link=\"{EscapeXml(NormalizeScopedReference(scopePrefix, parent))}\"/>\n");
			}

			var child = GetNodeTextOrDefault(jointNode, "child", string.Empty);
			if (!string.IsNullOrEmpty(child))
			{
				urdf.Append($"    <child link=\"{EscapeXml(NormalizeScopedReference(scopePrefix, child))}\"/>\n");
			}

			if (axisNode != null)
			{
				urdf.Append("    <axis");
				urdf.Append($" xyz=\"{EscapeXml(GetNodeTextOrDefault(axisNode, "xyz", "1 0 0"))}\"/>");
				urdf.Append("\n");

				if (limitNode != null)
				{
					urdf.Append("    <limit");
					urdf.Append($" lower=\"{EscapeXml(GetNodeTextOrDefault(limitNode, "lower", "0"))}\"");
					urdf.Append($" upper=\"{EscapeXml(GetNodeTextOrDefault(limitNode, "upper", "0"))}\"");
					urdf.Append($" effort=\"{EscapeXml(GetNodeTextOrDefault(limitNode, "effort", "0"))}\"");
					urdf.Append($" velocity=\"{EscapeXml(GetNodeTextOrDefault(limitNode, "velocity", "0"))}\"/>");
					urdf.Append("\n");
				}
			}

			urdf.Append("  </joint>\n");
		}

		private static void AppendGeometryElement(StringBuilder urdf, XmlNode sourceNode, string elementName)
		{
			var geometryNode = sourceNode.SelectSingleNode("geometry");
			if (geometryNode == null)
			{
				return;
			}

			urdf.Append($"    <{elementName}>\n");
			AppendOriginFromPoseNode(urdf, sourceNode.SelectSingleNode("pose"), "      ");
			urdf.Append("      <geometry>\n");

			if (geometryNode.SelectSingleNode("box") is XmlNode boxNode)
			{
				var size = GetNodeTextOrDefault(boxNode, "size", "1 1 1");
				urdf.Append($"        <box size=\"{EscapeXml(size)}\"/>\n");
			}
			else if (geometryNode.SelectSingleNode("sphere") is XmlNode sphereNode)
			{
				var radius = GetNodeTextOrDefault(sphereNode, "radius", "1");
				urdf.Append($"        <sphere radius=\"{EscapeXml(radius)}\"/>\n");
			}
			else if (geometryNode.SelectSingleNode("cylinder") is XmlNode cylinderNode)
			{
				var radius = GetNodeTextOrDefault(cylinderNode, "radius", "1");
				var length = GetNodeTextOrDefault(cylinderNode, "length", "1");
				urdf.Append($"        <cylinder radius=\"{EscapeXml(radius)}\" length=\"{EscapeXml(length)}\"/>\n");
			}
			else if (geometryNode.SelectSingleNode("mesh") is XmlNode meshNode)
			{
				var meshUriNode = meshNode.SelectSingleNode("uri") ?? meshNode.SelectSingleNode("filename");
				var meshUri = meshUriNode?.InnerText?.Trim() ?? string.Empty;
				var meshPath = ResolveMeshAbsolutePath(meshUri);
				var scale = GetNodeTextOrDefault(meshNode, "scale", string.Empty);

				if (!string.IsNullOrEmpty(scale))
				{
					urdf.Append($"        <mesh filename=\"{EscapeXml(meshPath)}\" scale=\"{EscapeXml(scale)}\"/>\n");
				}
				else
				{
					urdf.Append($"        <mesh filename=\"{EscapeXml(meshPath)}\"/>\n");
				}
			}

			urdf.Append("      </geometry>\n");
			urdf.Append($"    </{elementName}>\n");
		}

		private static void AppendOriginFromPoseNode(StringBuilder urdf, XmlNode poseNode, string indent)
		{
			if (poseNode == null)
			{
				return;
			}

			var tokens = poseNode.InnerText.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length < 6)
			{
				return;
			}

			if (!double.TryParse(tokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
				!double.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
				!double.TryParse(tokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z) ||
				!double.TryParse(tokens[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var roll) ||
				!double.TryParse(tokens[4], NumberStyles.Float, CultureInfo.InvariantCulture, out var pitch) ||
				!double.TryParse(tokens[5], NumberStyles.Float, CultureInfo.InvariantCulture, out var yaw))
			{
				return;
			}

			var xyz = string.Format(
				CultureInfo.InvariantCulture,
				"{0:0.################} {1:0.################} {2:0.################}",
				x,
				y,
				z);
			var rpy = string.Format(
				CultureInfo.InvariantCulture,
				"{0:0.################} {1:0.################} {2:0.################}",
				roll,
				pitch,
				yaw);

			urdf.Append($"{indent}<origin xyz=\"{EscapeXml(xyz)}\" rpy=\"{EscapeXml(rpy)}\"/>\n");
		}

		private static string ResolveMeshAbsolutePath(string uri)
		{
			if (string.IsNullOrEmpty(uri))
			{
				return string.Empty;
			}

			var path = uri.Trim();
			if (path.StartsWith("file://"))
			{
				path = path.Substring("file://".Length);
			}

			if (path.StartsWith("model://"))
			{
				path = path.Substring("model://".Length);
			}

			if (!Path.IsPathRooted(path))
			{
				path = Path.GetFullPath(path);
			}

			path = path.Replace('\\', '/');
			if (!path.StartsWith("/"))
			{
				path = "/" + path;
			}

			return "file://" + path;
		}

		private static string NormalizeScopedName(string scopePrefix, string localName)
		{
			if (string.IsNullOrEmpty(localName))
			{
				return string.Empty;
			}

			return CombineScope(scopePrefix, localName).Replace("::", "_");
		}

		private static string NormalizeScopedReference(string scopePrefix, string reference)
		{
			if (string.IsNullOrEmpty(reference))
			{
				return string.Empty;
			}

			var absoluteReference = reference.Contains("::") ? reference : CombineScope(scopePrefix, reference);
			return absoluteReference.Replace("::", "_");
		}

		private static string CombineScope(string scopePrefix, string localName)
		{
			if (string.IsNullOrEmpty(scopePrefix))
			{
				return localName ?? string.Empty;
			}

			if (string.IsNullOrEmpty(localName))
			{
				return scopePrefix;
			}

			return $"{scopePrefix}::{localName}";
		}

		private static string GetAttributeOrDefault(XmlNode node, string attributeName, string defaultValue)
		{
			if (node?.Attributes == null)
			{
				return defaultValue;
			}

			return node.Attributes[attributeName]?.Value ?? defaultValue;
		}

		private static string GetNodeTextOrDefault(XmlNode node, string path, string defaultValue)
		{
			var target = node?.SelectSingleNode(path);
			if (target == null)
			{
				return defaultValue;
			}

			var text = target.InnerText?.Trim();
			return string.IsNullOrEmpty(text) ? defaultValue : text;
		}

		private static string NormalizeJointType(string jointType, bool hasLimit)
		{
			switch (jointType)
			{
				case "revolute":
					return hasLimit ? "revolute" : "continuous";

				case "continuous":
				case "prismatic":
				case "fixed":
					return jointType;

				default:
					return "fixed";
			}
		}

		private static string EscapeXml(string value)
		{
			return System.Security.SecurityElement.Escape(value) ?? string.Empty;
		}

	}
}
