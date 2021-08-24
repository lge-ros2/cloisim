/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

[DefaultExecutionOrder(605)]
public class ActorControlPlugin : CLOiSimPlugin
{
	public static Dictionary<string, SDF.Helper.Actor> actorList = new Dictionary<string, SDF.Helper.Actor>();

	private bool isReceivedRequest = false;
	private SDF.Helper.Actor targetActor = null;
	private Vector3 targetDestination;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partsName = "ActorControlPlugin";

		UpdateActorList();
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var port, "Control"))
		{
			AddThread(port, ServiceThread);
		}
	}

	void LateUpdate()
	{
		if (isReceivedRequest && targetActor != null)
		{
			var targetActorAgent = targetActor.GetComponent<ActorAgent>();
			if (targetActorAgent && !targetActorAgent.RandomWalking)
			{
				targetActorAgent.AssignTargetDestination(targetDestination);
			}
			isReceivedRequest = false;
			targetActorAgent = null;
		}
	}

	private void UpdateActorList()
	{
		actorList.Clear();
		foreach (var actor in Main.WorldRoot.GetComponentsInChildren<SDF.Helper.Actor>())
		{
			actorList.Add(actor.name, actor);
			// Debug.Log(actor.name);
		}
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		var moveResponse = new messages.Param();
		moveResponse.Name = "result";

		var result = false;
		if (actorList.ContainsKey(requestType) && requestValue != null)
		{
			targetActor = actorList[requestType];
			if (targetActor)
			{
				var vec = requestValue.Vector3dValue;
				targetDestination = SDF2Unity.GetPosition(vec.X, vec.Y, vec.Z);
				isReceivedRequest = true;
				result = true;
			}
		}

		moveResponse.Value = new Any { Type = Any.ValueType.Boolean, BoolValue = result };
		response.SetMessage<messages.Param>(moveResponse);
	}
}