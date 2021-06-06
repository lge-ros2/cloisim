
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

	protected bool AddThread(in ThreadStart function)
	{
		return thread.Add(function);
	}

	protected bool AddThread(in ParameterizedThreadStart function, in Device paramDeviceObject)
	{
		return thread.Add(function, paramDeviceObject);
	}

	protected void SenderThread(System.Object deviceParam)
	{
		thread.Sender(deviceParam);
	}

	protected void ReceiverThread(System.Object deviceParam)
	{
		thread.Receiver(deviceParam);
	}

	protected void ServiceThread()
	{
		thread.Service();
	}
}