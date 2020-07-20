/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class MultiCameraPlugin : DevicePlugin
{
	private SensorDevices.MultiCamera cam = null;

	protected override void OnAwake()
	{
		cam = gameObject.GetComponent<SensorDevices.MultiCamera>();
	}

	protected override void OnStart()
	{
		partName = DeviceHelper.GetPartName(gameObject);

		var hashKey = MakeHashKey(partName);
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashKey);
		}

		AddThread(Sender);
	}

	private void Sender()
	{
		Stopwatch sw = new Stopwatch();
		while (true)
		{
			if (cam == null)
			{
				continue;
			}

			var datastreamToSend = cam.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			cam.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}
}