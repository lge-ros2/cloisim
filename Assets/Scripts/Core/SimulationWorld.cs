/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;

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
		if (RegisterTxDevice(out var portTx, "Clock"))
		{
			AddThread(portTx, SenderThread, clock);
		}
	}

	public Clock GetClock()
	{
		return clock;
	}
}