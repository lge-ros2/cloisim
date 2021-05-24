/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using messages = cloisim.msgs;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CLOiSimPluginThread : DeviceTransporter
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

	protected bool AddThread(in ParameterizedThreadStart function, in Device paramDeviceObject)
	{
		if (function != null)
		{
			threadList.Add((new Thread(function), paramDeviceObject as System.Object));
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

	protected void ReceiverThread(System.Object deviceParam)
	{
		var device = deviceParam as Device;
		while (IsRunningThread && device != null)
		{
			var receivedData = Subscribe();
			device.PushDeviceMessage(receivedData);

			WaitThread();
		}
	}

	protected void RequestThread()
	{
		var dmResponse = new DeviceMessage();
		while (runningThread)
		{
			var receivedBuffer = ReceiveRequest();
			var requestMessage = ParsingRequestMessage(receivedBuffer);

			if (requestMessage != null)
			{
				var requesteValue = (requestMessage.Value == null) ? string.Empty : requestMessage.Value.StringValue;
				HandleRequestMessage(requestMessage.Name, requesteValue, ref dmResponse);
				SendResponse(dmResponse);
			}

			WaitThread();
		}
	}

	protected virtual void HandleRequestMessage(in string requestType, in string requestValue, ref DeviceMessage response) { }

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

	protected void WaitThread(in int iteration = 1)
	{
		Thread.SpinWait(iteration);
	}

	protected void SleepThread(in int millisecondsTimeout = 1)
	{
		Thread.Sleep(millisecondsTimeout);
	}
}