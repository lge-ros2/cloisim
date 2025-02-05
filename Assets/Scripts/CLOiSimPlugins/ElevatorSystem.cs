/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Collections;

using UnityEngine;
using Any = cloisim.msgs.Any;
using Param = cloisim.msgs.Param;

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

	private Elevator elevators = new Elevator();

	private Dictionary<string, float> floorList = new Dictionary<string, float>();

	private ConcurrentQueue<ElevatorTask> elevatorTaskQueue = new ConcurrentQueue<ElevatorTask>();

	private string elevatorSystemName = string.Empty;

	private string initialFloor = string.Empty;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.ELEVATOR;
		_modelName = "World";
		_partsName = this.GetType().Name;
	}

	protected override void OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Control"))
		{
			AddThread(portService, ServiceThread);
		}

		ReadFloorContext();
		ReadElevatorContext();


		StartCoroutine(ServiceLoop());
	}

	new void OnDestroy()
	{
		StopCoroutine(ServiceLoop());

		base.OnDestroy();
	}

	protected override void OnReset()
	{
		var targetFloorHeight = GetFloorHeight(initialFloor);

		if (!float.IsNaN(targetFloorHeight))
		{
			elevators.Reset(targetFloorHeight);
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
		//	<system_name>ElevatorSystem_00</system_name>
		//	<elevator prefix_name="Elevator_" speed="2">
		//		<floor>floor_collision</floor>
		//		<doors speed="0.6" closing_timer="10.0">
		//		<inside open_offset="0.567">
		//			<door name="left">seocho_EV_door_L_link</door>
		//			<door name="right">seocho_EV_door_R_link</door>
		//		</inside>
		//		<outside open_offset="0.567">
		//			<door name="left">seocho_EV_out_doors_L_link</door>
		//			<door name="right">seocho_EV_out_doors_R_link</door>
		//		</outside>
		//		</doors>
		//	</elevator>
		elevatorSystemName = GetPluginParameters().GetValue<string>("system_name");
		var elevatorPrefixName = GetPluginParameters().GetAttributeInPath<string>("elevator", "prefix_name");
		var elevatorSpeed = GetPluginParameters().GetAttributeInPath<float>("elevator", "speed");
		var elevatorFloor = GetPluginParameters().GetValue<string>("elevator/floor");

		var elevatorDoorSpeed = GetPluginParameters().GetAttributeInPath<float>("elevator/doors", "speed");
		var elevatorDoorAutoClosingTimer = GetPluginParameters().GetAttributeInPath<float>("elevator/doors", "closing_timer");

		var elevatorInsideopenOffset = GetPluginParameters().GetAttributeInPath<float>("elevator/doors/inside","open_offset");
		var elevatorInsideDoorNameLeft = GetPluginParameters().GetValue<string>("elevator/doors/inside/door[@name='left']");
		var elevatorInsideDoorNameRight = GetPluginParameters().GetValue<string>("elevator/doors/inside/door[@name='right']");

		var elevatorOutsideopenOffset = GetPluginParameters().GetAttributeInPath<float>("elevator/doors/outside","open_offset");
		var elevatorOutsideDoorNameLeft = GetPluginParameters().GetValue<string>("elevator/doors/outside/door[@name='left']");
		var elevatorOutsideDoorNameRight = GetPluginParameters().GetValue<string>("elevator/doors/outside/door[@name='right']");

		foreach (var child in this.GetComponentsInChildren<SDF.Helper.Model>())
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

				elevators.AddEntity(objectName, elevatorControl);
			}
		}
	}

	public void ReadFloorContext()
	{
		this.initialFloor = "B1F";
		if (GetPluginParameters().GetValues<string>("floors/floor/name", out var listFloorName) &&
			GetPluginParameters().GetValues<float>("floors/floor/height", out var listFloorHeight))
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

	private void GenerateResponseMessage(out Param responseMessage)
	{
		responseMessage = new Param();

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
			Value = new Any { Type = Any.ValueType.String, StringValue = string.Empty }
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

	private void SetResponseMessage(ref Param responseMessage, in bool result, in string elevatorIndex, in string currentFloor, in float height)
	{
		responseMessage.Childrens[1].Value.BoolValue = result;
		responseMessage.Childrens[2].Value.StringValue = elevatorIndex;
		responseMessage.Childrens[3].Value.StringValue = currentFloor;
		responseMessage.Childrens[4].Value.DoubleValue = height;
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		if (requestType.Equals("request_system_name"))
		{
			SetSystemNameResponse(ref response);
		}
	}


	protected override void HandleCustomRequestMessage(in string requestType, in List<Param> requestChildren, ref DeviceMessage response)
	{
		if (!requestType.Equals("request_system_name"))
		{
			if (!elevatorSystemName.Equals(requestType))
			{
				Debug.LogWarningFormat("It's differnt elevator system name({0}) vs received({1})", elevatorSystemName, requestType);
			}

			HandleServiceRequest(requestChildren, ref response);
		}
	}

	private void SetSystemNameResponse(ref DeviceMessage response)
	{
		var nameResponseMessage = new Param();
		nameResponseMessage.Name = "request_system_name";
		nameResponseMessage.Value = new Any { Type = Any.ValueType.String, StringValue = elevatorSystemName };
		response.SetMessage<Param>(nameResponseMessage);
	}

	private void HandleServiceRequest(in List<Param> requestChildren, ref DeviceMessage response)
	{
		Param param = null;

		var serviceName = string.Empty;
		param = requestChildren[0];
		if (param != null && param.Name.Equals("service_name") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			serviceName = param.Value.StringValue;
		}

		var currentFloor = string.Empty;
		param = requestChildren[1];
		if (param != null && param.Name.Equals("current_floor") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			currentFloor = param.Value.StringValue;
		}

		var targetFloor = string.Empty;
		param = requestChildren[2];
		if (param != null && param.Name.Equals("target_floor") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			targetFloor = param.Value.StringValue;
		}

		var elevatorIndex = string.Empty;
		param = requestChildren[3];
		if (param != null && param.Name.Equals("elevator_index") &&
			param.Value.Type.Equals(Any.ValueType.String))
		{
			elevatorIndex = param.Value.StringValue;
		}

		// Debug.LogFormat("Parsed {0} {1} {2} {3} {4}", elevatorSystemName, serviceName, currentFloor, targetFloor, elevatorIndex);
		GenerateResponseMessage(out var responseMessage);
		responseMessage.Name = elevatorSystemName;
		responseMessage.Childrens[0].Value.StringValue = serviceName;

		HandleService(ref responseMessage, serviceName, currentFloor, targetFloor, elevatorIndex);

		response.SetMessage<Param>(responseMessage);
	}

	private void HandleService(ref Param responseMessage, in string serviceName, string currentFloor, in string targetFloor, string elevatorIndex)
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
				result = elevators.IsDoorOpened(elevatorIndex);
				break;

			case "get_elevator_information":
				result = true;
				height = elevators.GetCurrentHeight(elevatorIndex);
				currentFloor = GetFloorName(height);
				break;

			default:
				Debug.LogError("Unkown service name: " + serviceName);
				break;
		}

		SetResponseMessage(ref responseMessage, result, elevatorIndex, currentFloor, height);
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
		elevators.GetEntity(task.elevatorIndex, out var elevatorEntity);
		elevatorEntity.Control.OpenDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceCloseDoor(ref ElevatorTask task)
	{
		elevators.GetEntity(task.elevatorIndex, out var elevatorEntity);
		elevatorEntity.Control.CloseDoor();
		task.state = ElevatorTaskState.DONE;
	}

	private void DoServiceInStandby(ref ElevatorTask task)
	{
		var elevatorName = task.elevatorIndex;

		// new call
		if (string.IsNullOrEmpty(elevatorName))
		{
			// check if the elevator is already arrived
			if (elevators.FindAlreadyStoppedEntity(task.to.height, out elevatorName))
			{
				Debug.LogFormat("Already arrived: {0}, from: {1}({2}), to: {3}", elevatorName, task.from.name, task.from.height, task.to.name);
				task.state = ElevatorTaskState.DONE;
				return;
			}

			// check if the elevator is already moving to target
			foreach (var otherTask in elevatorTaskQueue)
			{
				if (!otherTask.Equals(task) && otherTask.state.Equals(ElevatorTaskState.PROCESSING))
				{
					if (otherTask.to.name.Equals(task.to.name))
					{
						Debug.LogFormat("Already moving: {0}, from {1}, to {2}", elevatorName, task.to.name, task.from.name);
						task.state = ElevatorTaskState.DONE;
						return;
					}
				}
			}

			// find a new elevator among the elevators at rest and move!!
			if (elevators.FindAvailableElevatorAndMoveTo(task.to.height, out elevatorName))
			{
				task.elevatorIndex = elevatorName;
				Debug.LogFormat("move : {0}, from: {1}, to: {2}", elevatorName, task.from.name, task.to.name);
				task.state = ElevatorTaskState.PROCESSING;
			}
		}
		// select floor
		else
		{
			if (elevators.MoveTo(elevatorName, task.from.height, task.to.height))
			{
				task.state = ElevatorTaskState.PROCESSING;
			}
			else
			{
				Debug.LogWarningFormat("Wrong:: elevator is not arrived yet Select floor: {0}, {1}, {2}", elevatorName, task.from.name, task.to.name);
				task.state = ElevatorTaskState.DONE;
			}
		}
	}

	private void DoServiceInProcess(ref ElevatorTask task)
	{
		var elevatorName = task.elevatorIndex;

		// check if the elevator arrived
		if (elevators.IsArrived(elevatorName, task.to.height))
		{
			task.state = ElevatorTaskState.DONE;
		}
	}

	private bool RequestDoorOpen(in string elevatorIndex)
	{
		var task = new ElevatorTask(elevatorIndex);
		task.state = ElevatorTaskState.DOOR_OPEN;
		elevatorTaskQueue.Enqueue(task);
		return true;
	}

	private bool RequestDoorClose(in string elevatorIndex)
	{
		var task = new ElevatorTask(elevatorIndex);
		task.state = ElevatorTaskState.DOOR_CLOSE;
		elevatorTaskQueue.Enqueue(task);
		return true;
	}

	private bool CallElevator(in string fromCurrentFloor, in string toTargetFloor, in string elevatorIndex = "")
	{
		var task = new ElevatorTask(elevatorIndex);
		task.to.name = toTargetFloor;
		task.to.height = GetFloorHeight(task.to.name);
		task.from.name = fromCurrentFloor;
		task.from.height = GetFloorHeight(task.from.name);

		if (float.IsNaN(task.to.height) || float.IsNaN(task.from.height))
		{
			return false;
		}

		elevatorTaskQueue.Enqueue(task);
		// Debug.Log("Call elevator: " + task.elevatorIndex);
		return true;
	}

	private bool GetCalledElevator(in string currentFloor, in string targetFloor, out string elevatorName)
	{
		elevatorName = string.Empty;

		var currentFloorHeight = GetFloorHeight(currentFloor);

		// If not, try to find in stopped elevator
		if (elevators.FindAlreadyStoppedEntity(currentFloorHeight, out elevatorName))
		{
			Debug.Log("Already elevator is stopped: " + elevatorName);
			return true;
		}

		// Try to find in task queue
		foreach (var task in elevatorTaskQueue)
		{
			if (task.to.name.Equals(currentFloor) && task.state.Equals(ElevatorTaskState.PROCESSING))
			{
				elevatorName = task.elevatorIndex;
				Debug.Log("Calling elevator " + elevatorName);
				return true;
			}
		}

		return false;
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

				var elevatorName = elevators.GetEntityNameByIndex(index);
				if (elevatorName.Equals(string.Empty))
				{
					break;
				}

				Debug.Log("Test elevatorIndex: " + elevatorName);

				if (Input.GetKey(KeyCode.LeftAlt) && Input.GetKey(KeyCode.LeftShift))
				{
					RequestDoorClose(elevatorName);
				}
				else if (Input.GetKey(KeyCode.Tab) && Input.GetKey(KeyCode.LeftShift))
				{
					var result = GetCalledElevator("B1F", "25F", out elevatorName);
					// Debug.Log(elevatorIndex + " - " + result);
				}
				else if (Input.GetKey(KeyCode.Tab))
				{
					var result = CallElevator("B1F", "25F");
					// Debug.Log(result);
				}
				else if (Input.GetKey(KeyCode.LeftAlt))
				{
					CallElevator("25F", "B1F", elevatorName);
				}
				else if (Input.GetKey(KeyCode.LeftShift))
				{
					CallElevator("B1F", "25F", elevatorName);
				}
				else
				{
					RequestDoorOpen(elevatorName);
				}
			}
		}
	}
#endif
}