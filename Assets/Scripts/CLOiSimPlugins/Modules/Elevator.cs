/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
#if UNITY_EDITOR
using System.Linq;
#endif
using UnityEngine;

public class Elevator
{
	public enum ElevatorState {STOP = 0, UPWARD, DOWNWARD};

	public struct ElevatorEntity
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

	private Dictionary<string, ElevatorEntity> elevatorList = new Dictionary<string, ElevatorEntity>();

	public void Reset(in float targetFloorHeight)
	{
		foreach (var item in elevatorList)
		{
			var elevator = item.Value;
			elevator.Control.Height = targetFloorHeight;
		}
	}

	public void AddEntity(in string elevatorName, in ElevatorControl elevatorControl)
	{
		var elevatorEntity = new ElevatorEntity(elevatorName, elevatorControl);

		// TODO: will chnage to object name
		// elevatorList.Add(objectName, elevatorEntity);

		var index = elevatorList.Count;
		elevatorList.Add(index.ToString(), elevatorEntity);
	}

	public bool GetEntity(in string elevatorName, out ElevatorEntity elevator)
	{
		if (elevatorList.TryGetValue(elevatorName, out elevator))
		{
			return true;
		}

		return false;
	}

#if UNITY_EDITOR
	public string GetEntityNameByIndex(in int index)
	{
		if (elevatorList.Keys.Count == 0 || index >= elevatorList.Keys.Count)
		{
			Debug.LogFormat("{0} elevator does not exist.", index);
			return string.Empty;
		}

		return elevatorList.Keys.ElementAt(index);
	}
#endif

	public bool IsDoorOpened(in string elevatorName)
	{
		if (GetEntity(elevatorName, out var elevator))
		{
			return elevator.Control.IsDoorOpened();
		}

		return false;
	}

	public float GetCurrentHeight(in string elevatorName)
	{
		if (GetEntity(elevatorName, out var elevator))
		{
			return elevator.Control.Height;
		}

		return float.NaN;
	}

	public bool FindAlreadyStoppedEntity(in float targetFloorHeight, out string elevatorName)
	{
		foreach (var elevator in elevatorList)
		{
			var entity = elevator.Value;
			if (entity.State.Equals(ElevatorState.STOP) && entity.Control.IsArrived(targetFloorHeight))
			{
				elevatorName = elevator.Key;
				return true;
			}
		}

		elevatorName = string.Empty;
		return false;
	}

	public bool IsArrived(in string elevatorName, in float targetFloorHeight)
	{
		if (GetEntity(elevatorName, out var elevator))
		{
			if (elevator.Control.IsArrived(targetFloorHeight))
			{
				elevator.SetState(ElevatorState.STOP);
				return true;
			}
		}

		return false;
	}

	public bool MoveTo(in string elevatorName, in float fromHeight, in float toHeight)
	{
		Debug.LogFormat("Select floor: {0}, from: {1}, to: {2}", elevatorName, fromHeight, toHeight);

		if (GetEntity(elevatorName, out var elevator))
		{
			if (elevator.Control.IsArrived(fromHeight))
			{
				elevator.MoveElevatorTo(fromHeight, toHeight);
				return true;
			}
		}

		return false;
	}

	public bool FindAvailableElevatorAndMoveTo(in float targetFloorHeight, out string elevatorName)
	{
		foreach (var elevator in elevatorList)
		{
			var entity = elevator.Value;
			if (entity.State.Equals(ElevatorState.STOP))
			{
				elevatorName = elevator.Key;
				entity.MoveElevatorTo(targetFloorHeight);
				return true;
			}
		}

		elevatorName = string.Empty;
		return false;
	}
}