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
	private Clock clock = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.WORLD;

		clock = gameObject.GetComponent<Clock>();
		attachedDevices.Add("Clock", clock);

		modelName = "World";
		partsName = "cloisim";
	}

	protected override void OnStart()
	{
		RegisterTxDevice("Clock");

		AddThread(SenderThread, clock);
	}

	public Clock GetClock()
	{
		return clock;
	}
}