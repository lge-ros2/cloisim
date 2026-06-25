/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;
using SDFormat;
using UnityEngine;

public class JointControlPlugin : CLOiSimPlugin
{
	private List<TF> _tfList = new();
	private string _robotDescription = "<?xml version='1.0' ?><robot></robot>";
	private SensorDevices.JointCommand _jointCommand = null;
	private SensorDevices.JointState _jointState = null;
	private string _tfPrefix = string.Empty;
	private readonly Dictionary<string, SDFormat.Helper.Link> _jointLinkMap = new();

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.JOINTCONTROL;

		_jointState = gameObject.AddComponent<SensorDevices.JointState>();
		_jointCommand = gameObject.AddComponent<SensorDevices.JointCommand>();
		_jointCommand.SetJointState(_jointState);
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, _jointCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, _jointState);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, _tfList);
		}

		yield return null;

		_robotDescription = SDF2URDF.ConvertModelXmlToUrdf(GetPluginParameters().ParentRawXml(), gameObject.name);
		// UnityEngine.Debug.Log(_robotDescription);

		var modelHelper = GetComponent<SDFormat.Helper.Model>();
		if (modelHelper != null && modelHelper.RootModel != null && !modelHelper.Equals(modelHelper.RootModel))
		{
			_tfPrefix = modelHelper.name;
			Debug.Log($"[JointControlPlugin] {gameObject.name} is Nested model, _tfPrefix: {_tfPrefix}");
		}

		LoadJoints();
		_robotDescription = InjectJointOriginsFromScene(_robotDescription);

		yield return null;
	}

	protected override void OnReset()
	{
	}

	private string ApplyPrefixOnce(string frame)
	{
		if (string.IsNullOrEmpty(frame) || string.IsNullOrEmpty(_tfPrefix))
			return frame;

		var prefix = $"{_tfPrefix}_";
		return frame.StartsWith(prefix) ? frame : $"{prefix}{frame}";
	}

	private void LoadJoints()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		_jointState.SetUpdateRate(updateRate);

		if (GetPluginParameters().GetValues<string>("joints/joint", out var joints))
		{
			foreach (var jointName in joints)
			{
				// UnityEngine.Debug.Log("Joints loaded "+ jointName);
				if (_jointState.AddTargetJoint(jointName, out var targetLink, out var isStatic))
				{
					_jointLinkMap[jointName] = targetLink;
					var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("joints/joint[text()='" + jointName + "']", "parent_frame_id");
					var jointParentLinkName = string.IsNullOrEmpty(parentFrameId) ? targetLink.JointParentLinkName : parentFrameId;

					var childFrame = ApplyPrefixOnce(targetLink.JointChildLinkName);
					var parentFrame = ApplyPrefixOnce(jointParentLinkName);

					var tf = new TF(targetLink, childFrame, parentFrame);

					if (isStatic)
					{
						_staticTfList.Add(tf);
						// UnityEngine.Debug.LogFormat("StaticTfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
					}
					else
					{
						_tfList.Add(tf);
						// UnityEngine.Debug.LogFormat("_tfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
					}
				}
			}
		}
		// UnityEngine.Debug.Log("Joints loaded");
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "robot_description":
				SetRobotDescription(ref response);
				break;

			default:
				break;
		}
	}

	private void SetRobotDescription(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2Param = new messages.Param();
		ros2Param.Params["description"] = new Any { Type = Any.ValueType.String, StringValue = _robotDescription };

		msRos2Info.SetMessage(ros2Param);
	}

	private string InjectJointOriginsFromScene(string urdfXml)
	{
		var doc = new XmlDocument();
		try { doc.LoadXml(urdfXml); }
		catch { return urdfXml; }

		// Map link name -> ArticulationBody (process the whole chain, independent of the controlled-joint list)
		var abByName = new Dictionary<string, ArticulationBody>();
		foreach (var ab in gameObject.GetComponentsInChildren<ArticulationBody>())
		{
			if (!abByName.ContainsKey(ab.name))
				abByName[ab.name] = ab;
		}

		foreach (XmlNode jointNode in doc.SelectNodes("/robot/joint"))
		{
			var urdfJointName = jointNode.Attributes?["name"]?.Value ?? "(unnamed)";
			var parentName = jointNode.SelectSingleNode("parent")?.Attributes?["link"]?.Value;
			var childName = jointNode.SelectSingleNode("child")?.Attributes?["link"]?.Value;
			if (string.IsNullOrEmpty(parentName) || string.IsNullOrEmpty(childName))
				continue;

			if (!abByName.TryGetValue(childName, out var childAB) ||
				!abByName.TryGetValue(parentName, out var parentAB))
			{
				Debug.LogWarning($"[JointOrigin] {urdfJointName}: AB not found (parent={parentName}, child={childName}) -> keep SDF2URDF origin");
				continue;
			}

			// URDF link frame = AB body frame (same reference as visual/inertial origins). Anchor is not used.
			var childLocalPos = parentAB.transform.InverseTransformPoint(childAB.transform.position);
			var childLocalRot = Quaternion.Inverse(parentAB.transform.rotation) * childAB.transform.rotation;

			var sdfPos = Unity2SDF.Vector(childLocalPos);
			var sdfQuat = Unity2SDF.Rotation(childLocalRot);
			var rpy = QuatToRpy(sdfQuat.W, sdfQuat.X, sdfQuat.Y, sdfQuat.Z);

			var originElem = jointNode.SelectSingleNode("origin") as XmlElement
				?? (XmlElement)jointNode.InsertBefore(doc.CreateElement("origin"), jointNode.FirstChild);
			originElem.SetAttribute("xyz", string.Format(CultureInfo.InvariantCulture,
				"{0:G8} {1:G8} {2:G8}", sdfPos.X, sdfPos.Y, sdfPos.Z));
			originElem.SetAttribute("rpy", string.Format(CultureInfo.InvariantCulture,
				"{0:G8} {1:G8} {2:G8}", rpy[0], rpy[1], rpy[2]));

			Debug.Log($"[JointOrigin] {urdfJointName} ({parentName}->{childName}): " +
				$"xyz=({sdfPos.X:F4} {sdfPos.Y:F4} {sdfPos.Z:F4}) rpy=({rpy[0]:F4} {rpy[1]:F4} {rpy[2]:F4}) | " +
				$"childWorld={childAB.transform.position} parentWorld={parentAB.transform.position}");
		}

		return doc.OuterXml;
	}

	private static double[] QuatToRpy(double w, double x, double y, double z)
	{
		var roll = Math.Atan2(2 * (w * x + y * z), 1 - 2 * (x * x + y * y));
		var sinp = 2 * (w * y - z * x);
		var pitch = Math.Abs(sinp) >= 1 ? Math.Sign(sinp) * Math.PI / 2 : Math.Asin(sinp);
		var yaw = Math.Atan2(2 * (w * z + x * y), 1 - 2 * (y * y + z * z));
		return new[] { roll, pitch, yaw };
	}
}