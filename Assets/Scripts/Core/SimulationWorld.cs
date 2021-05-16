/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class SimulationWorld : CLOiSimPlugin
{
	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.WORLD;
		targetDevice = gameObject.AddComponent<Clock>();

		modelName = "World";
		partName = "cloisim";
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