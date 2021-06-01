/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using messages = cloisim.msgs;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CLOiSimPluginThread : Transporter
{
	private bool runningThread = true;
	protected bool IsRunningThread => runningThread;

	private List<(Thread, System.Object)> threadList = new List<(Thread, System.Object)>();

	protected new void OnDestroy()
	{
		StopThread();

		base.OnDestroy();
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
		if (Publisher != null)
		{
			var sw = new Stopwatch();
			var device = deviceParam as Device;
			while (runningThread && device != null)
			{
				if (device.PopDeviceMessage(out var dataStreamToSend))
				{
					sw.Restart();
					if (Publisher.Publish(dataStreamToSend))
					{
						sw.Stop();
						device.SetTransportedTime((float)sw.Elapsed.TotalSeconds);
					}
				}
			}
		}
		else
		{
			Debug.LogWarning("publihser is null");
		}
	}

	protected void ReceiverThread(System.Object deviceParam)
	{
		if (Subscriber != null)
		{
			var device = deviceParam as Device;
			while (IsRunningThread && device != null)
			{
				var receivedData = Subscriber.Subscribe();
				device.PushDeviceMessage(receivedData);

				WaitThread();
			}
		}
		else
		{
			Debug.LogWarning("Subscriber is null");
		}
	}

	protected void ServiceThread()
	{
		if (Responsor != null)
		{
			var dmResponse = new DeviceMessage();
			while (runningThread)
			{
				var receivedBuffer = Responsor.ReceiveRequest();

				if (receivedBuffer != null)
				{
					var requestMessage = ParsingRequestMessage(receivedBuffer);

					if (requestMessage != null && dmResponse != null)
					{
						HandleRequestMessage(requestMessage, ref dmResponse);
					}
					else
					{
						Debug.Log("DeviceMessage for response or requestMesasge is null");
					}

					Responsor.SendResponse(dmResponse);
				}

				WaitThread();
			}
		}
		else
		{
			Debug.LogWarning("Responsor is null");
		}
	}

	protected virtual void HandleRequestMessage(in string requestType, in messages.Any requestValue, ref DeviceMessage response) { }
	protected virtual void HandleRequestMessage(in messages.Param requestMessage, ref DeviceMessage response)
	{
		HandleRequestMessage(requestMessage.Name, requestMessage.Value, ref response);
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

	protected void WaitThread(in int iteration = 1)
	{
		Thread.SpinWait(iteration);
	}

	protected void SleepThread(in int millisecondsTimeout = 1)
	{
		Thread.Sleep(millisecondsTimeout);
	}
}