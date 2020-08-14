/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using UnityEngine;
using Newtonsoft.Json;

public class SimulationControlRequest
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

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


public class SimulationControlResponseSensorPortList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public List<Dictionary<string, ushort>> result;

	public override void Print()
	{
		Debug.LogFormat("## {0}: {1}, {2}", this.GetType().Name, command, result);
	}
}

public class SimulationControlService : WebSocketBehavior
{
	public ModelLoader modelLoaderService = null;
	public BridgePortManager portDeviceService = null;

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

		var request = JsonConvert.DeserializeObject<SimulationControlRequest>(e.Data);

		if (request == null)
		{
			Debug.Log("Invalid JSON format");
			return;
		}

		request.Print();

		SimulationControlResponseBase output = null;

		switch (request.command)
		{
			case "reset":
				{
					var wasSuccessful = modelLoaderService.TriggerResetService(request.command);
					var result = (wasSuccessful) ? SimulationService.SUCCESS : SimulationService.FAIL;

					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = result;
				}
				break;

			case "device_list":
				{
					var result = portDeviceService.GetSensorPortList();

					output = new SimulationControlResponseSensorPortList();
					(output as SimulationControlResponseSensorPortList).result = result;
				}
				break;

			default:
				output = new SimulationControlResponseBase();
				request.command = "Invalid Command";
				break;
		}

		output.command = request.command;

		var responseJsonData = JsonConvert.SerializeObject(output, Formatting.Indented);

		Send(responseJsonData);
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Debug.LogFormat("{0}::OnError : {1}", GetType().Name, e.Message);
	}
}