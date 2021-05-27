/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class Micom : Device
{
	private MicomInput micomInput = null;
	private MicomSensor micomSensor = null;

	public bool debugging = false;

	protected override void OnAwake()
	{
		Mode = ModeType.NONE;
		DeviceName = "Micom";

		micomSensor = gameObject.AddComponent<MicomSensor>();
		micomInput = gameObject.AddComponent<MicomInput>();
		micomInput.SetMicomSensor(micomSensor);
	}

	protected override void OnStart()
	{
		micomInput.SetPluginParameters(GetPluginParameters());
		micomSensor.SetPluginParameters(GetPluginParameters());
		micomInput.EnableDebugging = EnableDebugging;
		micomSensor.EnableDebugging = EnableDebugging;
	}

	protected override void OnReset()
	{
		micomSensor.Reset();
		micomInput.Reset();
	}

	public MicomInput GetInput()
	{
		return micomInput;
	}

	public MicomSensor GetSensor()
	{
		return micomSensor;
	}
}