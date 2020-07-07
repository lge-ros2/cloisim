/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class CameraPlugin : DevicePlugin
{
	public string partName = string.Empty;

	private SensorDevices.Camera cam = null;

	protected override void OnAwake()
	{
		partName = DeviceHelper.GetPartName(gameObject);

		string hashKey = MakeHashKey(partName);
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for CameraPlugin - " + hashKey);
		}
	}

	protected override void OnStart()
	{
		cam = gameObject.GetComponent<SensorDevices.Camera>();

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