/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
using Newtonsoft.Json;

public class SimulationControlRequest
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

	[JsonProperty(Order = 1)]
	public bool indent = false;

	[JsonProperty(Order = 2)]
	public string filter = string.Empty;

	public void Print()
	{
		Debug.LogFormat("## {0}: {1}", this.GetType().Name, command);
	}
}

public class SimulationControlResponseBase
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

	public virtual void Print()
	{
		Debug.LogFormat("## {0}: {1}", this.GetType().Name, command);
	}
}

public class SimulationControlResponseNormal : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public string result = string.Empty;

	public override void Print()
	{
		Debug.LogFormat("## {0}: {1}, {2}", this.GetType().Name, command, result);
	}
}

public class SimulationControlResponseDeviceList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> result;

	public override void Print()
	{
		Debug.LogFormat("## {0}: {1}, {2}", this.GetType().Name, command, result);
	}
}

public class SimulationControlResponseTopicList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, ushort> result;

	public override void Print()
	{
		Debug.LogFormat("## {0}: {1}, {2}", this.GetType().Name, command, result);
	}
}

public class SimulationControlService : WebSocketBehavior
{
	private BridgeManager bridgeManager = null;

	protected override void OnOpen()
	{
		bridgeManager = Main.BridgeManager;
		Log.Level = LogLevel.Fatal;
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

		var request = JsonConvert.DeserializeObject<SimulationControlRequest>(e.Data);

		if (request == null)
		{
			Debug.Log("Invalid JSON format");
			return;
		}

		if (!request.command.Equals("device_list"))
			request.Print();

		SimulationControlResponseBase output = null;

		switch (request.command)
		{
			case "reset":
				{
					var wasSuccessful = Main.TriggerResetService();
					var result = (wasSuccessful) ? SimulationService.SUCCESS : SimulationService.FAIL;

					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = result;
				}
				break;

			case "device_list":
				{
					var result = bridgeManager.GetDeviceMapList(request.filter);
					output = new SimulationControlResponseDeviceList();
					(output as SimulationControlResponseDeviceList).result = result;
				}
				break;

			case "topic_list":
				{
					var result = bridgeManager.GetDevicePortList(request.filter);

					output = new SimulationControlResponseTopicList();
					(output as SimulationControlResponseTopicList).result = result;
				}
				break;

			default:
				output = new SimulationControlResponseBase();
				request.command = "Invalid Command";
				break;
		}

		output.command = request.command;

		var responseJsonData = JsonConvert.SerializeObject(output, (request.indent) ? Formatting.Indented : Formatting.None);
		Send(responseJsonData);
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Debug.LogFormat("{0}::OnError : {1}", GetType().Name, e.Message);
	}
}