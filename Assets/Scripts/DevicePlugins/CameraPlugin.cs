/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CameraPlugin : DevicePlugin
{
	private SensorDevices.Camera cam = null;

	public string subPartName = string.Empty;

	protected override void OnAwake()
	{
		cam = gameObject.GetComponent<SensorDevices.Camera>();
		partName = DeviceHelper.GetPartName(gameObject);
	}

	protected override void OnStart()
	{
		var hashKey = MakeHashKey(partName + subPartName);
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