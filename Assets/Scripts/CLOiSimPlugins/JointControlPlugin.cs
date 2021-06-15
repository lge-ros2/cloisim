/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Any = cloisim.msgs.Any;

public class JointControlPlugin : CLOiSimPlugin
{
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
		RegisterServiceDevice("Info");
		RegisterRxDevice("Rx");
		RegisterTxDevice("Tx");

		AddThread(ServiceThread);
		AddThread(ReceiverThread, jointCommand);
		AddThread(SenderThread, jointState);

		LoadJoints();
	}

	protected override void OnReset()
	{
	}

	private void LoadJoints()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		jointState.SetUpdateRate(updateRate);

		if (GetPluginParameters().GetValues<string>("joints/link", out var links))
		{
			foreach (var link in links)
			{
				if (jointState != null)
				{
					jointState.AddTarget(link);
					// Debug.Log(link);
				}
			}
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform_list":
				break;

			case "request_transform":
				// var transformPartsName = requestValue.StringValue;
				// var devicePose = jointState.GetSubPartsPose(transformPartsName);
				// var deviceName = cam.DeviceName;
				// SetTransformInfoResponse(ref response, deviceName, devicePose);
				break;

			default:
				break;
		}
	}
}