/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using SysMath = System.Math;
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
			var urdfVersion = DetermineRequiredUrdfVersion(modelNode);
			var urdf = new StringBuilder();
			urdf.Append("<?xml version='1.0'?>\n");
			urdf.Append($"<robot name=\"{EscapeXml(robotName)}\" version=\"{urdfVersion}\">\n");

			AppendModelUrdf(urdf, modelNode, string.Empty);

			urdf.Append("</robot>");
			return urdf.ToString();
		}

		// URDF version is derived from the URDF features actually emitted by this
		// converter, not from the SDF version of the input. SDF and URDF spec
		// versions evolve independently and do not map one-to-one.
		//
		// Note: parsing URDF 1.1 <capsule> requires a consumer with
		// urdfdom >= 5.1.0 and urdfdom_headers >= 2.1.0.
		private static string DetermineRequiredUrdfVersion(XmlNode modelNode)
		{
			return ContainsCapsule(modelNode) ? "1.1" : "1.0";
		}

		private static bool ContainsCapsule(XmlNode modelNode)
		{
			return modelNode.SelectSingleNode(".//capsule") != null;
		}

		private static void AppendModelUrdf(StringBuilder urdf, XmlNode modelNode, string scopePrefix)
		{
			var linkPoses = new Dictionary<string, double[]>();
			foreach (XmlNode linkNode in modelNode.SelectNodes("link"))
			{
				var linkName = GetAttributeOrDefault(linkNode, "name", string.Empty);
				var pose = ParsePose(linkNode.SelectSingleNode("pose"));
				if (pose != null && !string.IsNullOrEmpty(linkName))
					linkPoses[linkName] = pose;
			}

			foreach (XmlNode linkNode in modelNode.SelectNodes("link"))
			{
				AppendLinkUrdf(urdf, linkNode, scopePrefix);
			}

			foreach (XmlNode jointNode in modelNode.SelectNodes("joint"))
			{
				AppendJointUrdf(urdf, jointNode, scopePrefix, linkPoses);
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

		private static void AppendJointUrdf(StringBuilder urdf, XmlNode jointNode, string scopePrefix, Dictionary<string, double[]> linkPoses)
		{
			var jointName = NormalizeScopedName(scopePrefix, GetAttributeOrDefault(jointNode, "name", "joint"));
			var axisNode = jointNode.SelectSingleNode("axis");
			var limitNode = axisNode?.SelectSingleNode("limit");
			var jointType = NormalizeJointType(GetAttributeOrDefault(jointNode, "type", "fixed"), limitNode != null);

			urdf.Append($"  <joint name=\"{EscapeXml(jointName)}\" type=\"{EscapeXml(jointType)}\">\n");

			var jointPoseNode = jointNode.SelectSingleNode("pose");
			var jointPose = ParsePose(jointPoseNode);
			if (IsZeroPose(jointPose))
			{
				var parentLinkName = GetNodeTextOrDefault(jointNode, "parent", string.Empty);
				var childLinkName = GetNodeTextOrDefault(jointNode, "child", string.Empty);
				if (linkPoses.TryGetValue(parentLinkName, out var parentPose) &&
					linkPoses.TryGetValue(childLinkName, out var childPose))
				{
					var relPose = ComputeRelativePose(parentPose, childPose);
					AppendOriginFromPoseArray(urdf, relPose, "    ");
				}
			}
			else
			{
				AppendOriginFromPoseNode(urdf, jointPoseNode, "    ");
			}

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
			else if (geometryNode.SelectSingleNode("capsule") is XmlNode capsuleNode)
			{
				// SDF capsule length is the length of the cylindrical section along Z,
				// independent of the two hemispherical end caps. Pass it through as-is;
				// do not apply Unity's CapsuleCollider.height = length + 2 * radius rule,
				// which belongs to SDF -> Unity conversion, not SDF -> URDF conversion.
				var radiusText = GetNodeTextOrDefault(capsuleNode, "radius", "1");
				var lengthText = GetNodeTextOrDefault(capsuleNode, "length", "1");
				if (!double.TryParse(radiusText, NumberStyles.Float, CultureInfo.InvariantCulture, out var radius) ||
					!double.TryParse(lengthText, NumberStyles.Float, CultureInfo.InvariantCulture, out var length) ||
					radius <= 0.0 || length < 0.0)
				{
					Debug.LogWarning($"[SDF2URDF] Invalid capsule geometry: radius={radiusText}, length={lengthText}");
				}
				else
				{
					urdf.Append(string.Format(
						CultureInfo.InvariantCulture,
						"        <capsule radius=\"{0:0.################}\" length=\"{1:0.################}\"/>\n",
						radius,
						length));
				}
			}
			else if (geometryNode.SelectSingleNode("ellipsoid") is XmlNode ellipsoidNode)
			{
				// Ellipsoid has no standard URDF primitive representation. Approximate it
				// with a sphere whose radius is the mean of the three semi-axes. This
				// changes visual/collision/inertia semantics for non-uniform ellipsoids,
				// so a warning is emitted to make the approximation visible.
				var radiiText = GetNodeTextOrDefault(ellipsoidNode, "radii", "1 1 1");
				var radiiTokens = radiiText.Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
				if (radiiTokens.Length == 3 &&
					double.TryParse(radiiTokens[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var rx) &&
					double.TryParse(radiiTokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var ry) &&
					double.TryParse(radiiTokens[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var rz) &&
					rx > 0.0 && ry > 0.0 && rz > 0.0)
				{
					var approximateRadius = (rx + ry + rz) / 3.0;
					Debug.LogWarning($"[SDF2URDF] Ellipsoid geometry is not a standard URDF primitive; approximating with a sphere of radius {approximateRadius} (mean of radii \"{radiiText}\"). Visual/collision/inertia shape will differ from the original ellipsoid.");
					urdf.Append(string.Format(
						CultureInfo.InvariantCulture,
						"        <sphere radius=\"{0:0.################}\"/>\n",
						approximateRadius));
				}
				else
				{
					Debug.LogWarning($"[SDF2URDF] Invalid ellipsoid geometry radii: \"{radiiText}\"");
				}
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

		private static double[] ParsePose(XmlNode poseNode)
		{
			if (poseNode == null)
				return null;
			var tokens = poseNode.InnerText.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
			if (tokens.Length < 6)
				return null;
			var pose = new double[6];
			for (var i = 0; i < 6; i++)
			{
				if (!double.TryParse(tokens[i], NumberStyles.Float, CultureInfo.InvariantCulture, out pose[i]))
					return null;
			}
			return pose;
		}

		private static bool IsZeroPose(double[] pose)
		{
			if (pose == null)
				return true;
			const double eps = 1e-9;
			foreach (var v in pose)
				if (SysMath.Abs(v) > eps) return false;
			return true;
		}

		private static double[,] RpyToMatrix(double roll, double pitch, double yaw)
		{
			var cr = SysMath.Cos(roll); var sr = SysMath.Sin(roll);
			var cp = SysMath.Cos(pitch); var sp = SysMath.Sin(pitch);
			var cy = SysMath.Cos(yaw); var sy = SysMath.Sin(yaw);
			return new double[,]
			{
				{ cy*cp, cy*sp*sr - sy*cr, cy*sp*cr + sy*sr },
				{ sy*cp, sy*sp*sr + cy*cr, sy*sp*cr - cy*sr },
				{ -sp,   cp*sr,            cp*cr            }
			};
		}

		private static double[] MatrixToRpy(double[,] m)
		{
			var pitch = SysMath.Atan2(-m[2, 0], SysMath.Sqrt(m[0, 0] * m[0, 0] + m[1, 0] * m[1, 0]));
			double roll, yaw;
			if (SysMath.Abs(SysMath.Cos(pitch)) < 1e-6)
			{
				roll = 0;
				yaw = SysMath.Atan2(-m[1, 2], m[1, 1]);
			}
			else
			{
				roll = SysMath.Atan2(m[2, 1], m[2, 2]);
				yaw = SysMath.Atan2(m[1, 0], m[0, 0]);
			}
			return new[] { roll, pitch, yaw };
		}

		private static double[] ComputeRelativePose(double[] parentPose, double[] childPose)
		{
			var rp = RpyToMatrix(parentPose[3], parentPose[4], parentPose[5]);
			var rc = RpyToMatrix(childPose[3], childPose[4], childPose[5]);

			var dx = childPose[0] - parentPose[0];
			var dy = childPose[1] - parentPose[1];
			var dz = childPose[2] - parentPose[2];

			// R_parent^T * delta_xyz
			var rx = rp[0, 0] * dx + rp[1, 0] * dy + rp[2, 0] * dz;
			var ry = rp[0, 1] * dx + rp[1, 1] * dy + rp[2, 1] * dz;
			var rz = rp[0, 2] * dx + rp[1, 2] * dy + rp[2, 2] * dz;

			// R_parent^T * R_child
			var dr = new double[3, 3];
			for (var i = 0; i < 3; i++)
				for (var j = 0; j < 3; j++)
					for (var k = 0; k < 3; k++)
						dr[i, j] += rp[k, i] * rc[k, j];

			var rpy = MatrixToRpy(dr);
			return new[] { rx, ry, rz, rpy[0], rpy[1], rpy[2] };
		}

		private static void AppendOriginFromPoseArray(StringBuilder urdf, double[] pose, string indent)
		{
			if (pose == null)
				return;
			var xyz = string.Format(CultureInfo.InvariantCulture,
				"{0:0.################} {1:0.################} {2:0.################}",
				pose[0], pose[1], pose[2]);
			var rpy = string.Format(CultureInfo.InvariantCulture,
				"{0:0.################} {1:0.################} {2:0.################}",
				pose[3], pose[4], pose[5]);
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
