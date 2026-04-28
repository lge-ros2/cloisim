/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System.Text;
using System;
using Any = cloisim.msgs.Any;
using UnityEngine;
using UnityEngine.Video;
using SDFormat;
using Material = UnityEngine.Material;

public class MicomPlugin : CLOiSimPlugin
{
	private List<TF> _tfList = new List<TF>();
	private SensorDevices.MicomCommand _micomCommand = null;
	private SensorDevices.MicomSensor _micomSensor = null;
	private MotorControl _motorControl = null;
	private SDFormat.Helper.Link[] _linkHelperInChildren = null;
	private List<string> _displaySourceUris = new();

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.MICOM;
		_partsName = (GetPluginParameters() == null || string.IsNullOrEmpty(GetPluginParameters().Name)) ? "Micom" : GetPluginParameters().Name;

		_micomSensor = gameObject.AddComponent<SensorDevices.MicomSensor>();
		_micomCommand = gameObject.AddComponent<SensorDevices.MicomCommand>();
	}

	protected override IEnumerator OnStart()
	{
		_linkHelperInChildren = GetComponentsInChildren<SDFormat.Helper.Link>();

		SetupMicom();

		LoadStaticTF();
		LoadTF();

		yield return null;

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

		yield return null;
	}

	protected override void OnReset()
	{
		_motorControl?.Reset();
	}

	private void SetupMicom()
	{
		StartSummary.AppendLine($"[SetupMicom({name})]");

		_micomSensor.EnableDebugging = GetPluginParameters().GetValue("debug", false);

		var updateRate = GetPluginParameters().GetValue("update_rate", 20f);
		if (updateRate.Equals(0))
		{
			StartSummary.AppendLine("Update rate for micom CANNOT be 0. Set to default value 20 Hz");
			updateRate = 20f;
		}
		_micomSensor.SetUpdateRate(updateRate);

		if (GetPluginParameters().IsValidNode("self_balanced"))
		{
			SetSelfBalancedWheel("self_balanced");
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

		if (GetPluginParameters().IsValidNode("display"))
		{
			SetDisplay();
		}

		_micomSensor.PrintSensors();
	}

	private void SetDisplay()
	{
		const float meshScalingFactor = 1000f;
		var targetVisual = GetPluginParameters().GetValue("display/target/visual", string.Empty);

		if (string.IsNullOrEmpty(targetVisual))
		{
			StartSummary.AppendLine("Failed to set display - Empty target visual for display");
			return;
		}

		if (GetPluginParameters().GetValues("display/source/uri", out _displaySourceUris) == false)
		{
			StartSummary.AppendLine("Failed to set display - Empty display source uri");
			return;
		}

		var defaultSourceUri = GetPluginParameters().GetValue("display/source/uri[@default='true']", _displaySourceUris[0]);

		var visualHelpers = GetComponentsInChildren<SDFormat.Helper.Visual>();
		foreach (var visualHelper in visualHelpers)
		{
			if (visualHelper.name.Equals(targetVisual))
			{
				var meshFilter = visualHelper.GetComponentInChildren<MeshFilter>();
				var meshRenderer = visualHelper.GetComponentInChildren<MeshRenderer>();
				if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
				{
					StartSummary.AppendLine($"Failed to set display - Missing mesh or renderer for visual '{targetVisual}'");
					continue;
				}

				var mesh = meshFilter.sharedMesh;
				var displaySize = mesh.bounds.size;
				var videoWidth = Mathf.RoundToInt(displaySize.x * meshScalingFactor);
				var videoHeight = Mathf.RoundToInt(displaySize.z * meshScalingFactor);
				if (videoWidth <= 0 || videoHeight <= 0)
				{
					StartSummary.AppendLine($"Failed to set display - Invalid display size for visual '{targetVisual}'");
					continue;
				}

				var renderTexture = new RenderTexture(videoWidth, videoHeight, 0)
				{
					name = "VideoTexture",
					hideFlags = HideFlags.DontUnloadUnusedAsset
				};

				var shader = Shader.Find("Custom/Unlit/VideoTexture");
				if (shader == null)
				{
					StartSummary.AppendLine("Failed to set display - Shader not found: Custom/Unlit/VideoTexture");
					return;
				}

				meshRenderer.material = new Material(shader)
				{
					hideFlags = HideFlags.DontUnloadUnusedAsset
				};
				meshRenderer.sharedMaterial.SetTexture("_MainTex", renderTexture);

				var videoPlayer = visualHelper.gameObject.AddComponent<VideoPlayer>();
				videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
				videoPlayer.isLooping = true;
				videoPlayer.source = VideoSource.Url;
				videoPlayer.playOnAwake = true;
				videoPlayer.waitForFirstFrame = true;
				videoPlayer.url = defaultSourceUri;
				videoPlayer.renderMode = VideoRenderMode.RenderTexture;
				videoPlayer.targetTexture = renderTexture;
				videoPlayer.aspectRatio = VideoAspectRatio.Stretch;
				break;
			}
		}
	}

	private void SetSelfBalancedWheel(in string parameterPrefix)
	{
		StartSummary.AppendLine($"SetSelfBalancedWheel({parameterPrefix})");

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc") &&
			GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/mode"))
		{
			var outputMode = GetPluginParameters().GetValue($"{parameterPrefix}/smc/mode/output", "LQR");
			var switchingMode = GetPluginParameters().GetValue($"{parameterPrefix}/smc/mode/switching", "SAT");
			StartSummary.AppendLine($"outputMode: {outputMode}, switchingMode: {switchingMode}");

			_motorControl = new SelfBalancedDrive(transform, outputMode, switchingMode);
		}
		else
		{
			_motorControl = new SelfBalancedDrive(transform);
		}

		if (_micomSensor != null)
		{
			_micomSensor.SetMotorControl(_motorControl);
		}

		if (_micomCommand != null)
		{
			_micomCommand.SetMotorControl(_motorControl);
		}

		var autostart = GetPluginParameters().GetAttributeInPath<bool>(parameterPrefix, "autostart");
		if (autostart)
		{
			(_motorControl as SelfBalancedDrive).Balancing = true;
		}
		StartSummary.AppendLine($"AutoStart: {autostart}");

		var headJoint = GetPluginParameters().GetValue<string>($"{parameterPrefix}/head/joint");
		if (!string.IsNullOrEmpty(headJoint))
		{
			(_motorControl as SelfBalancedDrive).SetHeadJoint(headJoint);
		}

		var hipJointLeft = GetPluginParameters().GetValue<string>($"{parameterPrefix}/hip/joint[@type='left']");
		var hipJointRight = GetPluginParameters().GetValue<string>($"{parameterPrefix}/hip/joint[@type='right']");
		if (!string.IsNullOrEmpty(hipJointLeft) && !string.IsNullOrEmpty(hipJointRight))
		{
			(_motorControl as SelfBalancedDrive).SetHipJoints(hipJointLeft, hipJointRight);
		}

		var legJointLeft = GetPluginParameters().GetValue<string>($"{parameterPrefix}/leg/joint[@type='left']");
		var legJointRight = GetPluginParameters().GetValue<string>($"{parameterPrefix}/leg/joint[@type='right']");
		if (!string.IsNullOrEmpty(legJointLeft) && !string.IsNullOrEmpty(legJointRight))
		{
			(_motorControl as SelfBalancedDrive).SetLegJoints(legJointLeft, legJointRight);
		}

		var bodyJoint = GetPluginParameters().GetValue<string>($"{parameterPrefix}/body/joint");
		if (!string.IsNullOrEmpty(bodyJoint))
		{
			(_motorControl as SelfBalancedDrive).SetBodyJoint(bodyJoint);
		}

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/body/rotation/hip_adjust"))
		{
			var adjust = GetPluginParameters().GetValue($"{parameterPrefix}/body/rotation/hip_adjust", 1.88);
			(_motorControl as SelfBalancedDrive).AdjustBodyRotation = adjust;
		}

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc"))
		{
			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/param"))
			{
				var kSW = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/K_sw");
				var sigmaB = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/sigma_b");
				var wheelFF = GetPluginParameters().GetValue<double>($"{parameterPrefix}/smc/param/wheel_ff");
				(_motorControl as SelfBalancedDrive).SetSMCParams(kSW, sigmaB, wheelFF);
				StartSummary.AppendLine($"SetBalancedWheel() => kSW: {kSW}, sigmaB: {sigmaB}, wheelFF: {wheelFF}");
			}

			if (GetPluginParameters().IsValidNode($"{parameterPrefix}/smc/state_space"))
			{
				var A = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/A");
				var B = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/B");
				var K = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/K");
				var S = GetPluginParameters().GetValue<string>($"{parameterPrefix}/smc/state_space/S");
				(_motorControl as SelfBalancedDrive).SetSMCNominalModel(A, B, K, S);
			}
		}

		SetDriveForWheel($"{parameterPrefix}/wheel");

		(_motorControl as SelfBalancedDrive).ChangeWheelDriveType();
	}

	private void SetDifferentialDrive(in string parameterPrefix)
	{
		_motorControl = new DifferentialDrive(transform);

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
			StartSummary.AppendLine($"<tread> will be depreacted!! please use <separation>");
		}

		var wheelTread = GetPluginParameters().GetValue<float>($"{parameterPrefix}/tread"); // TODO: to be deprecated
		var wheelSeparation = GetPluginParameters().GetValue($"{parameterPrefix}/separation", wheelTread);

		StartSummary.AppendLine($"wheel separation/radius: {wheelSeparation}/{wheelRadius}");
		_motorControl.SetWheelInfo(wheelRadius, wheelSeparation);

		var wheelLeftName = GetPluginParameters().GetValue($"{parameterPrefix}/location[@type='left']", string.Empty);
		var wheelRightName = GetPluginParameters().GetValue($"{parameterPrefix}/location[@type='right']", string.Empty);

		var wheelRearNameLeft = GetPluginParameters().GetValue($"{parameterPrefix}/location[@type='rear_left']", string.Empty);
		var wheelRearNameRight = GetPluginParameters().GetValue($"{parameterPrefix}/location[@type='rear_right']", string.Empty);

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

 		var integralMin = float.NegativeInfinity;
		var integralMax = float.PositiveInfinity;
		var outputMin = float.NegativeInfinity;
		var outputMax = float.PositiveInfinity;

		if (GetPluginParameters().IsValidNode($"{parameterPrefix}/limit"))
		{
			if (!GetPluginParameters().IsValidNode($"{parameterPrefix}/limit/integral/min") &&
				!GetPluginParameters().IsValidNode($"{parameterPrefix}/limit/integral/max"))
			{
				var limitIntegral = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/integral");
				integralMin = -Math.Abs(limitIntegral);
				integralMax = Math.Abs(limitIntegral);
			}

			if (!GetPluginParameters().IsValidNode($"{parameterPrefix}/limit/integral/min") &&
				!GetPluginParameters().IsValidNode($"{parameterPrefix}/limit/integral/max"))
			{
				var limitOutput = GetPluginParameters().GetValue<float>($"{parameterPrefix}/limit/output");
				outputMin = -Math.Abs(limitOutput);
				outputMax = Math.Abs(limitOutput);
			}

			integralMin = GetPluginParameters().GetValue($"{parameterPrefix}/limit/integral/min", integralMin);
			integralMax = GetPluginParameters().GetValue($"{parameterPrefix}/limit/integral/max", integralMax);
			outputMin = GetPluginParameters().GetValue($"{parameterPrefix}/limit/output/min", outputMin);
			outputMax = GetPluginParameters().GetValue($"{parameterPrefix}/limit/output/max", outputMax);
		}

		SetPID(P, I, D, integralMin, integralMax, outputMin, outputMax);

		StartSummary.AppendLine($"SetMotorPID: {parameterPrefix}, {P}, {I}, {D}, {integralMin}, {integralMax}, {outputMin}, {outputMax}");
	}


	private void SetMowing()
	{
		var targetBladeName = GetPluginParameters().GetAttributeInPath<string>("mowing/blade", "target");

		if (string.IsNullOrEmpty(targetBladeName))
			return;

		SDFormat.Helper.Link targetBlade = null;
		foreach (var linkHelper in _linkHelperInChildren)
			if (linkHelper.name == targetBladeName) { targetBlade = linkHelper; break; }

		if (targetBlade != null)
		{
			var mowingBlade = targetBlade.gameObject.AddComponent<MowingBlade>();

			mowingBlade.HeightMin = GetPluginParameters().GetValue("mowing/blade/height/min", 0f);
			mowingBlade.HeightMax = GetPluginParameters().GetValue("mowing/blade/height/max", 0.1f);
			mowingBlade.RevSpeedMax = GetPluginParameters().GetValue<ushort>("mowing/blade/rev_speed/max", 1000);
			mowingBlade.Height = 0;

			if (_micomCommand != null)
			{
				_micomCommand.SetMowingBlade(mowingBlade);
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
						if (targetBattery.Name.Equals(batteryName))
						{
							// StartSummary.AppendLine("Battery: " + batteryName + ", Battery Consumer:" + consumption.ToString("F5"));
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
		StartSummary.AppendLine("Loaded Static TF Info : " + _modelName);

		if (GetPluginParameters().GetValues<string>("ros2/static_transforms/link", out var staticLinks))
		{
			foreach (var link in staticLinks)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath("ros2/static_transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");

				var (modelName, linkName) = SDF2Unity.GetModelLinkName(link);

				foreach (var linkHelper in _linkHelperInChildren)
				{
					if ((string.IsNullOrEmpty(modelName) || (!string.IsNullOrEmpty(modelName) &&
						linkHelper.Model.name.Equals(modelName))) &&
						linkHelper.name.Equals(linkName))
					{
						var tf = new TF(linkHelper, link, parentFrameId);
						_staticTfList.Add(tf);
						StartSummary.AppendLine(modelName + "::" + linkName + " : Static TF added");
						break;
					}
				}
			}
		}
	}

	private void LoadTF()
	{
		StartSummary.AppendLine("Loaded TF Info : " + _modelName);

		if (GetPluginParameters().GetValues<string>("ros2/transforms/link", out var links))
		{
			foreach (var link in links)
			{
				var parentFrameId = GetPluginParameters().GetAttributeInPath("ros2/transforms/link[text()='" + link + "']", "parent_frame_id", "base_link");
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
						StartSummary.AppendLine(modelName + "::" + linkName + " -> " + parentFrameId + " : TF added");
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

			case "request_bumper_topic_name":
				break;

			default:
				break;
		}
	}
}