/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class ElevatorSystem : CustomPlugin
{
	private enum ElevatorState {STOP = 0, UPWARD, DOWNWARD};
	private struct ElevatorEntity
	{
		private ElevatorState state;
		private string name;
		private ElevatorControl elevator;

		public ElevatorEntity(in string elevatorName, in ElevatorControl elevatorControl)
		{
			state = ElevatorState.STOP;
			name = elevatorName;
			elevator = elevatorControl;
		}

		public ElevatorState State => state;
		public string Name => name;
		public float Height => elevator.Height;
		public ElevatorControl Elevator => elevator;

		public void SetHeight(in float targetHeight)
		{
			elevator.Height = targetHeight;
		}

		public bool IsArrived(in float targetHeight)
		{
			return elevator.IsArrived(targetHeight);
		}

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
			var from = Height;
			SetState(from, to);
			elevator.MoveTo(to);
		}

		public void MoveElevatorTo(in float to, in float from)
		{
			SetState(from, to);
			elevator.MoveTo(to);
		}
	};

	private bool RequestDoorOpen(in int elevatorIndex)
	{
		var task = new ElevatorTask(elevatorIndex);
		task.state = ElevatorTaskState.DOOR_OPEN;
		elevatorTaskQueue.Enqueue(task);

		return true;
	}

	private bool RequestDoorClose(in int elevatorIndex)
	{
		var task = new ElevatorTask(elevatorIndex);
		task.state = ElevatorTaskState.DOOR_CLOSE;
		elevatorTaskQueue.Enqueue(task);

		return true;
	}

	private bool IsElevatorDoorOpened(in int elevatorIndex)
	{
		var entity = elevatorList[elevatorIndex];
		return entity.Elevator.IsDoorOpened();
	}

	private float GetElevatorCurrentHeight(in int elevatorIndex)
	{
		var entity = elevatorList[elevatorIndex];
		return entity.Height;
	}

	private bool SelectElevatorFloor(in int elevatorIndex, in string toTargetFloor, in string fromCurrentFloor)
	{
		var targetFloorHeight = GetFloorHeight(toTargetFloor);
		if (float.IsNaN(targetFloorHeight))
		{
			return false;
		}

		var task = new ElevatorTask(elevatorIndex);
		task.toFloor = toTargetFloor;
		task.toFloorHeight = targetFloorHeight;
		task.fromFloor = fromCurrentFloor;
		task.fromFloorHeight = GetFloorHeight(task.fromFloor);
		elevatorTaskQueue.Enqueue(task);

		return true;
	}

	private bool CallElevator(in string currentFloor, in string targetFloor)
	{
		var task = new ElevatorTask(NON_ELEVATOR_INDEX);
		task.toFloor = targetFloor;
		task.toFloorHeight = GetFloorHeight(task.toFloor);
		task.fromFloor = currentFloor;
		task.fromFloorHeight = GetFloorHeight(task.fromFloor);
		elevatorTaskQueue.Enqueue(task);

		// Debug.Log("Call elevator: " + task.elevatorIndex);

		return true;
	}

	private bool GetCalledElevator(in string currentFloor, in string targetFloor, out int elevatorIndex)
	{
		elevatorIndex = NON_ELEVATOR_INDEX;

		// Try to find in task queue
		foreach (var task in elevatorTaskQueue)
		{
			if (task.toFloor.Equals(currentFloor) && task.state.Equals(ElevatorTaskState.PROCESSING))
			{
				elevatorIndex = task.elevatorIndex;
				return true;
			}
		}

		var currentFloorHeight = GetFloorHeight(currentFloor);
		// If not, try to find in stopped elevator
		foreach (var elevatorItem in elevatorList)
		{
			var elevator = elevatorItem.Value;
			if (elevator.State.Equals(ElevatorState.STOP) && elevator.IsArrived(currentFloorHeight))
			{
				elevatorIndex = elevatorItem.Key;
				return true;
			}
		}

		return false;
	}
}