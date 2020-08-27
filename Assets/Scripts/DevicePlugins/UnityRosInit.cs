/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class UnityRosInit : DevicePlugin
{
	private Clock clock = null;

	protected override void OnAwake()
	{
		clock = gameObject.AddComponent<Clock>();

		var hashKey = MakeHashKey();
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for UnityRosInit - " + hashKey);
		}
	}

	protected override void OnStart()
	{
		AddThread(Sender);
	}

	private void Sender()
	{
		while (IsRunningThread)
		{
			if (clock == null)
			{
				continue;
			}

			var datastreamToSend = clock.PopData();
			Publish(datastreamToSend);
		}
	}
}