/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class Micom : Device
{
	private MicomSensor micomSensor = null;
	private MicomInput micomInput = null;

	public bool debugging = false;

	protected override void OnAwake()
	{
		Mode = ModeType.NONE;
		DeviceName = "Micom";
	}

	protected override void OnStart()
	{
	}

	protected override void OnReset()
	{
		micomSensor.Reset();
		micomInput.Reset();
	}

	public MicomInput GetInput()
	{
		if (micomInput == null)
		{
			micomInput = gameObject.AddComponent<MicomInput>();
			micomInput.SetMicomSensor(GetSensor());
			micomInput.EnableDebugging = EnableDebugging;
		}

		return micomInput;
	}

	public MicomSensor GetSensor()
	{
		if (micomSensor == null)
		{
			micomSensor = gameObject.AddComponent<MicomSensor>();
			micomSensor.SetPluginParameters(GetPluginParameters());
			micomSensor.EnableDebugging = EnableDebugging;
		}

		return micomSensor;
	}
}