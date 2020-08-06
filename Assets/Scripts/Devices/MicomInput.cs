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
	private const float MM2M = 0.001f;
	public enum VelocityType {Unknown, LinearAndAngular, LeftAndRight};

	private messages.Param micomWritingData = null;

	private VelocityType controlType = VelocityType.Unknown;

	public VelocityType ControlType => controlType;


	// TODO: change to float type??
	private int wheelVelocityLeft = 0; // linear velocity in millimeter per second
	private int wheelVelocityRight = 0; // linear velocity in millimeter per second

	private int linearVelocity = 0; // linear velocity in millimeter per second
	private int angularVelocity = 0; // angular velocit in deg per second

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
			if (micomWritingData.Value.IntValue == 0)
			{
				controlType = VelocityType.LinearAndAngular;

				linearVelocity
					= (!micomWritingData.Childrens[0].Name.Equals("nLinearVelocity")) ?
					0 : micomWritingData.Childrens[0].Value.IntValue;

				angularVelocity
					= (!micomWritingData.Childrens[1].Name.Equals("nAngularVelocity")) ?
					0 : micomWritingData.Childrens[1].Value.IntValue;
			}
			else if (micomWritingData.Value.IntValue == 1)
			{
				controlType = VelocityType.LeftAndRight;

				wheelVelocityLeft
					= (!micomWritingData.Childrens[0].Name.Equals("nLeftWheelVelocity")) ?
					0 : micomWritingData.Childrens[0].Value.IntValue;

				wheelVelocityRight
					= (!micomWritingData.Childrens[1].Name.Equals("nRightWheelVelocity")) ?
					0 : micomWritingData.Childrens[1].Value.IntValue;
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
		return (float)wheelVelocityLeft * MM2M * Mathf.Rad2Deg;
	}

	public float GetWheelRightVelocity()
	{
		return (float)wheelVelocityRight * MM2M * Mathf.Rad2Deg;
	}

	public float GetLinearVelocity()
	{
		return (float)linearVelocity * MM2M;
	}

	public float GetAngularVelocity()
	{
		return (float)angularVelocity * Mathf.Deg2Rad;
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