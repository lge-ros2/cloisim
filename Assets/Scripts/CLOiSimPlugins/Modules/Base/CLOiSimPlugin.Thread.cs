/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Threading;
using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private CLOiSimPluginThread _thread = new();
	protected CLOiSimPluginThread PluginThread => _thread;

	protected bool AddThread(in ushort targetPortForThread, in ParameterizedThreadStart function, in object pluginObject = null)
	{
		return _thread.Add(targetPortForThread, function, pluginObject);
	}

	protected void SenderThread(object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramObject.targetPort);
		var deviceParam = paramObject.param as Device;

		_thread.Sender(publisher, deviceParam);
	}

	protected void ReceiverThread(object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var subscriber = GetTransport().Get<Subscriber>(paramObject.targetPort);
		var deviceParam = paramObject.param as Device;

		_thread.Receiver(subscriber, deviceParam);
	}

	protected void ServiceThread(object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var responser = GetTransport().Get<Responsor>(paramObject.targetPort);

		_thread.Service(responser);
	}
}