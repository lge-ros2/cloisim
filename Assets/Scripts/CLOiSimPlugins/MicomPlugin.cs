/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using Any = cloisim.msgs.Any;
using UnityEngine;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> tfList = new List<TF>();
	private SensorDevices.MicomCommand micomCommand = null;
	private SensorDevices.MicomSensor micomSensor = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MICOM;
		micomSensor = gameObject.AddComponent<SensorDevices.MicomSensor>();
		micomCommand = gameObject.AddComponent<SensorDevices.MicomCommand>();
		micomCommand.SetMotorControl(micomSensor.MotorControl);

		attachedDevices.Add("Command", micomCommand);
		attachedDevices.Add("Sensor", micomSensor);
	}

	protected override void OnStart()
	{
		SetupMicom();

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, micomCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, micomSensor);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, tfList);
		}

		LoadStaticTF();
		LoadTF();
	}

	private void SetupMicom()
	{
		micomSensor.EnableDebugging = GetPluginParameters().GetValue<bool>("debug", false);

		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20f);
		if (updateRate.Equals(0))
		{
			Debug.LogWarning("Update rate for micom CANNOT be 0. Set to default value 20 Hz");
			updateRate = 20f;
		}
		micomSensor.SetUpdateRate(updateRate);

		var wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");
		var wheelTread = GetPluginParameters().GetValue<float>("wheel/tread");
		var P = GetPluginParameters().GetValue<float>("wheel/PID/kp");
		var I = GetPluginParameters().GetValue<float>("wheel/PID/ki");
		var D = GetPluginParameters().GetValue<float>("wheel/PID/kd");

		micomSensor.SetMotorConfiguration(wheelRadius, wheelTread, P, I, D);

		// TODO: to be utilized, currently not used
		var motorFriction = GetPluginParameters().GetValue<float>("wheel/friction/motor", 0.1f);
		var brakeFriction = GetPluginParameters().GetValue<float>("wheel/friction/brake", 0.1f);

		var wheelLeftName = GetPluginParameters().GetValue<string>("wheel/location[@type='left']", string.Empty);
		var wheelRightName = GetPluginParameters().GetValue<string>("wheel/location[@type='right']", string.Empty);
		var rearWheelLeftName = GetPluginParameters().GetValue<string>("wheel/location[@type='rear_left']", string.Empty);
		var rearWheelRightName = GetPluginParameters().GetValue<string>("wheel/location[@type='rear_right']", string.Empty);

		if (!rearWheelLeftName.Equals(string.Empty) && !rearWheelRightName.Equals(string.Empty))
		{
			micomSensor.SetWheel(wheelLeftName, wheelRightName, rearWheelLeftName, rearWheelRightName);
		}
		else
		{
			micomSensor.SetWheel(wheelLeftName, wheelRightName);
		}

		if (GetPluginParameters().GetValues<string>("uss/sensor", out var ussList))
		{
			micomSensor.SetUSS(ussList);
		}

		if (GetPluginParameters().GetValues<string>("ir/sensor", out var irList))
		{
			micomSensor.SetIRSensor(irList);
		}

		if (GetPluginParameters().GetValues<string>("magnet/sensor", out var magnetList))
		{
			micomSensor.SetMagnet(magnetList);
		}

		var targetContactName = GetPluginParameters().GetAttribute<string>("bumper", "contact");
		micomSensor.SetBumper(targetContactName);

		if (GetPluginParameters().GetValues<string>("bumper/joint_name", out var bumperJointNameList))
		{
			micomSensor.SetBumperSensor(bumperJointNameList);
		}
	}

	private void LoadStaticTF()
	{
		var staticTfLog = new StringBuilder();
		staticTfLog.AppendLine("Loaded Static TF Info : " + modelName);
		var linkHelpers = GetComponentsInChildren<SDF.Helper.Link>();

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			foreach (var link in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/static_transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				(var modelName, var linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in linkHelpers)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) &&
						linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						staticTfList.Add(tf);
						staticTfLog.AppendLine(modelName + "::" + linkName + " : static TF added");
						break;
					}
				}
			}
		}

		Debug.Log(staticTfLog.ToString());
	}

	private void LoadTF()
	{
		var tfLog = new StringBuilder();
		tfLog.AppendLine("Loaded TF Info : " + modelName);
		var linkHelpers = GetComponentsInChildren<SDF.Helper.Link>();
		if (GetPluginParameters().GetValues<string>("ros2/transforms/link", out var links))
		{
			foreach (var link in links)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				(var modelName, var linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in linkHelpers)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) && linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						tfList.Add(tf);
						tfLog.AppendLine(modelName + "::" + linkName + " : TF added");
						break;
					}
				}
			}
		}

		Debug.Log(tfLog.ToString());
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "reset_odometry":
				Reset();
				SetEmptyResponse(ref response);
				break;

			default:
				break;
		}
	}
}
