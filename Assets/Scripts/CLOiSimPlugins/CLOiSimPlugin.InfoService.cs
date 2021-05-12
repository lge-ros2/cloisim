
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using ProtoBuf;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public abstract partial class CLOiSimPlugin : CommonThread, ICLOiSimPlugin
{
	protected static messages.Param ParsingInfoRequest(in byte[] srcReceivedBuffer, ref DeviceMessage dstCameraInfoMemStream)
	{
		if (srcReceivedBuffer == null)
		{
			return null;
		}

		ClearDeviceMessage(ref dstCameraInfoMemStream);

		dstCameraInfoMemStream.Write(srcReceivedBuffer, 0, srcReceivedBuffer.Length);
		dstCameraInfoMemStream.Position = 0;

		return Serializer.Deserialize<messages.Param>(dstCameraInfoMemStream);
	}

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

	protected static void SetROS2CommonInfoResponse(ref DeviceMessage msRos2Info, in string topicName, in string frameId)
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

		var ros2FrameId = new messages.Param();
		ros2FrameId.Name = "frame_id";
		ros2FrameId.Value = new Any { Type = Any.ValueType.String, StringValue = frameId };

		ros2CommonInfo.Childrens.Add(ros2TopicName);
		ros2CommonInfo.Childrens.Add(ros2FrameId);

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
}