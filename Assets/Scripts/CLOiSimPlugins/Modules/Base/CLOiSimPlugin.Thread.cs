
/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private CLOiSimPluginThread thread = new CLOiSimPluginThread();
	protected CLOiSimPluginThread PluginThread => thread;

	protected bool AddThread(in ushort targetPortForThread, in ParameterizedThreadStart function, in System.Object paramObject = null)
	{
		return thread.Add(targetPortForThread, function, paramObject);
	}

	protected void SenderThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var publisher = this.transport.Get<Publisher>(paramObject.targetPort);
		var deviceParam = paramObject.paramObject as Device;

		thread.Sender(publisher, deviceParam);
	}

	protected void ReceiverThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var subscriber = this.transport.Get<Subscriber>(paramObject.targetPort);
		var deviceParam = paramObject.paramObject as Device;

		thread.Receiver(subscriber, deviceParam);
	}

	protected void ServiceThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var responsor = this.transport.Get<Responsor>(paramObject.targetPort);

		thread.Service(responsor);
	}
}