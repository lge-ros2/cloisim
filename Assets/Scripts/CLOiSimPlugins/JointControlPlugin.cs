/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

public class JointControlPlugin : CLOiSimPlugin
{
	private List<TF> tfList = new List<TF>();
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
			foreach (var linkName in links)
			{
				if (jointState != null)
				{
					var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("joints/link[text()='" + linkName + "']", "parent_frame_id", "base_link");

					if (jointState.AddTarget(linkName, out var targetLink))
					{
						var tf = new TF(targetLink, linkName, parentFrameId);
						tfList.Add(tf);
						// Debug.Log(link);
					}
				}
			}
		}
	}
}