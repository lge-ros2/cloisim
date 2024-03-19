/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;

public class JointControlPlugin : CLOiSimPlugin
{
	private List<TF> tfList = new List<TF>();
	private string _robotDescription = "<?xml version='1.0' ?><sdf></sdf>";
	private SensorDevices.JointCommand jointCommand = null;
	private SensorDevices.JointState jointState = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.JOINTCONTROL;
		jointState = gameObject.AddComponent<SensorDevices.JointState>();
		jointCommand = gameObject.AddComponent<SensorDevices.JointCommand>();
		jointCommand.SetJointState(jointState);

		attachedDevices.Add("Command", jointCommand);
		attachedDevices.Add("States", jointState);
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, jointCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, jointState);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, tfList);
		}

		LoadJoints();

		_robotDescription = "<?xml version='1.0' ?><sdf>" + GetPluginParameters().ParentRawXml() + "</sdf>";
		// UnityEngine.Debug.Log(_robotDescription);
	}

	protected override void OnReset()
	{
	}

	private void LoadJoints()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		jointState.SetUpdateRate(updateRate);

		if (GetPluginParameters().GetValues<string>("joints/joint", out var joints))
		{
			foreach (var jointName in joints)
			{
				// UnityEngine.Debug.Log("Joints loaded "+ jointName);
				if (jointState.AddTargetJoint(jointName, out var targetLink, out var isStatic))
				{
					var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("joints/joint[text()='" + jointName + "']", "parent_frame_id");
					var jointParentLinkName = (parentFrameId == null) ? targetLink.JointParentLinkName : parentFrameId;
					var tf = new TF(targetLink, targetLink.JointChildLinkName, jointParentLinkName);
					if (isStatic)
					{
						staticTfList.Add(tf);
						// UnityEngine.Debug.LogFormat("staticTfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
					}
					else
					{
						tfList.Add(tf);
						// UnityEngine.Debug.LogFormat("tfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
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

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "description";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.String };
		ros2CommonInfo.Value.StringValue = _robotDescription;

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}