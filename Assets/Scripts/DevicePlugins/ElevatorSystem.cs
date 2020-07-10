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
using Param = gazebo.msgs.Param;
using Any = gazebo.msgs.Any;

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

	private bool isRunningThread = true;
	private MemoryStream memoryStreamForService = null;
	private Param responseMessage = null;
	private Dictionary<string, float> floorList;
	private Dictionary<int, ElevatorEntity> elevatorList;
	private ConcurrentQueue<ElevatorTask> elevatorTaskQueue;

	ElevatorSystem()
	{
		memoryStreamForService = new MemoryStream();
		responseMessage = new Param();
		floorList = new Dictionary<string, float>();
		elevatorList = new Dictionary<int, ElevatorEntity>();
		elevatorTaskQueue = new ConcurrentQueue<ElevatorTask>();
	}

	protected override void OnAwake()
	{
		partName = "ElevatorSystem";

		var hashKey = modelName + partName;
		if (!RegisterServiceDevice(hashKey))
		{
			Debug.LogError("Failed to register ElevatorSystem service - " + hashKey);
		}
	}

	protected override void OnStart()
	{
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

	void OnDestroy()
	{
		isRunningThread = false;
	}

	public void ReadElevatorContext()
	{
		// ex) plugin parameters example
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

		var elevatorPrefixName = GetPluginAttribute<string>("elevator", "prefix_name");
		var elevatorSpeed = GetPluginAttribute<float>("elevator", "speed");
		var elevatorFloor = GetPluginValue<string>("elevator/floor");

		var elevatorDoorSpeed = GetPluginAttribute<float>("elevator/doors", "speed");
		var elevatorDoorAutoClosingTimer = GetPluginAttribute<float>("elevator/doors", "closing_timer");

		var elevatorInsideopenOffset = GetPluginAttribute<float>("elevator/doors/inside","open_offset");
		var elevatorInsideDoorNameLeft = GetPluginValue<string>("elevator/doors/inside/door[@name='left']");
		var elevatorInsideDoorNameRight = GetPluginValue<string>("elevator/doors/inside/door[@name='right']");

		var elevatorOutsideopenOffset = GetPluginAttribute<float>("elevator/doors/outside","open_offset");
		var elevatorOutsideDoorNameLeft = GetPluginValue<string>("elevator/doors/outside/door[@name='left']");
		var elevatorOutsideDoorNameRight = GetPluginValue<string>("elevator/doors/outside/door[@name='right']");

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
		if (GetPluginValues<string>("floors/floor/name", out var listFloorName) &&
			GetPluginValues<float>("floors/floor/height", out var listFloorHeight))
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

	private void SetResponseMessage(in string elevatorSystemName, in string serviceName)
	{
		responseMessage.Name = elevatorSystemName;
		responseMessage.Childrens[0].Value.StringValue = serviceName;
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
		byte[] receivedBuffer;

		while (isRunningThread)
		{
			receivedBuffer = ReceiveRequest();

			var receivedMessage = GetReceivedMessage(receivedBuffer);

			var streamToResponse = HandleServiceRequest(receivedMessage);

			SendResponse(streamToResponse);
		}
	}

	private Param GetReceivedMessage(in byte[] buffer)
	{
		ClearMemoryStream(ref memoryStreamForService);

		memoryStreamForService.Write(buffer, 0, buffer.Length);
		memoryStreamForService.Position = 0;

		return Serializer.Deserialize<Param>(memoryStreamForService);
	}

	private MemoryStream HandleServiceRequest(in Param receivedMessage)
	{
		Param param = null;

		var elevatorSystemName = receivedMessage.Name;

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

		Debug.LogFormat("Parsed {0} {1} {2} {3} {4}", elevatorSystemName, serviceName, currentFloor, targetFloor, elevatorIndex);

		SetResponseMessage(elevatorSystemName, serviceName);

		HandleService(serviceName, currentFloor, targetFloor, elevatorIndex);

		ClearMemoryStream(ref memoryStreamForService);

		Serializer.Serialize<Param>(memoryStreamForService, responseMessage);

		return memoryStreamForService;
	}

	private void HandleService(in string serviceName, string currentFloor, in string targetFloor, int elevatorIndex)
	{
		var result = true;
		var height = 0f;

		if (serviceName.Equals("call_elevator"))
		{
			elevatorIndex = NON_ELEVATOR_INDEX;
			result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
			if (!result)
			{
				result = CallElevator(currentFloor, targetFloor);
			}
		}
		else if (serviceName.Equals("get_called_elevator"))
		{
			result = GetCalledElevator(currentFloor, targetFloor, out elevatorIndex);
		}
		else if (serviceName.Equals("select_elevator_floor"))
		{
			result = SelectElevatorFloor(elevatorIndex, targetFloor, currentFloor);
		}
		else if (serviceName.Equals("request_door_open"))
		{
			result = RequestDoorOpen(elevatorIndex);
		}
		else if (serviceName.Equals("request_door_close"))
		{
			result = RequestDoorClose(elevatorIndex);
		}
		else if (serviceName.Equals("is_door_opened"))
		{
			result = IsElevatorDoorOpened(elevatorIndex);
		}
		else if (serviceName.Equals("get_elevator_information"))
		{
			height = GetElevatorCurrentHeight(elevatorIndex);
			currentFloor = GetFloorName(height);
		}
		else
		{
			Debug.LogError("Unkown service name: " + serviceName);
			result = false;
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
				// Trigger service
				if (task.state.Equals(ElevatorTaskState.DOOR_OPEN))
				{
					DoServiceOpenDoor(ref task);
				}
				else if (task.state.Equals(ElevatorTaskState.DOOR_CLOSE))
				{
					DoServiceCloseDoor(ref task);
				}
				else if (task.state.Equals(ElevatorTaskState.STANDBY))
				{
					DoServiceInStandby(ref task);
				}

				// handling service
				if (task.state.Equals(ElevatorTaskState.PROCESSING))
				{
					DoServiceInProcess(ref task);
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
				Debug.LogErrorFormat("Wrong:: elevator is not arrived yet Select floor: {0}, {1}, {2}", entity.Name, task.fromFloor, task.toFloor);
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

	private static void ClearMemoryStream(ref MemoryStream ms)
	{
		if (ms != null)
		{
			if (ms != null)
			{
				ms.SetLength(0);
				ms.Position = 0;
				ms.Capacity = 0;
			}
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