/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;
using System.IO;
using UnityEngine;
using ProtoBuf;
using Param = cloisim.msgs.Param;
using Any = cloisim.msgs.Any;

public partial class ElevatorSystem : DevicePlugin
{
	private enum ElevatorTaskState {DOOR_OPEN, DOOR_CLOSE, STANDBY, PROCESSING, DONE}

	private struct ElevatorTask
	{
		public ElevatorTaskState state;
		public int elevatorIndex;
		public string fromFloor;
		public float fromFloorHeight;
		public string toFloor;
		public float toFloorHeight;

		public ElevatorTask(in int index)
		{
			state = ElevatorTaskState.STANDBY;
			elevatorIndex = index;
			fromFloor = string.Empty;
			fromFloorHeight = float.NaN;
			toFloor = string.Empty;
			toFloorHeight = float.NaN;
		}
	}

	private const int NON_ELEVATOR_INDEX = -1;

	private MemoryStream msForService = new MemoryStream();
	private Param responseMessage = new Param();
	private Dictionary<string, float> floorList = new Dictionary<string, float>();
	private Dictionary<int, ElevatorEntity> elevatorList = new Dictionary<int, ElevatorEntity>();
	private ConcurrentQueue<ElevatorTask> elevatorTaskQueue = new ConcurrentQueue<ElevatorTask>();

	private string elevatorSystemName = string.Empty;

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
		const string initialFloor = "B1F";

		var targetFloorHeight = GetFloorHeight(initialFloor);

		if (!float.IsNaN(targetFloorHeight))
		{
			foreach (var item in elevatorList)
			{
				var elevator = item.Value;
				elevator.SetHeight(targetFloorHeight);
			}
		}
		else
		{
			Debug.Log("There is no initialFloor info. " + initialFloor);
		}

		// empty the task queue
		while (elevatorTaskQueue.Count > 0)
		{
			if (elevatorTaskQueue.TryDequeue(out var result))
			{
				Debug.LogFormat("Empty task Queue: {0}, {1}, {2}",result.elevatorIndex, result.fromFloor, result.toFloor);
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
		foreach (Transform child in transform)
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
				elevatorList.Add(index, elevatorEntity);

				index++;
			}
		}
	}

	public void ReadFloorContext()
	{
		if (parameters.GetValues<string>("floors/floor/name", out var listFloorName) &&
			parameters.GetValues<float>("floors/floor/height", out var listFloorHeight))
		{
			if (listFloorName.Count == listFloorHeight.Count)
			{
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
		var serviceNameParam = new Param
		{
			Name = "service_name",
			Value = new Any { Type = Any.ValueType.String, StringValue = string.Empty }
		};

		var resultParam = new Param
		{
			Name = "result",
			Value = new Any { Type = Any.ValueType.Boolean, BoolValue = false }
		};

		var elevatorIndexParam = new Param
		{
			Name = "elevator_index",
			Value = new Any { Type = Any.ValueType.Int32, IntValue = NON_ELEVATOR_INDEX }
		};

		var floorParam = new Param
		{
			Name = "current_floor",
			Value = new Any { Type = Any.ValueType.String, StringValue = string.Empty }
		};

		var heightParam = new Param
		{
			Name = "height",
			Value = new Any { Type = Any.ValueType.Double, DoubleValue = 0 }
		};

		responseMessage.Name = string.Empty;
		responseMessage.Childrens.Add(serviceNameParam);
		responseMessage.Childrens.Add(resultParam);
		responseMessage.Childrens.Add(elevatorIndexParam);
		responseMessage.Childrens.Add(floorParam);
		responseMessage.Childrens.Add(heightParam);
	}

	private void SetResponseMessage(in bool result, in int elevatorIndex, in string currentFloor, in float height)
	{
		responseMessage.Childrens[1].Value.BoolValue = result;
		responseMessage.Childrens[2].Value.IntValue = elevatorIndex;
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

			ThreadWait();
		}
	}

	private void SetSystemNameResponse(in Param receivedMessage)
	{
		var response = new Param();
		response.Name = "request_system_name";
		response.Value = new Any { Type = Any.ValueType.String, StringValue = elevatorSystemName };
		ClearMemoryStream(ref msForService);
		Serializer.Serialize<Param>(msForService, response);
	}

	private MemoryStream HandleServiceRequest(in Param receivedMessage)
	{
		Param param = null;

		if (!elevatorSystemName.Equals(receivedMessage.Name))
		{
			Debug.LogWarningFormat("It's differnt elevator system name({0}) vs received({1})", elevatorSystemName, receivedMessage.Name);
		}

		var serviceName = string.Empty;
		param = receivedMessage.Childrens[0];
		if (param.Name.Equals("service_name") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			serviceName = param.Value.StringValue;
		}

		var currentFloor = string.Empty;
		param = receivedMessage.Childrens[1];
		if (param.Name.Equals("current_floor") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			currentFloor = param.Value.StringValue;
		}

		var targetFloor = string.Empty;
		param = receivedMessage.Childrens[2];
		if (param.Name.Equals("target_floor") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			targetFloor = param.Value.StringValue;
		}

		var elevatorIndex = NON_ELEVATOR_INDEX;
		param = receivedMessage.Childrens[3];
		if (param.Name.Equals("elevator_index") &&
			param.Value.Type.Equals(Any.ValueType.Int32))
		{
			elevatorIndex = param.Value.IntValue;
		}

		// Debug.LogFormat("Parsed {0} {1} {2} {3} {4}", elevatorSystemName, serviceName, currentFloor, targetFloor, elevatorIndex);

		responseMessage.Name = elevatorSystemName;
		responseMessage.Childrens[0].Value.StringValue = serviceName;

		HandleService(serviceName, currentFloor, targetFloor, elevatorIndex);

		ClearMemoryStream(ref msForService);

		Serializer.Serialize<Param>(msForService, responseMessage);

		return msForService;
	}

	private void HandleService(in string serviceName, string currentFloor, in string targetFloor, int elevatorIndex)
	{
		var result = false;
		var height = 0f;

		switch (serviceName)
		{
			case "call_elevator":
				elevatorIndex = NON_ELEVATOR_INDEX;
				result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
				if (!result)
				{
					result = CallElevator(currentFloor, targetFloor);
				}
				break;

			case "get_called_elevator":
				result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
				break;

			case "select_elevator_floor":
				result = SelectElevatorFloor(elevatorIndex, targetFloor, currentFloor);
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
		var entity = elevatorList[task.elevatorIndex];
		entity.Elevator.OpenDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceCloseDoor(ref ElevatorTask task)
	{
		var entity = elevatorList[task.elevatorIndex];
		entity.Elevator.CloseDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceInStandby(ref ElevatorTask task)
	{
		// new call
		if (task.elevatorIndex <= NON_ELEVATOR_INDEX)
		{
			// check if the elevator is already arrived
			foreach (var elevatorItem in elevatorList)
			{
				var elevator = elevatorItem.Value;
				if (elevator.State.Equals(ElevatorState.STOP) && elevator.IsArrived(task.fromFloorHeight))
				{
					Debug.LogFormat("Already arrived: {0}, {1}, {2} => {3}", elevator.Name, task.fromFloor, task.toFloor, task.fromFloorHeight);
					task.state = ElevatorTaskState.DONE;
					return;
				}
			}

			// check if the elevator is already moving to target
			foreach (var otherTask in elevatorTaskQueue)
			{
				if (!otherTask.Equals(task) && otherTask.state.Equals(ElevatorTaskState.PROCESSING))
				{
					if (otherTask.toFloor.Equals(task.fromFloor))
					{
						Debug.LogFormat("Already moving: {0}, {1}, {2}", task.fromFloor, task.toFloor, task.fromFloorHeight);
						task.state = ElevatorTaskState.DONE;
						return;
					}
				}
			}

			// find a new elevator among the elevators at rest and move!!
			foreach (var elevatorItem in elevatorList)
			{
				var entity = elevatorItem.Value;
				if (entity.State.Equals(ElevatorState.STOP))
				{
					var elevatorIndex = elevatorItem.Key;
					task.elevatorIndex = elevatorIndex;

					entity.MoveElevatorTo(task.fromFloorHeight);

					Debug.LogFormat("Move floor: {0}, {1}, {2} => {3} == {4}", entity.Name, task.fromFloor, task.toFloor, task.fromFloorHeight, entity.Height);

					task.state = ElevatorTaskState.PROCESSING;
					break;
				}
			}
		}
		// select floor
		else
		{
			var entity = elevatorList[task.elevatorIndex];
			Debug.LogFormat("Select floor: {0}, {1}, {2} => {3} == {4}", entity.Name, task.fromFloor, task.toFloor, task.fromFloorHeight, entity.Height);

			if (entity.Elevator.IsArrived(task.fromFloorHeight))
			{
				entity.MoveElevatorTo(task.toFloorHeight);
				task.state = ElevatorTaskState.PROCESSING;
			}
			else
			{
				Debug.LogWarningFormat("Wrong:: elevator is not arrived yet Select floor: {0}, {1}, {2}", entity.Name, task.fromFloor, task.toFloor);
				task.state = ElevatorTaskState.DONE;
			}
		}
	}

	private void DoServiceInProcess(ref ElevatorTask task)
	{
		// check if the elevator arrived
		var elevatorIndex = task.elevatorIndex;
		var entity = elevatorList[elevatorIndex];

		if (entity.Elevator.IsArrived(task.toFloorHeight))
		{
			entity.SetState(ElevatorState.STOP);

			task.state = ElevatorTaskState.DONE;
		}
	}


#if UNITY_EDITOR
	// just for test
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
				var elevatorIndex = (int)numKey - (int)(KeyCode.Alpha0);
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
					SelectElevatorFloor(elevatorIndex, "B1F", "25F");
				}
				else if (Input.GetKey(KeyCode.LeftShift))
				{
					SelectElevatorFloor(elevatorIndex, "25F", "B1F");
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