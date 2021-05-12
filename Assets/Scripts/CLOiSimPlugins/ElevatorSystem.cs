/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.IO;
#if UNITY_EDITOR
using System.Linq;
#endif
using UnityEngine;
using ProtoBuf;
using messages = cloisim.msgs;

public partial class ElevatorSystem : CLOiSimPlugin
{
	private enum ElevatorTaskState {DOOR_OPEN, DOOR_CLOSE, STANDBY, PROCESSING, DONE}

	private struct ElevatorTask
	{
		public struct Floor
		{
			public string name;
			public float height;
		}

		public ElevatorTaskState state;
		public string elevatorIndex;
		public Floor from;
		public Floor to;

		public ElevatorTask(in string index = "")
		{
			state = ElevatorTaskState.STANDBY;
			elevatorIndex = index;
			from.name = string.Empty;
			from.height = float.NaN;
			to.name = string.Empty;
			to.height = float.NaN;
		}
	}

	private DeviceMessage msForService = new DeviceMessage();
	private messages.Param responseMessage = new messages.Param();
	private Dictionary<string, float> floorList = new Dictionary<string, float>();
	private Dictionary<string, ElevatorEntity> elevatorList = new Dictionary<string, ElevatorEntity>();
	private ConcurrentQueue<ElevatorTask> elevatorTaskQueue = new ConcurrentQueue<ElevatorTask>();

	private string elevatorSystemName = string.Empty;

	private string initialFloor = string.Empty;

	protected override void OnAwake()
	{
		type = Type.ELEVATOR;
		partName = "elevator_system";
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Control");

		ReadFloorContext();
		ReadElevatorContext();

		GenerateResponseMessage();

		AddThread(ServiceThread);

		StartCoroutine(ServiceLoop());
	}

	protected override void OnReset()
	{
		var targetFloorHeight = GetFloorHeight(initialFloor);

		if (!float.IsNaN(targetFloorHeight))
		{
			foreach (var item in elevatorList)
			{
				var elevator = item.Value;
				elevator.Control.Height =targetFloorHeight;
			}
		}
		else
		{
			Debug.Log("There is no initialFloor info. " + initialFloor);
		}

		// empty the task queue
		while (elevatorTaskQueue.Count > 0)
		{
			if (elevatorTaskQueue.TryDequeue(out var task))
			{
				Debug.LogFormat("Empty task Queue: {0}, {1}, {2}", task.elevatorIndex, task.from.name, task.to.name);
			}
		}
	}

	public void ReadElevatorContext()
	{
		// ex) plugin parameters example
		// <system_name>ElevatorSystem_00</system_name>
		// <elevator prefix_name="Elevator_" speed="2">
		//   <floor>floor_collision</floor>
		//   <doors speed="0.6" closing_timer="10.0">
		//     <inside open_offset="0.567">
		//       <door name="left">seocho_EV_door_L_link</door>
		//       <door name="right">seocho_EV_door_R_link</door>
		//     </inside>
		//     <outside open_offset="0.567">
		//       <door name="left">seocho_EV_out_doors_L_link</door>
		//       <door name="right">seocho_EV_out_doors_R_link</door>
		//     </outside>
		//   </doors>
		// </elevator>
		elevatorSystemName = parameters.GetValue<string>("system_name");
		var elevatorPrefixName = parameters.GetAttribute<string>("elevator", "prefix_name");
		var elevatorSpeed = parameters.GetAttribute<float>("elevator", "speed");
		var elevatorFloor = parameters.GetValue<string>("elevator/floor");

		var elevatorDoorSpeed = parameters.GetAttribute<float>("elevator/doors", "speed");
		var elevatorDoorAutoClosingTimer = parameters.GetAttribute<float>("elevator/doors", "closing_timer");

		var elevatorInsideopenOffset = parameters.GetAttribute<float>("elevator/doors/inside","open_offset");
		var elevatorInsideDoorNameLeft = parameters.GetValue<string>("elevator/doors/inside/door[@name='left']");
		var elevatorInsideDoorNameRight = parameters.GetValue<string>("elevator/doors/inside/door[@name='right']");

		var elevatorOutsideopenOffset = parameters.GetAttribute<float>("elevator/doors/outside","open_offset");
		var elevatorOutsideDoorNameLeft = parameters.GetValue<string>("elevator/doors/outside/door[@name='left']");
		var elevatorOutsideDoorNameRight = parameters.GetValue<string>("elevator/doors/outside/door[@name='right']");

		var index = 0;
		foreach (var child in this.GetComponentsInChildren<Transform>())
		{
			var objectName = child.name;
			if (objectName.StartsWith(elevatorPrefixName))
			{
				var elevatorControl = child.gameObject.AddComponent<ElevatorControl>();
				elevatorControl.liftControl.speed = elevatorSpeed;
				elevatorControl.liftControl.floorColliderName = elevatorFloor;

				elevatorControl.doorLinkNameLeft = elevatorInsideDoorNameLeft;
				elevatorControl.doorLinkNameRight = elevatorInsideDoorNameRight;
				elevatorControl.doorsControl.speed = elevatorDoorSpeed;
				elevatorControl.doorsControl.openOffset = elevatorInsideopenOffset;

				elevatorControl.outsideDoorLinkNameLeft = elevatorOutsideDoorNameLeft;
				elevatorControl.outsideDoorLinkNameRight = elevatorOutsideDoorNameRight;
				elevatorControl.outsideDoorsControl.speed = elevatorDoorSpeed;
				elevatorControl.outsideDoorsControl.openOffset = elevatorOutsideopenOffset;
				elevatorControl.doorAutoClosingTimer = elevatorDoorAutoClosingTimer;

				var elevatorEntity = new ElevatorEntity(objectName, elevatorControl);

				// TODO: will chnage to object name
				// elevatorList.Add(objectName, elevatorEntity);
				elevatorList.Add(index.ToString(), elevatorEntity);

				index++;
			}
		}
	}

	public void ReadFloorContext()
	{
		this.initialFloor = "B1F";
		if (parameters.GetValues<string>("floors/floor/name", out var listFloorName) &&
				parameters.GetValues<float>("floors/floor/height", out var listFloorHeight))
		{
			if (listFloorName.Count == listFloorHeight.Count)
			{
				initialFloor = listFloorName[0];

				var count = listFloorName.Count;
				while (count-- > 0)
				{
					floorList.Add(listFloorName[count], listFloorHeight[count]);
					Debug.Log(listFloorName[count] + " => " + listFloorHeight[count]);
				}
			}
		}
	}

	public float GetFloorHeight(in string targetFloor)
	{
		if (!string.IsNullOrEmpty(targetFloor))
		{
			if (floorList.TryGetValue(targetFloor, out var targetFloorHeight))
			{
				return targetFloorHeight;
			}
		}
		return float.NaN;
	}

	public string GetFloorName(in float targetFloorHeight)
	{
		if (!float.IsNaN(targetFloorHeight))
		{
			foreach (var floorItem in floorList)
			{
				var floorHeight = floorItem.Value;

				if (Mathf.Abs(floorHeight - targetFloorHeight) < Mathf.Epsilon)
				{
					return floorItem.Key;
				}
			}
		}
		return string.Empty;
	}

	private void GenerateResponseMessage()
	{
		var serviceNameParam = new messages.Param
		{
			Name = "service_name",
			Value = new messages.Any { Type = messages.Any.ValueType.String, StringValue = string.Empty }
		};

		var resultParam = new messages.Param
		{
			Name = "result",
			Value = new messages.Any { Type = messages.Any.ValueType.Boolean, BoolValue = false }
		};

		var elevatorIndexParam = new messages.Param
		{
			Name = "elevator_index",
			Value = new messages.Any { Type = messages.Any.ValueType.String, StringValue = string.Empty }
		};

		var floorParam = new messages.Param
		{
			Name = "current_floor",
			Value = new messages.Any { Type = messages.Any.ValueType.String, StringValue = string.Empty }
		};

		var heightParam = new messages.Param
		{
			Name = "height",
			Value = new messages.Any { Type = messages.Any.ValueType.Double, DoubleValue = 0 }
		};

		responseMessage.Name = string.Empty;
		responseMessage.Childrens.Add(serviceNameParam);
		responseMessage.Childrens.Add(resultParam);
		responseMessage.Childrens.Add(elevatorIndexParam);
		responseMessage.Childrens.Add(floorParam);
		responseMessage.Childrens.Add(heightParam);
	}

	private void SetResponseMessage(in bool result, in string elevatorIndex, in string currentFloor, in float height)
	{
		responseMessage.Childrens[1].Value.BoolValue = result;
		responseMessage.Childrens[2].Value.StringValue = elevatorIndex;
		responseMessage.Childrens[3].Value.StringValue = currentFloor;
		responseMessage.Childrens[4].Value.DoubleValue = height;
	}

	private void ServiceThread()
	{
		while (IsRunningThread)
		{
			var receivedBuffer = ReceiveRequest();

			if (receivedBuffer != null)
			{
				var requestMessage = ParsingInfoRequest(receivedBuffer, ref msForService);

				if (requestMessage.Name.Equals("request_system_name"))
				{
					SetSystemNameResponse(requestMessage);
					SendResponse(msForService);
				}
				else
				{
					var streamToResponse = HandleServiceRequest(requestMessage);
					SendResponse(streamToResponse);
				}
			}

			WaitThread();
		}
	}

	private void SetSystemNameResponse(in messages.Param receivedMessage)
	{
		var response = new messages.Param();
		response.Name = "request_system_name";
		response.Value = new messages.Any { Type = messages.Any.ValueType.String, StringValue = elevatorSystemName };
		msForService.SetMessage<messages.Param>(response);
	}

	private MemoryStream HandleServiceRequest(in messages.Param receivedMessage)
	{
		messages.Param param = null;

		if (!elevatorSystemName.Equals(receivedMessage.Name))
		{
			Debug.LogWarningFormat("It's differnt elevator system name({0}) vs received({1})", elevatorSystemName, receivedMessage.Name);
		}

		var serviceName = string.Empty;
		param = receivedMessage.Childrens[0];
		if (param.Name.Equals("service_name") &&
				param.Value.Type.Equals(messages.Any.ValueType.String))
		{
			serviceName = param.Value.StringValue;
		}

		var currentFloor = string.Empty;
		param = receivedMessage.Childrens[1];
		if (param.Name.Equals("current_floor") &&
				param.Value.Type.Equals(messages.Any.ValueType.String))
		{
			currentFloor = param.Value.StringValue;
		}

		var targetFloor = string.Empty;
		param = receivedMessage.Childrens[2];
		if (param.Name.Equals("target_floor") &&
				param.Value.Type.Equals(messages.Any.ValueType.String))
		{
			targetFloor = param.Value.StringValue;
		}

		var elevatorIndex = string.Empty;
		param = receivedMessage.Childrens[3];
		if (param.Name.Equals("elevator_index") &&
				param.Value.Type.Equals(messages.Any.ValueType.String))
		{
			elevatorIndex = param.Value.StringValue;
		}

		// Debug.LogFormat("Parsed {0} {1} {2} {3} {4}", elevatorSystemName, serviceName, currentFloor, targetFloor, elevatorIndex);

		responseMessage.Name = elevatorSystemName;
		responseMessage.Childrens[0].Value.StringValue = serviceName;

		HandleService(serviceName, currentFloor, targetFloor, elevatorIndex);

		ClearDeviceMessage(ref msForService);

		Serializer.Serialize<messages.Param>(msForService, responseMessage);

		return msForService;
	}

	private void HandleService(in string serviceName, string currentFloor, in string targetFloor, string elevatorIndex)
	{
		var result = false;
		var height = 0f;

		switch (serviceName)
		{
			case "call_elevator":
				result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
				if (!result)
				{
					result = CallElevator(targetFloor, currentFloor);
				}
				break;

			case "get_called_elevator":
				result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
				break;

			case "select_elevator_floor":
				if (string.IsNullOrEmpty(elevatorIndex))
				{
					Debug.LogWarning("must set elevator index!!");
					result = false;
				}
				else
				{
					result = CallElevator(currentFloor, targetFloor, elevatorIndex);
				}
				break;

			case "request_door_open":
				result = RequestDoorOpen(elevatorIndex);
				break;

			case "request_door_close":
				result = RequestDoorClose(elevatorIndex);
				break;

			case "is_door_opened":
				result = IsElevatorDoorOpened(elevatorIndex);
				break;

			case "get_elevator_information":
				result = true;
				height = GetElevatorCurrentHeight(elevatorIndex);
				currentFloor = GetFloorName(height);
				break;

			default:
				Debug.LogError("Unkown service name: " + serviceName);
				break;
		}

		SetResponseMessage(result, elevatorIndex, currentFloor, height);
	}

	private IEnumerator ServiceLoop()
	{
		var waitForSeconds = new WaitForSeconds(Time.fixedDeltaTime);
		var waitUntil = new WaitUntil(() => elevatorTaskQueue.IsEmpty == false);

		while (true)
		{
			yield return waitUntil;
			// Debug.Log("New Task Queue added!!");

			if (elevatorTaskQueue.TryDequeue(out var task))
			{
				switch (task.state)
				{
					// Trigger service
					case ElevatorTaskState.DOOR_OPEN:
						DoServiceOpenDoor(ref task);
						break;

					case ElevatorTaskState.DOOR_CLOSE:
						DoServiceCloseDoor(ref task);
						break;

					case ElevatorTaskState.STANDBY:
						DoServiceInStandby(ref task);
						break;

					// handling service
					case ElevatorTaskState.PROCESSING:
						DoServiceInProcess(ref task);
						break;
				}

				// finishing service
				// Queue the task again if it is still in process.
				if (!task.state.Equals(ElevatorTaskState.DONE))
				{
					elevatorTaskQueue.Enqueue(task);
				}

				yield return waitForSeconds;
			}
		}
	}

	private void DoServiceOpenDoor(ref ElevatorTask task)
	{
		var elevator = elevatorList[task.elevatorIndex];
		elevator.Control.OpenDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceCloseDoor(ref ElevatorTask task)
	{
		var elevator = elevatorList[task.elevatorIndex];
		elevator.Control.CloseDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceInStandby(ref ElevatorTask task)
	{
		// new call
		if (string.IsNullOrEmpty(task.elevatorIndex))
		{
			// check if the elevator is already arrived
			foreach (var elevatorItem in elevatorList)
			{
				var elevator = elevatorItem.Value;
				if (elevator.State.Equals(ElevatorState.STOP) && elevator.Control.IsArrived(task.to.height))
				{
					Debug.LogFormat("Already arrived: {0}, from: {1}({2}), to: {3}", elevator.Name, task.from.name, task.from.height, task.to.name);
					task.state = ElevatorTaskState.DONE;
					return;
				}
			}

			// check if the elevator is already moving to target
			foreach (var otherTask in elevatorTaskQueue)
			{
				if (!otherTask.Equals(task) && otherTask.state.Equals(ElevatorTaskState.PROCESSING))
				{
					if (otherTask.to.name.Equals(task.to.name))
					{
						Debug.LogFormat("Already moving: {0}, from {1}, to {2}", task.elevatorIndex, task.to.name, task.from.name);
						task.state = ElevatorTaskState.DONE;
						return;
					}
				}
			}

			// find a new elevator among the elevators at rest and move!!
			foreach (var elevatorItem in elevatorList)
			{
				var elevator = elevatorItem.Value;
				if (elevator.State.Equals(ElevatorState.STOP))
				{
					task.elevatorIndex = elevatorItem.Key;
					elevator.MoveElevatorTo(task.to.height);

					Debug.LogFormat("move : {0}, from: {1}, to: {2}", elevator.Name, task.from.name, task.to.name);

					task.state = ElevatorTaskState.PROCESSING;
					break;
				}
			}
		}
		// select floor
		else
		{
			var elevator = elevatorList[task.elevatorIndex];
			Debug.LogFormat("Select floor: {0}, from: {1}, to: {2}", elevator.Name, task.from.name, task.to.name, task.from.height);

			if (elevator.Control.IsArrived(task.from.height))
			{
				elevator.MoveElevatorTo(task.from.height, task.to.height);
				task.state = ElevatorTaskState.PROCESSING;
			}
			else
			{
				Debug.LogWarningFormat("Wrong:: elevator is not arrived yet Select floor: {0}, {1}, {2}", elevator.Name, task.from.name, task.to.name);
				task.state = ElevatorTaskState.DONE;
			}
		}
	}

	private void DoServiceInProcess(ref ElevatorTask task)
	{
		// check if the elevator arrived
		var elevatorIndex = task.elevatorIndex;
		var elevator = elevatorList[elevatorIndex];

		if (elevator.Control.IsArrived(task.to.height))
		{
			elevator.SetState(ElevatorState.STOP);

			task.state = ElevatorTaskState.DONE;
		}
	}

#if UNITY_EDITOR
	// just for test....
	private KeyCode[] numKeyCodes = {
		 KeyCode.Alpha0,
		 KeyCode.Alpha1,
		 KeyCode.Alpha2,
		 KeyCode.Alpha3,
		 KeyCode.Alpha4,
		 KeyCode.Alpha5,
		 KeyCode.Alpha6,
		 KeyCode.Alpha7,
		 KeyCode.Alpha8,
		 KeyCode.Alpha9,
	 };

	void Update()
	{
		foreach (var numKey in numKeyCodes)
		{
			if (Input.GetKeyUp(numKey))
			{
				var index = (int)numKey - (int)(KeyCode.Alpha0);

				if (index >= elevatorList.Keys.Count)
				{
					Debug.LogFormat("{0} elevator does not exist.");
					break;
				}

				var elevatorIndex = elevatorList.Keys.ElementAt(index);
				Debug.Log("Test elevatorIndex: " + elevatorIndex);

				if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
				{
					RequestDoorClose(elevatorIndex);
				}
				else if (Input.GetKey(KeyCode.Tab) && Input.GetKey(KeyCode.LeftShift))
				{
					var result = GetCalledElevator("B1F", "25F", out elevatorIndex);
					// Debug.Log(elevatorIndex + " - " + result);
				}
				else if (Input.GetKey(KeyCode.Tab))
				{
					var result = CallElevator("B1F", "25F");
					// Debug.Log(result);
				}
				else if (Input.GetKey(KeyCode.LeftAlt))
				{
					CallElevator("25F", "B1F", elevatorIndex);
				}
				else if (Input.GetKey(KeyCode.LeftShift))
				{
					CallElevator("B1F", "25F", elevatorIndex);
				}
				else
				{
					RequestDoorOpen(elevatorIndex);
				}
			}
		}
	}
#endif
}