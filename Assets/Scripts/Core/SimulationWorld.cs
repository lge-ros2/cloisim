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
		_modelName = "World";
		_partsName = this.GetType().Name;

		clock = gameObject.GetComponent<Clock>();
		attachedDevices.Add("Clock", clock);
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