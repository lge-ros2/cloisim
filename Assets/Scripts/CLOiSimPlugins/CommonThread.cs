/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using messages = cloisim.msgs;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CommonThread : DeviceTransporter
{
	private bool runningThread = true;
	protected bool IsRunningThread => runningThread;

	private List<(Thread, System.Object)> threadList = new List<(Thread, System.Object)>();

	protected void OnDestroy()
	{
		StopThread();
	}

	protected bool AddThread(in ThreadStart function)
	{
		if (function != null)
		{
			threadList.Add((new Thread(function), null));
			// thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			return true;
		}

		return false;
	}

	protected bool AddThread(in ParameterizedThreadStart function, in System.Object paramObject)
	{
		if (function != null)
		{
			threadList.Add((new Thread(function), paramObject));
			// thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			return true;
		}

		return false;
	}

	protected void StartThreads()
	{
		runningThread = true;

		foreach (var threadTuple in threadList)
		{
			var thread = threadTuple.Item1;
			if (thread != null && !thread.IsAlive)
			{
				var paramObject = threadTuple.Item2;

				if (paramObject == null)
				{
					thread.Start();
				}
				else
				{
					thread.Start(paramObject);
				}
			}
		}
	}

	public void StopThread()
	{
		runningThread = false;

		foreach (var threadTuple in threadList)
		{
			var thread = threadTuple.Item1;
			if (thread != null)
			{
				if (thread.IsAlive)
				{
					thread.Join();
					thread.Abort();
				}
			}
		}
	}

	protected void SenderThread(System.Object deviceParam)
	{
		var sw = new Stopwatch();
		var device = deviceParam as Device;
		while (runningThread && device != null)
		{
			if (device.PopDeviceMessage(out var dataStreamToSend))
			{
				sw.Restart();
				Publish(dataStreamToSend);
				sw.Stop();
				device.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
			}
		}
	}

	protected void ResponseThread(System.Object deviceParam)
	{
		var dmInfoResponse = new DeviceMessage();

		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();
			var requestMessage = ParsingRequestMessage(receivedBuffer);

			// Debug.Log(subPartName + receivedString);
			if (requestMessage != null)
			{
				// switch (requestMessage.Name)
				// {
				// 	case "request_ros2":
				// 		var topic_name = parameters.GetValue<string>("ros2/topic_name");
				// 		var frame_id = parameters.GetValue<string>("ros2/frame_id");
				// 		SetROS2CommonInfoResponse(ref dmInfoResponse, topic_name, frame_id);
				// 		break;

				// 	case "request_camera_info":
				// 		var cameraInfoMessage = cam.GetCameraInfo();
				// 		SetCameraInfoResponse(ref dmInfoResponse, cameraInfoMessage);
				// 		break;

				// 	case "request_transform":
				// 		var isSubParts = string.IsNullOrEmpty(subPartName);
				// 		var devicePose = cam.GetPose(isSubParts);
				// 		SetTransformInfoResponse(ref dmInfoResponse, devicePose);
				// 		break;

				// 	default:
				// 		// HandleCustomRequestMessage();
				// 		break;
				// }

				SendResponse(dmInfoResponse);
			}

			WaitThread();
		}
	}

	protected void WaitThread(in int iteration = 1)
	{
		Thread.SpinWait(iteration);

	}
	protected static messages.Param ParsingRequestMessage(in byte[] infoBuffer)
	{
		if (infoBuffer != null)
		{
			var deviceMessage = new DeviceMessage();
			deviceMessage.SetMessage(infoBuffer);
			return deviceMessage.GetMessage<messages.Param>();
		}

		return null;
	}
}