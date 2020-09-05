/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class UnityRosWorld : DevicePlugin
{
	private Clock clock = null;

	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		modelName = "World";

		clock = gameObject.AddComponent<Clock>();

		hashKey = MakeHashKey();
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for UnityRosWorld - " + hashKey);
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