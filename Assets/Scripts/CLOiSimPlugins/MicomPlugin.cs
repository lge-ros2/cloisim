/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;
using UnityEngine;

public class MicomPlugin : CLOiSimPlugin
{
	private Dictionary<string, (Pose, string)> otherPartsPoseTable = new Dictionary<string, (Pose, string)>();
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

		LoadTFs();
	}

	private void SetupMicom()
	{
		micomSensor.EnableDebugging = GetPluginParameters().GetValue<bool>("debug", false);

		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		micomSensor.SetUpdateRate(updateRate);

		var wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");
		var wheelTread = GetPluginParameters().GetValue<float>("wheel/tread");
		var P = GetPluginParameters().GetValue<float>("wheel/PID/kp");
		var I = GetPluginParameters().GetValue<float>("wheel/PID/ki");
		var D = GetPluginParameters().GetValue<float>("wheel/PID/kd");

		micomSensor.SetMotorConfiguration(wheelRadius, wheelTread, P, I, D);

		var wheelNameLeft = GetPluginParameters().GetValue<string>("wheel/location[@type='left']");
		var wheelNameRight = GetPluginParameters().GetValue<string>("wheel/location[@type='right']");

		// TODO: to be utilized
		var motorFriction = GetPluginParameters().GetValue<float>("wheel/friction/motor", 0.1f); // Currently not used
		var brakeFriction = GetPluginParameters().GetValue<float>("wheel/friction/brake", 0.1f); // Currently not used

		micomSensor.SetWheel(wheelNameLeft, wheelNameRight);

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

	private void LoadTFs()
	{
		var linkHelpers = GetComponentsInChildren<SDF.Helper.Link>();

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			foreach (var linkName in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/static_transforms/link[text()='" + linkName + "']", "parent_frame_id", "base_link");

				foreach (var linkHelper in linkHelpers)
				{
					if (linkHelper.name.Equals(linkName))
					{
						var initialTfPose = linkHelper.GetPose();

						if (!linkHelper.Model.Equals(linkHelper.RootModel))
						{
							initialTfPose = initialTfPose.GetTransformedBy(linkHelper.Model.GetPose());
						}

						StorePose(linkName, parentFrameId, initialTfPose);

						Debug.Log(linkName + " : TF static added");
						break;
					}
				}
			}
		}

		if (GetPluginParameters().GetValues<string>("ros2/transforms/link", out var links))
		{
			foreach (var link in links)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				var modelName = string.Empty;
				var linkName = link;
				if (link.Contains("::"))
				{
					var splittedName = link.Replace("::", ":").Split(':');
					modelName = splittedName[0];
					linkName = splittedName[1];
				}

				foreach (var linkHelper in linkHelpers)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) && linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link.Replace("::", "_"), parentFrameId);
						tfList.Add(tf);
						Debug.Log(modelName + "::" + linkName + " : TF added");
						break;
					}
				}
			}
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_static_transforms":
				SetStaticTransformsResponse(ref response);
				break;

			case "request_transform":
				var devicePartsName = requestValue.StringValue;
				var devicePose = GetPose(devicePartsName, out var parentLinkName);
				SetTransformInfoResponse(ref response, devicePartsName, devicePose, parentLinkName);
				break;

			case "reset_odometry":
				Reset();
				SetEmptyResponse(ref response);
				break;

			default:
				break;
		}
	}

	private void SetStaticTransformsResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "static_transforms";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			var ros2StaticTransformLink = new messages.Param();

			foreach (var linkName in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/static_transforms/link[text()='" + linkName + "']", "parent_frame_id", "base_link");

				ros2StaticTransformLink.Name = linkName;
				ros2StaticTransformLink.Value = new Any { Type = Any.ValueType.String, StringValue = parentFrameId };

				ros2CommonInfo.Childrens.Add(ros2StaticTransformLink);
			}
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}


	public void StorePose(in string targetLinkName, in string parentLinkName, in Pose initialPose)
	{
		otherPartsPoseTable.Add(targetLinkName, (initialPose, parentLinkName));
	}

	public Pose GetPose(in string targetLinkName, out string parentLinkName)
	{
		if (otherPartsPoseTable.TryGetValue(targetLinkName, out var item))
		{
			parentLinkName = item.Item2;
			return item.Item1;
		}
		parentLinkName = null;
		return Pose.identity;
	}
}