/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class ElevatorSystem : DevicePlugin
{
	private enum ElevatorState {STOP = 0, UPWARD, DOWNWARD};
	private struct ElevatorEntity
	{
		private ElevatorState state;
		private string name;
		private ElevatorControl control;

		public ElevatorEntity(in string elevatorName, in ElevatorControl elevatorControl)
		{
			this.state = ElevatorState.STOP;
			this.name = elevatorName;
			this.control = elevatorControl;
		}

		public ElevatorState State => state;
		public string Name => name;
		public float Height => control.Height;
		public ElevatorControl Control => control;

		public void SetState(in float from, in float to)
		{
			if (to > from)
			{
				state = ElevatorState.UPWARD;
			}
			else if (to < from)
			{
				state = ElevatorState.DOWNWARD;
			}
			else
			{
				state = ElevatorState.STOP;
			}
		}

		public void SetState(in ElevatorState currentState)
		{
			state = currentState;
		}

		public void MoveElevatorTo(in float to)
		{
			SetState(this.Height, to);
			control.MoveTo(to);
		}

		public void MoveElevatorTo(in float from, in float to)
		{
			SetState(from, to);
			control.MoveTo(to);
		}
	};

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

	private bool IsElevatorDoorOpened(in string elevatorIndex)
	{
		var elevator = elevatorList[elevatorIndex];
		return elevator.Control.IsDoorOpened();
	}

	private float GetElevatorCurrentHeight(in string elevatorIndex)
	{
		var elevator = elevatorList[elevatorIndex];
		return elevator.Control.Height;
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

	private bool GetCalledElevator(in string currentFloor, in string targetFloor, out string elevatorIndex)
	{
		elevatorIndex = string.Empty;

		var currentFloorHeight = GetFloorHeight(currentFloor);
		// If not, try to find in stopped elevator
		foreach (var elevatorItem in elevatorList)
		{
			var elevator = elevatorItem.Value;
			if (elevator.State.Equals(ElevatorState.STOP) && elevator.Control.IsArrived(currentFloorHeight))
			{
				elevatorIndex = elevatorItem.Key;
				Debug.Log("Already elevator is stopped " + elevatorIndex);
				return true;
			}
		}

		// Try to find in task queue
		foreach (var task in elevatorTaskQueue)
		{
			if (task.to.name.Equals(currentFloor) && task.state.Equals(ElevatorTaskState.PROCESSING))
			{
				elevatorIndex = task.elevatorIndex;
				Debug.Log("Calling elevator " + elevatorIndex);
				return true;
			}
		}

		return false;
	}
}