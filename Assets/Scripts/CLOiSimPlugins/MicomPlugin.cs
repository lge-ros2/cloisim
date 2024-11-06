/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using System.Linq;
using System;
using Any = cloisim.msgs.Any;
using UnityEngine;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> _tfList = new List<TF>();
	private SensorDevices.MicomCommand _micomCommand = null;
	private SensorDevices.MicomSensor _micomSensor = null;
	private MotorControl _motorControl = null;
	private SDF.Helper.Link[] _linkHelperInChildren = null;
	private StringBuilder _log = new StringBuilder();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.MICOM;

		_micomSensor = gameObject.AddComponent<SensorDevices.MicomSensor>();
		_micomCommand = gameObject.AddComponent<SensorDevices.MicomCommand>();

		attachedDevices.Add("Command", _micomCommand);
		attachedDevices.Add("Sensor", _micomSensor);

		_log.Clear();
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

		Debug.Log(_log.ToString());
	}


	protected override void OnReset()
	{
		_motorControl?.Reset();
		_log.Clear();
	}

	private void SetupMicom()
	{
		_log.AppendLine($"SetupMicom({name})");

		_micomSensor.EnableDebugging = GetPluginParameters().GetValue<bool>("debug", false);

		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20f);
		if (updateRate.Equals(0))
		{
			_log.AppendLine("Update rate for micom CANNOT be 0. Set to default value 20 Hz");
			updateRate = 20f;
		}
		_micomSensor.SetUpdateRate(updateRate);

		if (GetPluginParameters().IsValidNode("self_balanced"))
		{
			SetBalancedWheel("self_balanced");
		}

		if (GetPluginParameters().IsValidNode("wheel"))
		{
			SetDifferentialDrive("wheel");
		}

		if (GetPluginParameters().IsValidNode("mowing"))
		{
			SetMowing();
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
			_micomSensor.SetIMU(targetImuSensorName);
		}
	}

	private void SetBalancedWheel(in string parameterPrefix)
	{
		_log.AppendLine($"SetBalancedWheel({parameterPrefix})");

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc") &&
			GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/mode"))
		{
			var outputMode = GetPluginParameters().GetValue<string> ($"{parameterPrefix}/smc/mode/output", "LQR");
			var switchingMode = GetPluginParameters().GetValue<string> ($"{parameterPrefix}/smc/mode/switching", "SAT");
			_log.AppendLine($"outputMode: {outputMode}, switchingMode: {switchingMode}");
			_motorControl = new BalancedDrive(this.transform, outputMode, switchingMode);
		}
		else
		{
			_motorControl = new BalancedDrive(this.transform);
		}

		if (_micomSensor != null)
		{
			_micomSensor.SetMotorControl(_motorControl);
		}

		if (_micomCommand != null)
		{
			_micomCommand.SetMotorControl(_motorControl);
		}

		var hipJointLeft = GetPluginParameters().GetValue<string>($"{parameterPrefix}/hip/joint[@type='left']");
		var hipJointRight = GetPluginParameters().GetValue<string>($"{parameterPrefix}/hip/joint[@type='right']");
		if (!string.IsNullOrEmpty(hipJointLeft) && !string.IsNullOrEmpty(hipJointRight))
		{
			(_motorControl as BalancedDrive).SetHipJoints(hipJointLeft, hipJointRight);

			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/hip/PID"))
			{
				SetMotorPID($"{parameterPrefix}/head/PID", (_motorControl as BalancedDrive).SetHipJointPID);
			}
		}

		var legJointLeft = GetPluginParameters().GetValue<string>($"{parameterPrefix}/leg/joint[@type='left']");
		var legJointRight = GetPluginParameters().GetValue<string>($"{parameterPrefix}/leg/joint[@type='right']");
		if (!string.IsNullOrEmpty(legJointLeft) && !string.IsNullOrEmpty(legJointRight))
		{
			(_motorControl as BalancedDrive).SetLegJoints(legJointLeft, legJointRight);

			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/leg/PID"))
			{
				SetMotorPID($"{parameterPrefix}/leg/PID", (_motorControl as BalancedDrive).SetLegJointPID);
			}
		}

		var headJoint = GetPluginParameters().GetValue<string>($"{parameterPrefix}/head/joint");
		if (!string.IsNullOrEmpty(headJoint))
		{
			(_motorControl as BalancedDrive).SetHeadJoint(headJoint);
		}

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/head/PID"))
		{
			SetMotorPID($"{parameterPrefix}/head/PID", (_motorControl as BalancedDrive).SetHeadJointPID);
		}

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc"))
		{
			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/param"))
			{
				var kSW = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/K_sw");
				var sigmaB = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/sigma_b");
				var wheelFF = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/wheel_ff");
				(_motorControl as BalancedDrive).SetSMCParams(kSW, sigmaB, wheelFF);
				_log.AppendLine($"SetBalancedWheel() => kSW: {kSW}, sigmaB: {sigmaB}, wheelFF: {wheelFF}");
			}

			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/state_space"))
			{
				var A = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/A");
				var B = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/B");
				var K = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/K");
				var S = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/S");
				// Debug.Log(A.Trim());
				// Debug.Log(B.Trim());
				// Debug.Log(K.Trim());
				// Debug.Log(S.Trim());
				(_motorControl as BalancedDrive).SetSMCNominalModel(A, B, K, S);
			}
		}

		SetDriveForWheel($"{parameterPrefix}/wheel");

		(_motorControl as BalancedDrive).ChangeWheelDriveType();
	}

	private void SetDifferentialDrive(in string parameterPrefix)
	{
		_motorControl = new DifferentialDrive(this.transform);

		if (_micomSensor != null)
		{
			_micomSensor.SetMotorControl(_motorControl);
		}

		if (_micomCommand != null)
		{
			_micomCommand.SetMotorControl(_motorControl);
		}

		SetDriveForWheel(parameterPrefix);
	}

	private void SetDriveForWheel(in string parameterPrefix)
	{
		var wheelRadius = GetPluginParameters().GetValue<float>($"{parameterPrefix}/radius");

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/tread"))
		{
			_log.AppendLine($"<tread> will be depreacted!! please use <separation>");
		}

		var wheelTread = GetPluginParameters().GetValue<float>($"{parameterPrefix}/tread"); // TODO: to be deprecated
		var wheelSeparation = GetPluginParameters().GetValue<float>($"{parameterPrefix}/separation", wheelTread);

		_motorControl.SetWheelInfo(wheelRadius, wheelSeparation);

		var wheelLeftName = GetPluginParameters().GetValue<string>($"{parameterPrefix}/location[@type='left']", string.Empty);
		var wheelRightName = GetPluginParameters().GetValue<string>($"{parameterPrefix}/location[@type='right']", string.Empty);

		var wheelRearNameLeft = GetPluginParameters().GetValue<string>($"{parameterPrefix}/location[@type='rear_left']", string.Empty);
		var wheelRearNameRight = GetPluginParameters().GetValue<string>($"{parameterPrefix}/location[@type='rear_right']", string.Empty);

		if (!wheelRearNameLeft.Equals(string.Empty) && !wheelRearNameRight.Equals(string.Empty))
		{
			_motorControl.AttachWheel(wheelLeftName, wheelRightName, wheelRearNameLeft, wheelRearNameRight);
		}
		else
		{
			_motorControl.AttachWheel(wheelLeftName, wheelRightName);
		}

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/PID"))
		{
			SetMotorPID($"{parameterPrefix}/PID", _motorControl.SetWheelPID);
		}
	}

	private void SetMotorPID(
		in string parameterPrefix,
		Action<float, float, float, float, float, float, float> SetPID)
	{
		var P = GetPluginParameters().GetValue<float>($"{parameterPrefix}/kp");
		var I = GetPluginParameters().GetValue<float>($"{parameterPrefix}/ki");
		var D = GetPluginParameters().GetValue<float>($"{parameterPrefix}/kd");

		var limitIntegral = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/integral");
		var limitOutput = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/output");

		var integralMin = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/integral/min", -Mathf.Abs(limitIntegral));
		var integralMax = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/integral/max", Mathf.Abs(limitIntegral));
		var outputMin = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/output/min", -Mathf.Abs(limitOutput));
		var outputMax = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/output/max", Mathf.Abs(limitOutput));
		_log.AppendLine($"SetMotorPID: {parameterPrefix}, {P}, {I}, {D}, {integralMin}, {integralMax}, {outputMin}, {outputMax}");

		SetPID(P, I, D, integralMin, integralMax, outputMin, outputMax);
	}

	private void SetMowing()
	{
		var targetBladeName = GetPluginParameters().GetAttributeInPath<string>("mowing/blade", "target");
		if (!string.IsNullOrEmpty(targetBladeName))
		{
			var linkHelpers = GetComponentsInChildren<SDF.Helper.Link>();
			var targetBlade = linkHelpers.FirstOrDefault(x => x.name == targetBladeName);

			if (targetBlade != null)
			{
				var mowingBlade = targetBlade.gameObject.AddComponent<MowingBlade>();

				mowingBlade.HeightMin = GetPluginParameters().GetValue<float>("mowing/blade/height/min", 0f);
				mowingBlade.HeightMax = GetPluginParameters().GetValue<float>("mowing/blade/height/max", 0.1f);
				mowingBlade.RevSpeedMax = GetPluginParameters().GetValue<UInt16>("mowing/blade/rev_speed/max", 1000);
				mowingBlade.Height = 0;
			}
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
							// _log.AppendLine("Battery: " + batteryName + ", Battery Consumer:" + consumption.ToString("F5"));
							targetBattery.Discharge(consumption);
							_micomSensor.SetBattery(targetBattery);
							break;
						}
					}
				}
			}
		}
	}

	private void LoadStaticTF()
	{
		_log.AppendLine("Loaded Static TF Info : " + modelName);

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			foreach (var link in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/static_transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				var (modelName, linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in _linkHelperInChildren)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) &&
						linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						staticTfList.Add(tf);
						_log.AppendLine(modelName + "::" + linkName + " : Static TF added");
						break;
					}
				}
			}
		}
	}

	private void LoadTF()
	{
		_log.AppendLine("Loaded TF Info : " + modelName);

		if (GetPluginParameters().GetValues<string>("ros2/transforms/link", out var links))
		{
			foreach (var link in links)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("ros2/transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");
				var (modelName, linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in _linkHelperInChildren)
				{
					if ((string.IsNullOrEmpty(modelName) ||
						 modelName.Equals("__default__") ||
						 (!string.IsNullOrEmpty(modelName) && linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						_tfList.Add(tf);
						_log.AppendLine(modelName + "::" + linkName + " : TF added");
						break;
					}
				}
			}
		}
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
