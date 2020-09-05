/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class UnityRosInit : DevicePlugin
{
	private Clock clock = null;

	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		clock = gameObject.AddComponent<Clock>();

		hashKey = MakeHashKey();
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for UnityRosInit - " + hashKey);
		}
	}

	protected override void OnStart()
	{
		AddThread(Sender);
	}

	protected override void OnTerminate()
	{
		DeregisterDevice(hashKey);
	}

	private void Sender()
	{
		while (IsRunningThread)
		{
			if (clock != null)
			{
				var datastreamToSend = clock.PopData();
				Publish(datastreamToSend);
			}
		}
	}
}