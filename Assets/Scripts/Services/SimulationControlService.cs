/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;

[Serializable]
public class SimulationControlRequest
{
	[SerializeField] public string command = string.Empty;

	public void Print()
	{
		Debug.LogFormat("## {0}: {1}", this.GetType().Name, command);
	}
}

[Serializable]
public class SimulationControlResponse
{
	[SerializeField] public string command = string.Empty;
	[SerializeField] public string result = string.Empty;

	public void Print()
	{
		Debug.LogFormat("## {0}: {1}, {2}", this.GetType().Name, command, result);
	}
}

public class SimulationControlService : WebSocketBehavior
{
	private ModelLoader targetComponent = null;

	public SimulationControlService(in ModelLoader target)
	{
		targetComponent = target;
	}

	protected override void OnOpen()
	{
		Debug.Log("Open SimulationControlService");
	}

	protected override void OnClose(CloseEventArgs e)
	{
		Sessions.Sweep();
		Debug.LogFormat("Close SimulationControlService({0}), {1}", e.Code, e.Reason);
	}

	protected override void OnMessage(MessageEventArgs e)
	{
		if (e.RawData.Length == 0 || e.IsPing)
		{
			// Debug.LogFormat("length:{0}, {1}", e.RawData.Length, e.IsPing);
			return;
		}

		var request = JsonUtility.FromJson<SimulationControlRequest>(e.Data);
		request.Print();

		var result = targetComponent.TriggerResetService(request.command);

		GenerateResponse(request, result, out var response);

		var responseJsonData = JsonUtility.ToJson(response, true);
		Send(responseJsonData);
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Debug.LogFormat("{0}::OnError : {1}", GetType().Name, e.Message);
	}

	private void GenerateResponse(in SimulationControlRequest requeset, in string result, out SimulationControlResponse output)
	{
		output = new SimulationControlResponse();
		output.command = requeset.command;
		output.result = result;
	}
}