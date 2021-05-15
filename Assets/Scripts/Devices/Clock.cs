/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

public class Clock : Device
{
	private messages.Param timeInfo = null;
	private messages.Time simTime = null;
	private messages.Time realTime = null;

	private double restartedSimTime = 0;
	private double restartedRealTime = 0;

	private double currentSimTime = 0;
	private double currentRealTime = 0;

	public double SimTime => currentSimTime;

	public double RealTime => currentRealTime;

	protected override void OnAwake()
	{
		Mode = ModeType.NONE;
		DeviceName = "World Clock";
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

	private void FixedUpdate()
	{
		currentSimTime = Time.timeAsDouble - restartedSimTime;
		currentRealTime = Time.realtimeSinceStartupAsDouble - restartedRealTime;
		// Debug.Log(currentRealTime.ToString("F6") + ", " + currentSimTime.ToString("F6"));
		if (timeInfo != null)
		{
			DeviceHelper.SetCurrentTime(simTime, false);
			DeviceHelper.SetCurrentTime(realTime, true);
			PushDeviceMessage<messages.Param>(timeInfo);
		}
	}

	public void ResetTime()
	{
		restartedSimTime = Time.timeAsDouble;
		restartedRealTime = Time.realtimeSinceStartupAsDouble;
	}
}