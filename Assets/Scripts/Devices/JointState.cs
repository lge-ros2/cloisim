/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

public class JointState : Device
{
	private messages.JointStateV jointStates = null;

	protected override void OnAwake()
	{
		Mode = ModeType.TX_THREAD;
		DeviceName = "JointsState";
	}

	protected override void OnStart()
	{
	}

	protected override void OnReset()
	{
	}

	protected override void InitializeMessages()
	{
		jointStates = new messages.JointStateV();
		jointStates.Header = new messages.Header();
		jointStates.Header.Stamp = new messages.Time();
	}

	protected override void GenerateMessage()
	{
		DeviceHelper.SetCurrentTime(jointStates.Header.Stamp);



		PushDeviceMessage<messages.JointStateV>(jointStates);
	}
}