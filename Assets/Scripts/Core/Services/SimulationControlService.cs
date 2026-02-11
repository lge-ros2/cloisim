/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;

public class SimulationControlRequest
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

	[JsonProperty(Order = 1)]
	public bool indent = false;

	[JsonProperty(Order = 2)]
	public string filter = string.Empty;

	[JsonProperty(Order = 3)]
	public string filename = string.Empty;

	public void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command} {indent} {filter} {filename}");
	}
}

public class SimulationControlResponseBase
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

	public virtual void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command}");
	}
}

public class SimulationControlResponseNormal : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public string result = string.Empty;

	public override void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command}, {result}");
	}
}

public class SimulationControlResponseDeviceList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> result;

	public override void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command}, {result}");
	}
}

public class SimulationControlResponseTopicList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, ushort> result;

	public override void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command}, {result}");
	}
}

public class SimulationControlService : WebSocketBehavior
{
	private BridgeManager bridgeManager = null;

	protected override void OnOpen()
	{
		bridgeManager = Main.BridgeManager;
		Log.Level = LogLevel.Fatal;
		Console.WriteLine("Open SimulationControlService");
	}

	protected override void OnClose(CloseEventArgs e)
	{
		Sessions.Sweep();
		Console.WriteLine($"Close SimulationControlService({e.Code}), {e.Reason}, {e.WasClean}");
	}

	protected override void OnMessage(MessageEventArgs e)
	{
		if (e.RawData.Length == 0 || e.IsPing)
		{
			// Debug.LogFormat("length:{0}, {1}", e.RawData.Length, e.IsPing);
			return;
		}

		SimulationControlRequest request = null;
		try
		{
			request = JsonConvert.DeserializeObject<SimulationControlRequest>(e.Data);
		}
		catch (System.Exception ex)
		{
			Console.Error.WriteLine($"Invalid JSON: {ex.Message}\nraw={e.Data}");
			return;
		}

		if (!request.command.Equals("device_list"))
			request.Print();

		SimulationControlResponseBase output = null;

		switch (request.command)
		{
			case "fps":
				{
					var fps = Main.InfoDisplay.FPS();
					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = fps.ToString();
				}
				break;

			case "reset":
				{
					var wasSuccessful = Main.Instance.TriggerResetService();
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

			case "port_list":
				{
					var result = bridgeManager.GetDevicePortList(request.filter);
					output = new SimulationControlResponseTopicList();
					(output as SimulationControlResponseTopicList).result = result;
				}
				break;

			case "start_record":
				{
					var result = "filename is empty!!";
					if (!string.IsNullOrEmpty(request.filename))
					{
						Main.Instance.TriggerStartRecordService(request.filename);
						result = true.ToString();
					}
					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = result;
				}
				break;

			case "stop_record":
				{
					Main.Instance.TriggerStopRecordService();
					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = true.ToString();
				}
				break;
			default:
				output = new SimulationControlResponseBase();
				request.command = "Invalid Command";
				break;
		}

		output.command = request.command;

		var responseJsonData = JsonConvert.SerializeObject(output, (request.indent) ? Formatting.Indented : Formatting.None);

		try
		{
			if (State == WebSocketState.Open)
				Send(responseJsonData);
		}
		catch (System.Exception ex)
		{
			Console.Error.WriteLine($"Send failed: {ex.GetType().Name} {ex.Message}");
		}
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Console.Error.WriteLine($"{GetType().Name}::OnError : {e.Message}");
	}
}