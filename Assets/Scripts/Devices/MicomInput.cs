/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class MicomInput : Device
{
	private const float MM2M = 0.001f;

	private gazebo.msgs.Param micomWritingData = null;

	private int wheelVelocityLeft = 0; // linear velocity in millimeter per second
	private int wheelVelocityRight = 0; // linear velocity in millimeter per second

	protected override void OnAwake()
	{
	}

	protected override void OnStart()
	{
		deviceName = "MicomInput";
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
		micomWritingData = GetMessageData<gazebo.msgs.Param>();

		if (micomWritingData.Name.Equals("control_type") &&
			micomWritingData.Value.IntValue == 1 &&
			micomWritingData.Childrens.Count == 2)
		{
			wheelVelocityLeft
				= (!micomWritingData.Childrens[0].Name.Equals("nLeftWheelVelocity")) ?
				0 : micomWritingData.Childrens[0].Value.IntValue;

			wheelVelocityRight
				= (!micomWritingData.Childrens[1].Name.Equals("nRightWheelVelocity")) ?
				0 : micomWritingData.Childrens[1].Value.IntValue;

			// Debug.Log("nLeftWheelVel: " + wheelVelocityLeft + ", nRightWheelVel : " + wheelVelocityRight);
		}
		// Debug.Log("MicomInput: Working OK...");
	}

	public float GetWheelVelocity(in int index)
	{
		var velocity = 0f;
		switch (index)
		{
			case 0:
				velocity = GetWheelVelocityLeft();
				break;

			case 1:
				velocity = GetWheelVelocityRight();
				break;

			default:
				Debug.LogError("Invalid index - " + index);
				break;
		}

		return velocity;
	}

	public float GetWheelVelocityLeft()
	{
		return (float)wheelVelocityLeft * MM2M * Mathf.Rad2Deg;
	}

	public float GetWheelVelocityRight()
	{
		return (float)wheelVelocityRight * MM2M * Mathf.Rad2Deg;
	}

}
