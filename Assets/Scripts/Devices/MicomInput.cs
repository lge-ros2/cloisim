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
	private MicomSensor micomForWheelDrive = null;

	public enum VelocityType {Unknown, LinearAndAngular, LeftAndRight};

	private messages.Param micomWritingData = null;

	private VelocityType controlType = VelocityType.Unknown;

	public VelocityType ControlType => controlType;

	private float wheelLinearVelocityLeft = 0; // m/s
	private float wheelLinearVelocityRight = 0; // m/s

	private float linearVelocity = 0; // m/s
	private float angularVelocity = 0; // rad/s

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

			GenerateMessage();
			DoWheelDrive();
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
		try
		{
			micomWritingData = GetMessageData<messages.Param>();
		}
		catch
		{
			Debug.LogWarning("GetMessageData: ERROR");
		}

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
					= (!child1.Name.Equals("AngularVelocity")) ? 0 : (float)child1.Value.DoubleValue;
			}
			else if (micomWritingData.Value.IntValue == 1)
			{
				controlType = VelocityType.LeftAndRight;

				wheelLinearVelocityLeft
					= (!child0.Name.Equals("LeftWheelVelocity")) ? 0 : (float)child0.Value.DoubleValue;

				wheelLinearVelocityRight
					= (!child1.Name.Equals("RightWheelVelocity")) ? 0 : (float)child1.Value.DoubleValue;
			}
			else
			{
				controlType = VelocityType.Unknown;
				Debug.LogWarningFormat("MicomInput: Unsupported Control Type({0}", controlType);
			}
		}
	}

	public void SetMicomSensor(in MicomSensor targetDevice)
	{
		micomForWheelDrive = targetDevice;
	}

	private void DoWheelDrive()
	{
		if (micomForWheelDrive == null)
		{
			Debug.LogWarning("micom device for wheel drive is not ready!!");
			return;
		}

		switch (ControlType)
		{
			case MicomInput.VelocityType.LinearAndAngular:
				var targetLinearVelocity = GetLinearVelocity();
				var targetAngularVelocity = GetAngularVelocity();
				micomForWheelDrive.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
				break;

			case MicomInput.VelocityType.LeftAndRight:
				var targetWheelLeftLinearVelocity = GetWheelLeftVelocity();
				var targetWheelRightLinearVelocity = GetWheelRightVelocity();
				micomForWheelDrive.SetDifferentialDrive(targetWheelLeftLinearVelocity, targetWheelRightLinearVelocity);
				break;

			case MicomInput.VelocityType.Unknown:
				break;
		}
	}

	public float GetWheelLeftVelocity()
	{
		return (float)wheelLinearVelocityLeft;
	}

	public float GetWheelRightVelocity()
	{
		return (float)wheelLinearVelocityRight;
	}

	public float GetLinearVelocity()
	{
		return (float)linearVelocity;
	}

	public float GetAngularVelocity()
	{
		return (float)angularVelocity;
	}
}