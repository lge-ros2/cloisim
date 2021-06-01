/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
 using UnityEngine;

[DefaultExecutionOrder(600)]
[RequireComponent(typeof(Clock))]
public class SimulationWorld : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.WORLD;
		targetDevice = gameObject.GetComponent<Clock>();

		modelName = "World";
		partsName = "cloisim";
	}

	protected override void OnStart()
	{
		RegisterTxDevice("Clock");

		AddThread(SenderThread, targetDevice);
	}

	public Clock GetClock()
	{
		return targetDevice as Clock;
	}
}