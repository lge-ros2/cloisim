
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
		ClearMemoryStream(ref dstCameraInfoMemStream);

		dstCameraInfoMemStream.Write(srcReceivedBuffer, 0, srcReceivedBuffer.Length);
		dstCameraInfoMemStream.Position = 0;

		return Serializer.Deserialize<messages.Param>(dstCameraInfoMemStream);
	}

	public static void SetCameraInfoResponse(ref MemoryStream msCameraInfo, in messages.CameraSensor sensorInfo)
	{
		if (msCameraInfo != null && sensorInfo != null)
		{
			ClearMemoryStream(ref msCameraInfo);
			Serializer.Serialize<messages.CameraSensor>(msCameraInfo, sensorInfo);
		}
	}

	public static void SetTransformInfoResponse(ref MemoryStream msCameraInfo, in Pose devicePose)
	{
		if (msCameraInfo != null)
		{
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

			ClearMemoryStream(ref msCameraInfo);
			Serializer.Serialize<messages.Param>(msCameraInfo, objectTransformInfo);
		}
	}
}