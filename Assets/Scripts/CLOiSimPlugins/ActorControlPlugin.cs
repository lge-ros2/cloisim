/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;

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
		foreach (var actorAgent in Main.WorldRoot.GetComponentsInChildren<ActorAgent>())
		{
			if (!actorAgent.RandomWalking)
			{
				actorAgentList.Add(actorAgent.name, actorAgent);
			}
		}

		RegisterServiceDevice("Control");
		AddThread(RequestThread);
	}

	protected override void HandleRequestMessage(in string requestType, in messages.Any requestValue, ref DeviceMessage response)
	{
		// switch (requestType)
		// {
		// 	case "request_ros2":
		// 		var topic_name = GetPluginParameters().GetValue<string>("ros2/topic_name");
		// 		GetPluginParameters().GetValues<string>("ros2/frame_id", out var frameIdList);
		// 		SetROS2CommonInfoResponse(ref response, topic_name, frameIdList);
		// 		break;

		// 	default:
		// 		var value = requestValue.StringValue;
		// 		HandleCustomRequestMessage(requestType, value, ref response);
		// 		break;
		// }
	}
}