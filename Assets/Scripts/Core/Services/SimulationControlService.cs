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

	[JsonProperty(Order = 4, PropertyName = "target_model")]
	public string targetModel = string.Empty;

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

public class ModelInfoResult
{
	[JsonProperty("name")]
	public string name = string.Empty;

	[JsonProperty("pose")]
	public ModelInfoPose pose = new();
}

public class ModelInfoPose
{
	[JsonProperty("position")]
	public ModelInfoPosition position = new();

	[JsonProperty("orientation")]
	public ModelInfoOrientation orientation = new();
}

public class ModelInfoPosition
{
	[JsonProperty("x")]
	public double x = 0;

	[JsonProperty("y")]
	public double y = 0;

	[JsonProperty("z")]
	public double z = 0;
}

public class ModelInfoOrientation
{
	[JsonProperty("roll")]
	public double roll = 0;

	[JsonProperty("pitch")]
	public double pitch = 0;

	[JsonProperty("yaw")]
	public double yaw = 0;
}

public class SimulationControlResponseModelInfo : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public ModelInfoResult result = null;

	public override void Print()
	{
		Console.WriteLine($"## {this.GetType().Name}: {command}, {result?.name}");
	}
}

public class TeleportPose
{
	[JsonProperty("x")]
	public double x = 0;

	[JsonProperty("y")]
	public double y = 0;

	[JsonProperty("z")]
	public double z = 0;

	[JsonProperty("roll")]
	public double roll = 0; // roll

	[JsonProperty("pitch")]
	public double pitch = 0; // pitch

	[JsonProperty("yaw")]
	public double yaw = 0; // yaw
}

public class TeleportTarget
{
	[JsonProperty("target")]
	public string target = string.Empty;

	[JsonProperty("pose")]
	public TeleportPose pose = null;

	[JsonProperty("reset")]
	public bool reset = false;
}

public struct TeleportOperation
{
	public List<TeleportTarget> targets;
	public bool worldReset;
}

public class SimulationControlRequestTeleport : SimulationControlRequest
{
	[JsonProperty("world_reset")]
	public bool world_reset = false;

	[JsonProperty("targets")]
	public List<TeleportTarget> targets = new();
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
					var result = wasSuccessful ? SimulationService.SUCCESS : SimulationService.FAIL;

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

			case "teleport":
				{
					var teleportRequest = JsonConvert.DeserializeObject<SimulationControlRequestTeleport>(e.Data);
					var result = TeleportModel(teleportRequest);
					output = new SimulationControlResponseNormal();
					(output as SimulationControlResponseNormal).result = result;
				}
				break;

			case "get_model_info":
				{
					output = GetModelInfo(request.targetModel);
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

	private string TeleportModel(in SimulationControlRequestTeleport request)
	{
		// Validation for targets
		if (request.targets == null || request.targets.Count == 0)
		{
			return "targets list is empty";
		}

		foreach (var target in request.targets)
		{
			if (string.IsNullOrEmpty(target.target))
			{
				return "one or more target model names are empty";
			}

			if (target.pose == null)
			{
				return "one or more pose information is null";
			}
		}

		// Create lightweight operation struct and delegate to Main for async execution
		var operation = new TeleportOperation
		{
			targets = request.targets,
			worldReset = request.world_reset
		};

		Main.Instance.TriggerTeleportService(in operation);

		return SimulationService.SUCCESS;
	}

	private SimulationControlResponseBase GetModelInfo(in string targetModel)
	{
		if (string.IsNullOrEmpty(targetModel))
		{
			var errorResponse = new SimulationControlResponseNormal();
			errorResponse.result = "target_model is empty";
			return errorResponse;
		}

		if (!Main.Instance.TriggerModelInfoQuery(targetModel, out var pose))
		{
			var errorResponse = new SimulationControlResponseNormal();
			errorResponse.result = $"model '{targetModel}' not found";
			return errorResponse;
		}

		var sdfPose = Unity2SDF.Pose(pose.position, pose.rotation);
		var sdfRotation = sdfPose.Rotation.ToEuler();

		var response = new SimulationControlResponseModelInfo();
		response.result = new ModelInfoResult
		{
			name = targetModel,
			pose = new ModelInfoPose
			{
				position = new ModelInfoPosition
				{
					x = sdfPose.Position.X,
					y = sdfPose.Position.Y,
					z = sdfPose.Position.Z
				},
				orientation = new ModelInfoOrientation
				{
					roll = sdfRotation.X,
					pitch = sdfRotation.Y,
					yaw = sdfRotation.Z
				}
			}
		};

		return response;
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Console.Error.WriteLine($"{GetType().Name}::OnError : {e.Message}");
	}
}