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
	private List<TF> staticTfList = new List<TF>();
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
					var tf = new TF(targetLink);

					if (isStatic)
						staticTfList.Add(tf);
					else
						tfList.Add(tf);
				}
			}
		}
		// UnityEngine.Debug.Log("Joints loaded");
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
				ros2StaticTransformElement.Value = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = poseMessage };

				ros2StaticTransformLink.Childrens.Add(ros2StaticTransformElement);
				// Debug.Log(poseMessage.Name + ", " + poseMessage.Position + ", " + poseMessage.Orientation);
			}

			ros2CommonInfo.Childrens.Add(ros2StaticTransformLink);
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}