/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;
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

		attachedDevices.Add("command", jointCommand);
		attachedDevices.Add("states", jointState);
	}

	protected override void OnStart()
	{
		RegisterRxDevice("Rx");
		RegisterTxDevice("Tx");

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
					Debug.Log(link);
				}
			}
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				// var jointState = (attachedDevices as Articulation).GetState();
				// var transformPartsName = requestValue.StringValue;
				// var devicePose = jointState.GetPartsPose(transformPartsName);
				// SetTransformInfoResponse(ref response, devicePose);
				break;

			default:
				break;
		}
	}
}