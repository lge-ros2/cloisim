/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;
using UnityEngine;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> staticTfList = new List<TF>();
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
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) && linkHelper.Model.name.Equals(modelName))) &&
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
			case "request_static_transforms":
				SetStaticTransformsResponse(ref response);
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

		foreach (var tf in staticTfList)
		{
			var ros2StaticTransformLink = new messages.Param();
			ros2StaticTransformLink.Name = "parent_frame_id";
			ros2StaticTransformLink.Value = new Any { Type = Any.ValueType.String, StringValue = tf.parentFrameId };

			{
				var tfPose = tf.GetPose();

				var poseMessage = new messages.Pose();
				poseMessage.Position = new messages.Vector3d();
				poseMessage.Orientation = new messages.Quaternion();

				poseMessage.Name = tf.childFrameId;
				DeviceHelper.SetVector3d(poseMessage.Position, tfPose.position);
				DeviceHelper.SetQuaternion(poseMessage.Orientation, tfPose.rotation);

				var ros2StaticTransformElement = new messages.Param();
				ros2StaticTransformElement.Name = "pose";
				ros2StaticTransformElement.Value = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = poseMessage};

				ros2StaticTransformLink.Childrens.Add(ros2StaticTransformElement);
				// Debug.Log(poseMessage.Name + ", " + poseMessage.Position + ", " + poseMessage.Orientation);
			}

			ros2CommonInfo.Childrens.Add(ros2StaticTransformLink);
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}
