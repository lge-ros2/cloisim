/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;

public class JointControlPlugin : CLOiSimPlugin
{
	private List<TF> _tfList = new List<TF>();
	private string _robotDescription = "<?xml version='1.0' ?><sdf></sdf>";
	private SensorDevices.JointCommand _jointCommand = null;
	private SensorDevices.JointState _jointState = null;

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

		LoadJoints();

		_robotDescription = "<?xml version='1.0' ?><sdf>" + GetPluginParameters().ParentRawXml() + "</sdf>";
		// UnityEngine.Debug.Log(_robotDescription);

		yield return null;
	}

	protected override void OnReset()
	{
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
					var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("joints/joint[text()='" + jointName + "']", "parent_frame_id");
					var jointParentLinkName = (string.IsNullOrEmpty(parentFrameId)) ? targetLink.JointParentLinkName : parentFrameId;
					var tf = new TF(targetLink, targetLink.JointChildLinkName, jointParentLinkName);
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

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "description";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.String };
		ros2CommonInfo.Value.StringValue = _robotDescription;

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}