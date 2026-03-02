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

public class CLOiSimPluginThread : IDisposable
{
	public class ParamObject
	{
		public ushort targetPort;
		public System.Object param;

		public ParamObject(in ushort targetPort, in System.Object parameter)
		{
			this.targetPort = targetPort;
			this.param = parameter;
		}
	}

	private List<(Thread, ParamObject)> _threadList = new();

	private bool _runningThread = true;
	public bool IsRunning => _runningThread;

	public delegate void RefAction<T1, T2, T3>(in T1 arg1, in T2 arg2, ref T3 arg3);
	public delegate void RefAction<T1, T2>(in T1 arg1, ref T2 arg3);

	public RefAction<string, messages.Any, DeviceMessage> HandleRequestTypeValue = delegate (in string requestType, in Any requestValue, ref DeviceMessage response) { };
	public RefAction<string, List<messages.Param>, DeviceMessage> HandleRequestTypeChildren = delegate (in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response) { };

	~CLOiSimPluginThread()
	{
		// Debug.Log("Destroy Thread");
		RequestStop();
	}

	public virtual void Dispose()
	{
		// Debug.Log("Dispose Thread");
		RequestStop();
	}

	public bool Add(in ushort targetPortForThread, in ParameterizedThreadStart function, in System.Object pluginObject = null)
	{
		if (function != null)
		{
			var thread = new Thread(function);
			thread.IsBackground = true;
			var paramObject = new ParamObject(targetPortForThread, pluginObject);
			// thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			_threadList.Add((thread, paramObject));
			return true;
		}

		return false;
	}

	public void Start()
	{
		_runningThread = true;

		foreach (var threadTuple in _threadList)
		{
			var thread = threadTuple.Item1;
			if (thread != null && !thread.IsAlive)
			{
				var threadObject = threadTuple.Item2;
				thread.Start(threadObject);
			}
		}
	}

	public void RequestStop()
	{
		_runningThread = false;
		GC.SuppressFinalize(this);
	}

	public bool TryJoinStep(in int joinTimeoutMs = 50)
	{
		var allStopped = true;
		foreach (var threadTuple in _threadList)
		{
			var thread = threadTuple.Item1;

			if (thread == null) continue;
			if (!thread.IsAlive) continue;

			if (thread.Join(joinTimeoutMs))
			{
#if UNITY_EDITOR
				Debug.LogWarning($"Thread({thread.ManagedThreadId}) did not stop within {joinTimeoutMs}ms");
#endif
			}

			if (thread.IsAlive)
				allStopped = false;
		}

		if (allStopped)
		{
#if UNITY_EDITOR
			Debug.LogWarning($"All Thread stopped!!");
#endif
			_threadList.Clear();
		}

		return allStopped;
	}

	public void Sender(Publisher publisher, Device device)
	{
		if (publisher == null)
		{
			Debug.LogWarning("Publisher is null");
			return;
		}

		var sw = new Stopwatch();
		while (IsRunning && device != null)
		{
			if (device.PopDeviceMessage(out var dataStreamToSend))
			{
				var t0 = Stopwatch.GetTimestamp();
				if (publisher.Publish(dataStreamToSend))
				{
					var t1 = Stopwatch.GetTimestamp();
					var transportingTime = (float)((t1 - t0) / (double)Stopwatch.Frequency);
					// Debug.Log($"{transportingTime:F5}");
					device.SetTransportedTime(transportingTime);
				}

				// Return to pool for reuse — avoids per-frame MemoryStream allocation
				Device.ReturnDeviceMessage(dataStreamToSend);
			}
			else
			{
				// Yield instead of Sleep(1) to minimize latency —
				// Sleep(1) has ~1-4ms granularity on Linux, which caps
				// throughput for high-rate sensors (1000Hz JointState, etc.)
				Wait();
			}
		}
	}

	public void Receiver(Subscriber subscriber, Device device)
	{
		if (subscriber == null)
		{
			Debug.LogWarning("Subscriber is null");
			return;
		}

		while (IsRunning && device != null)
		{
			var receivedData = subscriber.Subscribe();
			device.PushDeviceMessage(receivedData);
			Wait();
		}
	}

	public void Service(Responsor responsor)
	{
		if (responsor == null)
		{
			Debug.LogWarning("Responsor is null");
			return;
		}

		var dmResponse = new DeviceMessage();
		while (IsRunning)
		{
			var receivedBuffer = responsor.ReceiveRequest();

			if (receivedBuffer != null)
			{
				var requestMessage = CLOiSimPluginThread.ParseMessageParam(receivedBuffer);

				if (requestMessage != null && dmResponse != null)
				{
					HandleRequestTypeValue(requestMessage.Name, requestMessage.Value, ref dmResponse);
					HandleRequestTypeChildren(requestMessage.Name, requestMessage.Childrens, ref dmResponse);
				}
				else
				{
					Debug.Log("DeviceMessage for response or requestMessage is null");
				}

				responsor.SendResponse(dmResponse);
			}

			Wait();
		}
		dmResponse.Dispose();
	}

	public static messages.Param ParseMessageParam(in byte[] infoBuffer)
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