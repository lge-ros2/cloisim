/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class Clock : Device
{
	private const float updateRate = 100f;

	private messages.Param timeInfo = null;
	private messages.Time simTime = null;
	private messages.Time realTime = null;

	private float restartedSimTime = 0;
	private float restartedRealTime = 0;

	protected override void OnAwake()
	{
		deviceName = "World Clock";
	}

	protected override void OnStart()
	{
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
		timeInfo.Value = new Any { Type = Any.ValueType.None };

		var simTimeParam = new messages.Param();
		simTimeParam.Name = "simTime";
		simTimeParam.Value = new Any { Type = Any.ValueType.Time, TimeValue = simTime };
		timeInfo.Childrens.Add(simTimeParam);

		var realTimeParam = new messages.Param();
		realTimeParam.Name = "realTime";
		realTimeParam.Value = new Any { Type = Any.ValueType.Time, TimeValue = realTime };
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

	public void ResetTime()
	{
		restartedSimTime = Time.time;
		restartedRealTime = Time.realtimeSinceStartup;
	}

	public float GetSimTime()
	{
		return Time.time - restartedSimTime;
	}

	public float GetRealTime()
	{
		return Time.realtimeSinceStartup - restartedRealTime;
	}
}