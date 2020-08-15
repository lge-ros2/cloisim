/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.IO;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using ProtoBuf;
using messages = gazebo.msgs;

public class CameraPlugin : DevicePlugin
{
	private SensorDevices.Camera cam = null;

	public string subPartName = string.Empty;

	protected override void OnAwake()
	{
		cam = gameObject.GetComponent<SensorDevices.Camera>();
		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var hashServiceKey = MakeHashKey(partName, subPartName + "Info");
		if (!RegisterServiceDevice(hashServiceKey))
		{
			Debug.LogError("Failed to register ElevatorSystem service - " + hashServiceKey);
		}

		var hashTxKey = MakeHashKey(partName, subPartName);
		if (!RegisterTxDevice(hashTxKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashTxKey);
		}

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		Stopwatch sw = new Stopwatch();
		while (true)
		{
			if (cam == null)
			{
				continue;
			}

			var datastreamToSend = cam.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			cam.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}

	private void Response()
	{
		while (true)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_camera_info":
						var cameraInfoMessage = cam.GetCameraInfo();
						SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);
						break;

					case "request_transform":
						var devicePosition = cam.GetDevicePosition();
						var deviceRotation = cam.GetDeviceRotation();

						SetTransformInfoResponse(ref msForInfoResponse, devicePosition, deviceRotation);
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}
		}
	}

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

	public static void SetTransformInfoResponse(ref MemoryStream msCameraInfo, in Vector3 objectPos, in Quaternion objectRot)
	{
		if (msCameraInfo != null)
		{
			var objectPose = new messages.Pose();
			objectPose.Position = new messages.Vector3d();
			objectPose.Orientation = new messages.Quaternion();

			DeviceHelper.SetVector3d(objectPose.Position, objectPos);
			DeviceHelper.SetQuaternion(objectPose.Orientation, objectRot);

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