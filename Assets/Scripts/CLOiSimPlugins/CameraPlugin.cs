/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using Stopwatch = System.Diagnostics.Stopwatch;

public class CameraPlugin : CLOiSimPlugin
{
	private SensorDevices.Camera cam = null;

	public string subPartName = string.Empty;

	public SensorDevices.Camera GetCamera()
	{
		return cam;
	}

	protected override void OnAwake()
	{
		var depthcam = gameObject.GetComponent<SensorDevices.DepthCamera>();
		if (depthcam is null)
		{
			ChangePluginType(Type.CAMERA);
			cam = gameObject.GetComponent<SensorDevices.Camera>();
		}
		else
		{
			ChangePluginType(Type.DEPTHCAMERA);
			cam = depthcam;
		}

		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		RegisterServiceDevice(subPartName + "Info");
		RegisterTxDevice(subPartName + "Data");

		AddThread(Response);
		AddThread(Sender);
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (IsRunningThread)
		{
			if (cam != null)
			{
				var datastreamToSend = cam.PopData();
				sw.Restart();
				Publish(datastreamToSend);
				sw.Stop();
				cam.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
		}
	}

	private void Response()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForInfoResponse);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				switch (requestMessage.Name)
				{
					case "request_ros2":
						var topic_name = parameters.GetValue<string>("ros2/topic_name");
						var frame_id = parameters.GetValue<string>("ros2/frame_id");
						SetROS2CommonInfoResponse(ref msForInfoResponse, topic_name, frame_id);
						break;

					case "request_camera_info":
						var cameraInfoMessage = cam.GetCameraInfo();
						SetCameraInfoResponse(ref msForInfoResponse, cameraInfoMessage);
						break;

					case "request_transform":
						var isSubParts = string.IsNullOrEmpty(subPartName);
						var devicePose = cam.GetPose(isSubParts);
						SetTransformInfoResponse(ref msForInfoResponse, devicePose);
						break;

					default:
						break;
				}

				SendResponse(msForInfoResponse);
			}

			ThreadWait();
		}
	}
}