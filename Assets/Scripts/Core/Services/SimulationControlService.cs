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
		Console.WriteLine($"## {GetType().Name}: {command} {indent} {filter} {filename}");
	}
}

public class SimulationControlResponseBase
{
	[JsonProperty(Order = 0)]
	public string command = string.Empty;

	[JsonProperty(Order = -1, PropertyName = "sim_version")]
	public string simVersion = SimulationControlService.SimVersion;

	public virtual void Print()
	{
		Console.WriteLine($"## {GetType().Name}: {command}");
	}
}

public class SimulationControlResponseNormal : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public string result = string.Empty;

	public override void Print()
	{
		Console.WriteLine($"## {GetType().Name}: {command}, {result}");
	}
}

public class SimulationControlResponseDeviceList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> result;

	public override void Print()
	{
		Console.WriteLine($"## {GetType().Name}: {command}, {result}");
	}
}

public class SimulationControlResponseTopicList : SimulationControlResponseBase
{
	[JsonProperty(Order = 1)]
	public Dictionary<string, ushort> result;

	public override void Print()
	{
		Console.WriteLine($"## {GetType().Name}: {command}, {result}");
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
		Console.WriteLine($"## {GetType().Name}: {command}, {result?.name}");
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
	public static string SimVersion { get; set; } = string.Empty;
	private const int MaxFilterLength = 256;
	private const int MaxFilenameLength = 512;

	private BridgeManager bridgeManager = null;

	private static SimulationControlResponseNormal CreateNormalResponse(in string result)
	{
		return new SimulationControlResponseNormal
		{
			result = result
		};
	}

	private static bool TryValidateFilter(in string filter, out SimulationControlResponseNormal errorResponse)
	{
		if (!string.IsNullOrEmpty(filter) && filter.Length > MaxFilterLength)
		{
			errorResponse = CreateNormalResponse($"filter is too long (max {MaxFilterLength} characters)");
			return false;
		}

		errorResponse = null;
		return true;
	}

	private void SendResponse(SimulationControlRequest request, SimulationControlResponseBase output)
	{
		output.command = request?.command ?? string.Empty;

		var responseJsonData = JsonConvert.SerializeObject(output, request != null && request.indent ? Formatting.Indented : Formatting.None);

		try
		{
			if (State == WebSocketState.Open)
			{
				Send(responseJsonData);
			}
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Send failed: {ex.GetType().Name} {ex.Message}");
		}
	}

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
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Invalid JSON: {ex.Message}\nraw={e.Data}");
			SendResponse(null, CreateNormalResponse($"invalid json: {ex.Message}"));
			return;
		}

		if (request == null)
		{
			SendResponse(null, CreateNormalResponse("request body is empty"));
			return;
		}

		if (string.IsNullOrWhiteSpace(request.command))
		{
			SendResponse(request, CreateNormalResponse("command is empty"));
			return;
		}

		if (!request.command.Equals("device_list"))
			request.Print();

		SimulationControlResponseBase output = null;

		switch (request.command)
		{
			case "fps":
				{
						if (Main.InfoDisplay == null)
						{
							output = CreateNormalResponse("fps service is unavailable");
							break;
						}

					var fps = Main.InfoDisplay.FPS();
						output = CreateNormalResponse(fps.ToString());
				}
				break;

			case "reset":
				{
					var wasSuccessful = Main.Instance.TriggerResetService();
					var result = wasSuccessful ? SimulationService.SUCCESS : SimulationService.FAIL;

						output = CreateNormalResponse(result);
				}
				break;

			case "device_list":
				{
						if (!TryValidateFilter(request.filter, out var filterError))
						{
							output = filterError;
							break;
						}

						if (bridgeManager == null)
						{
							output = CreateNormalResponse("bridge manager is unavailable");
							break;
						}

					var result = bridgeManager.GetDeviceMapList(request.filter);
					output = new SimulationControlResponseDeviceList();
					(output as SimulationControlResponseDeviceList).result = result;
				}
				break;

			case "port_list":
				{
						if (!TryValidateFilter(request.filter, out var filterError))
						{
							output = filterError;
							break;
						}

						if (bridgeManager == null)
						{
							output = CreateNormalResponse("bridge manager is unavailable");
							break;
						}

					var result = bridgeManager.GetDevicePortList(request.filter);
					output = new SimulationControlResponseTopicList();
					(output as SimulationControlResponseTopicList).result = result;
				}
				break;

			case "start_record":
				{
						var result = "filename is empty!!";
						if (!string.IsNullOrEmpty(request.filename) && request.filename.Length <= MaxFilenameLength)
					{
						Main.Instance.TriggerStartRecordService(request.filename);
						result = true.ToString();
					}
						else if (!string.IsNullOrEmpty(request.filename))
						{
							result = $"filename is too long (max {MaxFilenameLength} characters)";
						}

						output = CreateNormalResponse(result);
				}
				break;

			case "stop_record":
				{
					Main.Instance.TriggerStopRecordService();
						output = CreateNormalResponse(true.ToString());
				}
				break;

			case "teleport":
				{
						try
						{
							var teleportRequest = JsonConvert.DeserializeObject<SimulationControlRequestTeleport>(e.Data);
							var result = TeleportModel(teleportRequest);
							output = CreateNormalResponse(result);
						}
						catch (Exception ex)
						{
							output = CreateNormalResponse($"invalid teleport request: {ex.Message}");
						}
				}
				break;

			case "get_model_info":
				{
					output = GetModelInfo(request.targetModel);
				}
				break;

			default:
					output = CreateNormalResponse($"unsupported command '{request.command}'");
				break;
		}

		SendResponse(request, output);
	}

	private string TeleportModel(in SimulationControlRequestTeleport request)
	{
		if (request == null)
		{
			return "teleport request is empty";
		}

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
			var errorResponse = new SimulationControlResponseNormal
			{
				result = "target_model is empty"
			};
			return errorResponse;
		}

		if (!Main.Instance.TriggerModelInfoQuery(targetModel, out var pose))
		{
			var errorResponse = new SimulationControlResponseNormal
			{
				result = $"model '{targetModel}' not found"
			};
			return errorResponse;
		}

		var sdfPose = Unity2SDF.Pose(pose.position, pose.rotation);
		var sdfRotation = sdfPose.Rotation.ToEuler();

		var response = new SimulationControlResponseModelInfo
		{
			result = new ModelInfoResult
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
			}
		};

		return response;
	}

	protected override void OnError(ErrorEventArgs e)
	{
		Console.Error.WriteLine($"{GetType().Name}::OnError : {e.Message}");
	}
}