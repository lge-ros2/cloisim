
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private CLOiSimPluginThread _thread = new CLOiSimPluginThread();
	protected CLOiSimPluginThread PluginThread => _thread;

	protected bool AddThread(in ushort targetPortForThread, in ParameterizedThreadStart function, in System.Object paramObject = null)
	{
		return _thread.Add(targetPortForThread, function, paramObject);
	}

	protected void SenderThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = GetTransport().Get<Publisher>(paramObject.targetPort);
		var deviceParam = paramObject.param as Device;

		_thread.Sender(publisher, deviceParam);
	}

	protected void ReceiverThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var subscriber = GetTransport().Get<Subscriber>(paramObject.targetPort);
		var deviceParam = paramObject.param as Device;

		_thread.Receiver(subscriber, deviceParam);
	}

	protected void ServiceThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var responsor = GetTransport().Get<Responsor>(paramObject.targetPort);

		_thread.Service(responsor);
	}
}