/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class Clock : Device
{
	private const float updateRate = 250f;

	private gazebo.msgs.Param timeInfo = null;
	private gazebo.msgs.Time simTime = null;
	private gazebo.msgs.Time realTime = null;

	protected override void OnAwake()
	{
	}

	protected override void OnStart()
	{
		deviceName = "Unity Clock";
		SetUpdateRate(updateRate);
	}

	protected override IEnumerator OnVisualize()
	{
		yield return null;
	}

	protected override void InitializeMessages()
	{
		simTime = new gazebo.msgs.Time();
		realTime = new gazebo.msgs.Time();

		timeInfo = new gazebo.msgs.Param();
		timeInfo.Name = "timeInfo";
		timeInfo.Value = new gazebo.msgs.Any();
		timeInfo.Value.Type = gazebo.msgs.Any.ValueType.None;

		var simTimeParam = new gazebo.msgs.Param();
		simTimeParam.Name = "simTime";
		simTimeParam.Value = new gazebo.msgs.Any();
		simTimeParam.Value.Type = gazebo.msgs.Any.ValueType.Time;
		simTimeParam.Value.TimeValue = simTime;
		timeInfo.Childrens.Add(simTimeParam);

		var realTimeParam = new gazebo.msgs.Param();
		realTimeParam.Name = "realTime";
		realTimeParam.Value = new gazebo.msgs.Any();
		realTimeParam.Value.Type = gazebo.msgs.Any.ValueType.Time;
		realTimeParam.Value.TimeValue = realTime;
		timeInfo.Childrens.Add(realTimeParam);
	}

	protected override IEnumerator MainDeviceWorker()
	{
		var waitForSeconds = new WaitForSeconds(UpdatePeriod);
		while (true)
		{
			GenerateMessage();
			yield return waitForSeconds;
		}
	}

	protected override void GenerateMessage()
	{
		if (timeInfo != null)
		{
			DeviceHelper.SetCurrentTime(simTime, false);
			DeviceHelper.SetCurrentTime(realTime, true);
			PushData<gazebo.msgs.Param>(timeInfo);
		}
	}
}