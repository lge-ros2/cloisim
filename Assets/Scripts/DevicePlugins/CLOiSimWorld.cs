/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class CLOiSimWorld : DevicePlugin
{
	private Clock clock = null;

	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = Type.WORLD;
		clock = gameObject.AddComponent<Clock>();

		modelName = "World";
		partName = "cloisim_world";
	}

	protected override void OnStart()
	{
		RegisterTxDevice("Clock");

		AddThread(Sender);
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