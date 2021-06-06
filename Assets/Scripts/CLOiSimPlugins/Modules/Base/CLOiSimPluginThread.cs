/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;
using messages = cloisim.msgs;
using Stopwatch = System.Diagnostics.Stopwatch;
using Any = cloisim.msgs.Any;

public class CLOiSimPluginThread : Transporter
{
	private bool runningThread = true;
	public bool IsRunning => runningThread;

	private List<(Thread, System.Object)> threadList = new List<(Thread, System.Object)>();

	public delegate void RefAction<T1, T2, T3>(in T1 arg1, in T2 arg2, ref T3 arg3);
	public delegate void RefAction<T1, T2>(in T1 arg1, ref T2 arg3);

	public RefAction<string, messages.Any, DeviceMessage> HandleRequestTypeValue = delegate (in string requestType, in Any requestValue, ref DeviceMessage response) { };
	public RefAction<string, List<messages.Param>, DeviceMessage> HandleRequestTypeChildren = delegate (in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response) { };

	~CLOiSimPluginThread()
	{
		Dispose();
	}

	public override void Dispose()
	{
		// Debug.Log("Destroy Thread");
		Stop();
		base.Dispose();
	}

	public bool Add(in ThreadStart function)
	{
		if (function != null)
		{
			var thread = new Thread(function);
			thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			threadList.Add((thread, null));
			return true;
		}

		return false;
	}

	public bool Add(in ParameterizedThreadStart function, in Device paramDeviceObject)
	{
		if (function != null)
		{
			var thread = new Thread(function);
			thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			threadList.Add((thread, paramDeviceObject as System.Object));
			return true;
		}

		return false;
	}

	public void Start()
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

	public void Stop()
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
		threadList.Clear();
	}

	public void Sender(System.Object deviceParam)
	{
		if (Publisher != null)
		{
			var sw = new Stopwatch();
			var device = deviceParam as Device;
			while (IsRunning && device != null)
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

	public void Receiver(System.Object deviceParam)
	{
		if (Subscriber != null)
		{
			var device = deviceParam as Device;
			while (IsRunning && device != null)
			{
				var receivedData = Subscriber.Subscribe();
				device.PushDeviceMessage(receivedData);

				Wait();
			}
		}
		else
		{
			Debug.LogWarning("Subscriber is null");
		}
	}

	public void Service()
	{
		if (Responsor != null)
		{
			var dmResponse = new DeviceMessage();
			while (IsRunning)
			{
				var receivedBuffer = Responsor.ReceiveRequest();

				if (receivedBuffer != null)
				{
					var requestMessage = ParsingRequestMessage(receivedBuffer);

					if (requestMessage != null && dmResponse != null)
					{
						HandleRequestTypeValue(requestMessage.Name, requestMessage.Value, ref dmResponse);
						HandleRequestTypeChildren(requestMessage.Name, requestMessage.Childrens, ref dmResponse);
					}
					else
					{
						Debug.Log("DeviceMessage for response or requestMesasge is null");
					}

					Responsor.SendResponse(dmResponse);
				}

				Wait();
			}
		}
		else
		{
			Debug.LogWarning("Responsor is null");
		}
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

	public static void Wait(in int iteration = 1)
	{
		Thread.SpinWait(iteration);
	}

	public static void Sleep(in int millisecondsTimeout = 1)
	{
		Thread.Sleep(millisecondsTimeout);
	}
}