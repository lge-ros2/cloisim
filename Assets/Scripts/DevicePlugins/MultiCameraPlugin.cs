/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using ProtoBuf;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class MultiCameraPlugin : DevicePlugin
{
	private SensorDevices.MultiCamera multicam = null;

	protected override void OnAwake()
	{
		type = Type.MULTICAMERA;
		partName = DeviceHelper.GetPartName(gameObject);
		multicam = gameObject.GetComponent<SensorDevices.MultiCamera>();
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Info");
		RegisterTxDevice("Data");

		AddThread(Sender);
		AddThread(Response);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (multicam != null)
			{
 				var datastreamToSend = multicam.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				multicam.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
		}
	}

	private void Response()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = CameraPlugin.ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			if (requestMessage != null)
			{
				var cameraName = (requestMessage.Value == null) ? string.Empty : requestMessage.Value.StringValue;

				switch (requestMessage.Name)
				{
					case "request_ros2":
						if (parameters.GetValues<string>("ros2/frames_id/frame_id", out var frames_id))
						{
							SetROS2FramesIdInfoResponse(ref msForInfoResponse, frames_id);
						}
						break;

					case "request_camera_info":
						{
							var camera = multicam.GetCamera(cameraName);
							var cameraInfoMessage = camera.GetCameraInfo();
							CameraPlugin.SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);
						}
						break;

					case "request_transform":
						{
							var camera = multicam.GetCamera(cameraName);
							var devicePose = camera.GetPose();
							SetTransformInfoResponse(ref msForInfoResponse, devicePose);
						}
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}

			ThreadWait();
		}
	}

	private void SetROS2FramesIdInfoResponse(ref MemoryStream msForInfoResponse, in List<string> frames_id)
	{
		if (msForInfoResponse == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "ros2";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.None };

		var ros2FramesIdInfo = new messages.Param();
		ros2FramesIdInfo.Name = "frames_id";
		ros2FramesIdInfo.Value = new Any { Type = Any.ValueType.None };
		ros2CommonInfo.Childrens.Add(ros2FramesIdInfo);

		foreach (var frame_id in frames_id)
		{
			var ros2FrameId = new messages.Param();
			ros2FrameId.Name = "frame_id";
			ros2FrameId.Value = new Any { Type = Any.ValueType.String, StringValue = frame_id };
			ros2FramesIdInfo.Childrens.Add(ros2FrameId);
		}

		ClearMemoryStream(ref msForInfoResponse);
		Serializer.Serialize<messages.Param>(msForInfoResponse, ros2CommonInfo);
	}
}