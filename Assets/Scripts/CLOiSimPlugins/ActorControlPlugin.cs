/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class ActorControlPlugin : CLOiSimPlugin
{
	public static Dictionary<string, ActorAgent> actorAgentList = new Dictionary<string, ActorAgent>();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partName = "ActorControlPlugin";
	}

	protected override void OnStart()
	{
		// update actorAgentlist

		RegisterServiceDevice("Control");
		AddThread(ControlThread);
	}

	private void ControlThread()
	{
		var dmResponse = new DeviceMessage();
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();
			var requestMessage = ParsingRequestMessage(receivedBuffer);

			if (requestMessage != null)
			{
				var targetName = requestMessage.Name;

				if (requestMessage.Value != null)
				{
					var requesteValue = requestMessage.Value.Vector3dValue;
					// HandleRequestMessage(requestMessage.Name, requesteValue, ref dmResponse);
				}
				SendResponse(dmResponse);
			}

			WaitThread();
		}
	}
}