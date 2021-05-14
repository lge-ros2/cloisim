
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public abstract partial class CLOiSimPlugin : CLOiSimPluginThread, ICLOiSimPlugin
{
	protected static void SetCameraInfoResponse(ref DeviceMessage msCameraInfo, in messages.CameraSensor sensorInfo)
	{
		if (msCameraInfo == null || sensorInfo == null)
		{
			return;
		}

		msCameraInfo.SetMessage<messages.CameraSensor>(sensorInfo);
	}

	protected static void SetTransformInfoResponse(ref DeviceMessage msTransformInfo, in Pose devicePose)
	{
		if (msTransformInfo == null)
		{
			return;
		}

		var objectPose = new messages.Pose();
		objectPose.Position = new messages.Vector3d();
		objectPose.Orientation = new messages.Quaternion();

		DeviceHelper.SetVector3d(objectPose.Position, devicePose.position);
		DeviceHelper.SetQuaternion(objectPose.Orientation, devicePose.rotation);

		var objectTransformInfo = new messages.Param();
		objectTransformInfo.Name = "transform";
		objectTransformInfo.Value = new Any { Type = Any.ValueType.Pose3d, Pose3dValue = objectPose };

		msTransformInfo.SetMessage<messages.Param>(objectTransformInfo);
	}

	protected static void SetROS2CommonInfoResponse(ref DeviceMessage msRos2Info, in string topicName, in List<string> frameIdList)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "ros2";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		var ros2TopicName = new messages.Param();
		ros2TopicName.Name = "topic_name";
		ros2TopicName.Value = new Any { Type = Any.ValueType.String, StringValue = topicName };
		ros2CommonInfo.Childrens.Add(ros2TopicName);

		foreach (var frameId in frameIdList)
		{
			var ros2FrameId = new messages.Param();
			ros2FrameId.Name = "frame_id";
			ros2FrameId.Value = new Any { Type = Any.ValueType.String, StringValue = frameId };
			ros2CommonInfo.Childrens.Add(ros2FrameId);
		}

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}

	protected static void SetEmptyResponse(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info != null)
		{
			var emptyMessage = new messages.Empty();
			emptyMessage.Unused = true;
			msRos2Info.SetMessage<messages.Empty>(emptyMessage);
		}
	}

	protected override void HandleRequestMessage(in string requestType, in string requestValue,  ref DeviceMessage response)
	{
		if (response == null)
		{
			Debug.Log("DeviceMessage for response is null");
			return;
		}

		switch (requestType)
		{
			case "request_ros2":
				var topic_name = GetPluginParameters().GetValue<string>("ros2/topic_name");
				GetPluginParameters().GetValues<string>("ros2/frame_id", out var frameIdList);
				SetROS2CommonInfoResponse(ref response, topic_name, frameIdList);
				break;

			default:
				HandleCustomRequestMessage(requestType, requestValue, ref response);
				break;
		}
	}

	protected virtual void HandleCustomRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response) { }
}