
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using ProtoBuf;
using messages = gazebo.msgs;

public abstract partial class DevicePlugin : DeviceTransporter, IDevicePlugin
{
	public static messages.Param ParsingInfoRequest(in byte[] srcReceivedBuffer, ref MemoryStream dstCameraInfoMemStream)
	{
		if (srcReceivedBuffer == null)
		{
			return null;
		}

		ClearMemoryStream(ref dstCameraInfoMemStream);

		dstCameraInfoMemStream.Write(srcReceivedBuffer, 0, srcReceivedBuffer.Length);
		dstCameraInfoMemStream.Position = 0;

		return Serializer.Deserialize<messages.Param>(dstCameraInfoMemStream);
	}

	public static void SetCameraInfoResponse(ref MemoryStream msCameraInfo, in messages.CameraSensor sensorInfo)
	{
		if (msCameraInfo == null || sensorInfo == null)
		{
			return;
		}

		ClearMemoryStream(ref msCameraInfo);
		Serializer.Serialize<messages.CameraSensor>(msCameraInfo, sensorInfo);
	}

	public static void SetTransformInfoResponse(ref MemoryStream msTransformInfo, in Pose devicePose)
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
		objectTransformInfo.Value = new messages.Any();
		objectTransformInfo.Value.Type = messages.Any.ValueType.Pose3d;
		objectTransformInfo.Value.Pose3dValue = objectPose;

		ClearMemoryStream(ref msTransformInfo);
		Serializer.Serialize<messages.Param>(msTransformInfo, objectTransformInfo);
	}

	public static void SetROS2CommonInfoResponse(ref MemoryStream msRos2Info, in string topicName, in string frameId)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "ros2";
		ros2CommonInfo.Value = new messages.Any();
		ros2CommonInfo.Value.Type = messages.Any.ValueType.None;

		var ros2TopicName = new messages.Param();
		ros2TopicName.Name = "topic_name";
		ros2TopicName.Value = new messages.Any();
		ros2TopicName.Value.Type = messages.Any.ValueType.String;
		ros2TopicName.Value.StringValue = topicName;

		var ros2FrameId = new messages.Param();
		ros2FrameId.Name = "frame_id";
		ros2FrameId.Value = new messages.Any();
		ros2FrameId.Value.Type = messages.Any.ValueType.String;
		ros2FrameId.Value.StringValue = frameId;

		ros2CommonInfo.Childrens.Add(ros2TopicName);
		ros2CommonInfo.Childrens.Add(ros2FrameId);

		ClearMemoryStream(ref msRos2Info);
		Serializer.Serialize<messages.Param>(msRos2Info, ros2CommonInfo);

	}
}