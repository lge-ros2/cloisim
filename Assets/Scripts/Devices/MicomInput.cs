/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using messages = gazebo.msgs;

public class MicomInput : Device
{
	public enum VelocityType {Unknown, LinearAndAngular, LeftAndRight};

	private messages.Param micomWritingData = null;

	private VelocityType controlType = VelocityType.Unknown;

	public VelocityType ControlType => controlType;

	private float wheelVelocityLeft = 0; // deg/s
	private float wheelVelocityRight = 0; // deg/s

	private float linearVelocity = 0; // m/s
	private float angularVelocity = 0; // deg/s

	protected override void OnAwake()
	{
		deviceName = "MicomInput";
	}

	protected override void OnStart()
	{
	}

	protected override IEnumerator MainDeviceWorker()
	{
		var waitUntil = new WaitUntil(() => GetDataStream().Length > 0);

		while (true)
		{
			yield return waitUntil;

			try
			{
				GenerateMessage();
			}
			catch
			{
				Debug.LogError("MicomInput: Error");
			}
		}
	}

	protected override IEnumerator OnVisualize()
	{
		yield return null;
	}

	protected override void InitializeMessages()
	{
		micomWritingData = null;
	}

	protected override void GenerateMessage()
	{
		micomWritingData = GetMessageData<messages.Param>();

		if (micomWritingData.Name.Equals("control_type") &&
			micomWritingData.Childrens.Count == 2)
		{
			var child0 = micomWritingData.Childrens[0];
			var child1 = micomWritingData.Childrens[1];

			if (micomWritingData.Value.IntValue == 0)
			{
				controlType = VelocityType.LinearAndAngular;

				linearVelocity
					= (!child0.Name.Equals("LinearVelocity")) ? 0 : (float)child0.Value.DoubleValue;

				angularVelocity
					= (!child1.Name.Equals("AngularVelocity")) ? 0 : (float)child1.Value.DoubleValue * Mathf.Rad2Deg;
			}
			else if (micomWritingData.Value.IntValue == 1)
			{
				controlType = VelocityType.LeftAndRight;

				wheelVelocityLeft
					= (!child0.Name.Equals("LeftWheelVelocity")) ? 0 : ((float)child0.Value.DoubleValue * Mathf.Rad2Deg);

				wheelVelocityRight
					= (!child1.Name.Equals("RightWheelVelocity")) ? 0 : ((float)child1.Value.DoubleValue* Mathf.Rad2Deg);
			}
			else
			{
				controlType = VelocityType.Unknown;
				Debug.LogWarningFormat("MicomInput: Unsupported Control Type({0}", controlType);
			}
			// Debug.Log("nLeftWheelVel: " + wheelVelocityLeft + ", nRightWheelVel : " + wheelVelocityRight);
		}
		// Debug.Log("MicomInput: Working OK...");
	}

	public float GetWheelLeftVelocity()
	{
		return (float)wheelVelocityLeft;
	}

	public float GetWheelRightVelocity()
	{
		return (float)wheelVelocityRight;
	}

	public float GetLinearVelocity()
	{
		return (float)linearVelocity;
	}

	public float GetAngularVelocity()
	{
		return (float)angularVelocity;
	}

	public float GetVelocity(in int index)
	{
		switch (controlType)
		{
		case VelocityType.LinearAndAngular:
			return GetType0Velocity(index);

		case VelocityType.LeftAndRight:
			return GetType1Velocity(index);

		case VelocityType.Unknown:
		default:
			Debug.LogWarning("Unknown control type!!!! nothing to get velocity");
			break;
		}

		return 0;
	}


	private float GetType1Velocity(in int index)
	{
		var velocity = 0f;
		switch (index)
		{
			case 0:
				velocity = GetWheelLeftVelocity();
				break;

			case 1:
				velocity = GetWheelRightVelocity();
				break;

			default:
				Debug.LogError("Invalid index - " + index);
				break;
		}

		return velocity;
	}

	private float GetType0Velocity(in int index)
	{
		var velocity = 0f;
		switch (index)
		{
			case 0:
				velocity = GetLinearVelocity();
				break;

			case 1:
				velocity = GetAngularVelocity();
				break;

			default:
				Debug.LogError("Invalid index - " + index);
				break;
		}

		return velocity;
	}
}