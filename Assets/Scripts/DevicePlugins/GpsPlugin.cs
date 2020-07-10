/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class GpsPlugin : DevicePlugin
{
	private SensorDevices.GPS gps = null;

	protected override void OnAwake()
	{
		partName = DeviceHelper.GetPartName(gameObject);

		string hashKey = MakeHashKey(partName);
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for GpsPlugin - " + hashKey);
		}
	}

	protected override void OnStart()
	{
		gps = gameObject.GetComponent<SensorDevices.GPS>();

		AddThread(Sender);
	}

	private void Sender()
	{
		Stopwatch sw = new Stopwatch();
		while (true)
		{
			if (gps == null)
			{
				continue;
			}

			var datastreamToSend = gps.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			gps.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}
}