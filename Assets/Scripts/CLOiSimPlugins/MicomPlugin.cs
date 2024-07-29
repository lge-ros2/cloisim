/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using Any = cloisim.msgs.Any;
using UnityEngine;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> _tfList = new List<TF>();
	private SensorDevices.MicomCommand _micomCommand = null;
	private SensorDevices.MicomSensor _micomSensor = null;
	private MotorControl _motorControl = null;
	private SDF.Helper.Link[] _linkHelperInChildren = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MICOM;

		_motorControl = new MotorControl(this.transform);

		_micomSensor = gameObject.AddComponent<SensorDevices.MicomSensor>();
		_micomSensor.SetMotorControl(_motorControl);
		_micomCommand = gameObject.AddComponent<SensorDevices.MicomCommand>();
		_micomCommand.SetMotorControl(_motorControl);

		attachedDevices.Add("Command", _micomCommand);
		attachedDevices.Add("Sensor", _micomSensor);
	}

	protected override void OnStart()
	{
		_linkHelperInChildren = GetComponentsInChildren<SDF.Helper.Link>();

		SetupMicom();

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, _micomCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, _micomSensor);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, _tfList);
		}

		LoadStaticTF();
		LoadTF();
	}

	protected override void OnReset()
	{
		if (_motorControl != null)
		{
			_motorControl.Reset();
		}
	}

	private void SetupMicom()
	{
		_micomSensor.EnableDebugging = GetPluginParameters().GetValue<bool>("debug", false);

		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20f);
		if (updateRate.Equals(0))
		{
			Debug.LogWarning("Update rate for micom CANNOT be 0. Set to default value 20 Hz");
			updateRate = 20f;
		}
		_micomSensor.SetUpdateRate(updateRate);

		var wheelRadius = GetPluginParameters().GetValue<float>("wheel/radius");

		if (GetPluginParameters().IsValidNode("wheel/tread"))
		{
			Debug.LogWarning("<wheel/tread> will be depreacted!! please use wheel/separation");
		}

		var wheelTread = GetPluginParameters().GetValue<float>("wheel/tread"); // TODO: to be deprecated
		var wheelSeparation = GetPluginParameters().GetValue<float>("wheel/separation", wheelTread);

		if (GetPluginParameters().IsValidNode("wheel/PID"))
		{
			var P = GetPluginParameters().GetValue<float>("wheel/PID/kp");
			var I = GetPluginParameters().GetValue<float>("wheel/PID/ki");
			var D = GetPluginParameters().GetValue<float>("wheel/PID/kd");
			_motorControl.SetPID(P, I, D);
		}

		_motorControl.SetWheelInfo(wheelRadius, wheelSeparation);

		var wheelLeftName = GetPluginParameters().GetValue<string>("wheel/location[@type='left']", string.Empty);
		var wheelRightName = GetPluginParameters().GetValue<string>("wheel/location[@type='right']", string.Empty);
		var rearWheelLeftName = GetPluginParameters().GetValue<string>("wheel/location[@type='rear_left']", string.Empty);
		var rearWheelRightName = GetPluginParameters().GetValue<string>("wheel/location[@type='rear_right']", string.Empty);

		if (!rearWheelLeftName.Equals(string.Empty) && !rearWheelRightName.Equals(string.Empty))
		{
			SetWheel(wheelLeftName, wheelRightName, rearWheelLeftName, rearWheelRightName);
		}
		else
		{
			SetWheel(wheelLeftName, wheelRightName);
		}

		if (GetPluginParameters().IsValidNode("battery"))
		{
			SetBattery();
		}

		if (GetPluginParameters().GetValues<string>("uss/sensor", out var ussList))
		{
			_micomSensor.SetUSS(ussList);
		}

		if (GetPluginParameters().GetValues<string>("ir/sensor", out var irList))
		{
			_micomSensor.SetIRSensor(irList);
		}

		if (GetPluginParameters().GetValues<string>("magnet/sensor", out var magnetList))
		{
			_micomSensor.SetMagnet(magnetList);
		}

		if (GetPluginParameters().GetValues<string>("bumper/sensor", out var bumperList))
		{
			_micomSensor.SetBumper(bumperList);
		}

		var targetImuSensorName = GetPluginParameters().GetValue<string>("imu");
		if (!string.IsNullOrEmpty(targetImuSensorName))
		{
			// Debug.Log("Imu Sensor = " + targetImuName);
			_micomSensor.SetIMU(targetImuSensorName);
		}
	}

	private void SetBattery()
	{
		if (GetPluginParameters().GetValues<string>("battery/voltage", out var batteryList))
		{
			foreach (var item in batteryList)
			{
				var batteryName = GetPluginParameters().GetAttributeInPath<string>("battery/voltage", "name");
				var consumption = GetPluginParameters().GetValue<float>("battery/voltage[@name='" + batteryName + "']/consumption");

				foreach (var linkHelper in _linkHelperInChildren)
				{
					var targetBattery = linkHelper.Battery;
					if (targetBattery != null)
					{
						if (targetBattery.Name.CompareTo(batteryName) == 0)
						{
							// Debug.Log("Battery: " + batteryName + ", Battery Consumer:" + consumption.ToString("F5"));
							targetBattery.Discharge(consumption);
							_micomSensor.SetBattery(targetBattery);
							break;
						}
					}
				}
			}
		}
	}

	public void SetWheel(in string wheelNameLeft, in string wheelNameRight)
	{
		var linkList = GetComponentsInChildren<SDF.Helper.Link>();
		foreach (var link in linkList)
		{
			var wheelLocation = MotorControl.WheelLocation.NONE;

			if (link.name.Equals(wheelNameLeft) || link.Model.name.Equals(wheelNameLeft))
			{
				wheelLocation = MotorControl.WheelLocation.LEFT;

			}
			else if (link.name.Equals(wheelNameRight) || link.Model.name.Equals(wheelNameRight))
			{
				wheelLocation = MotorControl.WheelLocation.RIGHT;
			}
			else
			{
				continue;
			}

			if (!wheelLocation.Equals(MotorControl.WheelLocation.NONE))
			{
				var motorObject = (link.gameObject != null) ? link.gameObject : link.Model.gameObject;
				_motorControl.AttachWheel(wheelLocation, motorObject);
			}
		}
	}

	public void SetWheel(in string frontWheelLeftName, in string frontWheelRightName, in string rearWheelLeftName, in string rearWheelRightName)
	{
		SetWheel(frontWheelLeftName, frontWheelRightName);

		var linkList = GetComponentsInChildren<SDF.Helper.Link>();
		foreach (var link in linkList)
		{
			var wheelLocation = MotorControl.WheelLocation.NONE;

			if (link.name.Equals(rearWheelLeftName) || link.Model.name.Equals(rearWheelLeftName))
			{
				wheelLocation = MotorControl.WheelLocation.REAR_LEFT;

			}
			else if (link.name.Equals(rearWheelRightName) || link.Model.name.Equals(rearWheelRightName))
			{
				wheelLocation = MotorControl.WheelLocation.REAR_RIGHT;
			}
			else
			{
				continue;
			}

			if (!wheelLocation.Equals(MotorControl.WheelLocation.NONE))
			{
				var motorObject = (link.gameObject != null) ? link.gameObject : link.Model.gameObject;
				_motorControl.AttachWheel(wheelLocation, motorObject);
			}
		}
	}

	private void LoadStaticTF()
	{
		var staticTfLog = new StringBuilder();
		staticTfLog.AppendLine("Loaded Static TF Info : " + modelName);

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			foreach (var link in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/static_transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				(var modelName, var linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in _linkHelperInChildren)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) &&
						linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						staticTfList.Add(tf);
						staticTfLog.AppendLine(modelName + "::" + linkName + " : static TF added");
						break;
					}
				}
			}
		}

		Debug.Log(staticTfLog.ToString());
	}

	private void LoadTF()
	{
		var tfLog = new StringBuilder();
		tfLog.AppendLine("Loaded TF Info : " + modelName);

		if (GetPluginParameters().GetValues<string>("ros2/transforms/link", out var links))
		{
			foreach (var link in links)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				(var modelName, var linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in _linkHelperInChildren)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) && linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						_tfList.Add(tf);
						tfLog.AppendLine(modelName + "::" + linkName + " : TF added");
						break;
					}
				}
			}
		}

		Debug.Log(tfLog.ToString());
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "reset_odometry":
				Reset();
				SetEmptyResponse(ref response);
				break;

			default:
				break;
		}
	}
}
