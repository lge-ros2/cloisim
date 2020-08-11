/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

public class RobotControl : DevicePlugin
{
	private MicomInput micomInput = null;
	private MicomSensor micomSensor = null;


	protected override void OnAwake()
	{
		micomInput = gameObject.AddComponent<MicomInput>();
		micomSensor = gameObject.AddComponent<MicomSensor>();
		micomSensor.SetPluginParameter(parameters);
	}

	protected override void OnStart()
	{
		var debugging = parameters.GetValue<bool>("debug", false);
		micomInput.EnableDebugging = debugging;

		var txHashKey = MakeHashKey("MICOM", "_SENSOR");
		if (!RegisterTxDevice(txHashKey))
		{
			Debug.LogError("Failed to register for RobotControl TX- " + txHashKey);
		}

		var rxHashKey = MakeHashKey("MICOM", "_INPUT");
		if (!RegisterRxDevice(rxHashKey))
		{
			Debug.LogError("Failed to register for RobotControl RX- " + rxHashKey);
		}

		AddThread(Sender);
		AddThread(Receiver);
	}

	void FixedUpdate()
	{
		if (micomInput != null && micomSensor != null)
		{
			switch (micomInput.ControlType)
			{
				case MicomInput.VelocityType.LinearAndAngular:
					var targetLinearVelocity = micomInput.GetLinearVelocity();
					var targetAngularVelocity = micomInput.GetAngularVelocity();
					micomSensor.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
					break;

				case MicomInput.VelocityType.LeftAndRight:
					var targetWheelLeftVelocity = micomInput.GetWheelLeftVelocity();
					var targetWheelRightVelocity = micomInput.GetWheelRightVelocity();
					micomSensor.SetDifferentialDrive(targetWheelLeftVelocity, targetWheelRightVelocity);
					break;

				case MicomInput.VelocityType.Unknown:
					break;
			}
		}
	}

	private void Sender()
	{
		var sw = new Stopwatch();
		while (true)
		{
			if (micomSensor == null)
			{
				continue;
			}

			var datastreamToSend = micomSensor.PopData();
			sw.Restart();
			Publish(datastreamToSend);
			sw.Stop();

			micomSensor.SetTransportTime((float)sw.Elapsed.TotalSeconds);
		}
	}

	private void Receiver()
	{
		while (true)
		{
			if (micomInput == null)
			{
				continue;
			}

			var receivedData = Subscribe();
			micomInput.SetDataStream(receivedData);
			Thread.SpinWait(5);
		}
	}
}