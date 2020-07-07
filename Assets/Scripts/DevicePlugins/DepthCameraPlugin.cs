/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class DepthCameraPlugin : DevicePlugin
{
	public string partName = string.Empty;

	private SensorDevices.DepthCamera depthCam = null;

	protected override void OnAwake()
	{
		partName = DeviceHelper.GetPartName(gameObject);

		string hashKey = MakeHashKey(partName);
		if (!RegisterTxDevice(hashKey))
		{
			Debug.LogError("Failed to register for DepthCameraPlugin - " + hashKey);
		}
	}

	protected override void OnStart()
	{
		depthCam = gameObject.GetComponent<SensorDevices.DepthCamera>();

		AddThread(Sender);
	}

	private void Sender()
	{
		Stopwatch sw = new Stopwatch();
		while (true)
		{
			if (depthCam == null)
			{
				continue;
			}

			var datastreamToSend = depthCam.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();
			depthCam.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}
}