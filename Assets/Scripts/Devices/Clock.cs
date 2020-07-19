/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

public class Clock : Device
{
	private const float updateRate = 250f;

	private messages.Param timeInfo = null;
	private messages.Time simTime = null;
	private messages.Time realTime = null;

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
		simTime = new messages.Time();
		realTime = new messages.Time();

		timeInfo = new messages.Param();
		timeInfo.Name = "timeInfo";
		timeInfo.Value = new messages.Any();
		timeInfo.Value.Type = messages.Any.ValueType.None;

		var simTimeParam = new messages.Param();
		simTimeParam.Name = "simTime";
		simTimeParam.Value = new messages.Any();
		simTimeParam.Value.Type = messages.Any.ValueType.Time;
		simTimeParam.Value.TimeValue = simTime;
		timeInfo.Childrens.Add(simTimeParam);

		var realTimeParam = new messages.Param();
		realTimeParam.Name = "realTime";
		realTimeParam.Value = new messages.Any();
		realTimeParam.Value.Type = messages.Any.ValueType.Time;
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
			PushData<messages.Param>(timeInfo);
		}
	}
}