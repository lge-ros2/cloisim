/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class SimulationWorld : CLOiSimPlugin
{
	private Clock clock = null;

	private string hashKey = string.Empty;

	protected override void OnAwake()
	{
		type = Type.WORLD;
		clock = gameObject.AddComponent<Clock>();
		SetDevice(clock);

		modelName = "World";
		partName = "cloisim_world";
	}

	protected override void OnStart()
	{
		RegisterTxDevice("Clock");

		AddThread(SenderThread, clock as System.Object);
	}
}