/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using Any = cloisim.msgs.Any;
using messages = cloisim.msgs;
using cloisim.Native;
using System.Runtime.InteropServices;
using System;

public class JointControlPlugin : CLOiSimPlugin
{
	private List<TF> tfList = new List<TF>();
	private string _robotDescription = "<?xml version='1.0' ?><sdf></sdf>";
	private SensorDevices.JointCommand _jointCommand = null;
	private SensorDevices.JointState _jointState = null;

	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosJointStatePublisher = IntPtr.Zero;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.JOINTCONTROL;

		_jointState = gameObject.AddComponent<SensorDevices.JointState>();
		_jointCommand = gameObject.AddComponent<SensorDevices.JointCommand>();
		_jointCommand.SetJointState(_jointState);
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterRxDevice(out var portRx, "Rx"))
		{
			AddThread(portRx, ReceiverThread, _jointCommand);
		}

		if (RegisterTxDevice(out var portTx, "Tx"))
		{
			AddThread(portTx, SenderThread, _jointState);
		}

		if (RegisterTxDevice(out var portTf, "Tf"))
		{
			AddThread(portTf, PublishTfThread, tfList);
		}

		LoadJoints();

		_robotDescription = "<?xml version='1.0' ?><sdf>" + GetPluginParameters().ParentRawXml() + "</sdf>";
		// UnityEngine.Debug.Log(_robotDescription);

		// Initialize native ROS2 JointState publisher
		Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_jointcontrol_" + gameObject.name.Replace(" ", "_");
		_rosNode = Ros2NativeWrapper.CreateNode(nodeName);

		var topicName = GetPluginParameters().GetValue<string>("ros2/joint_state_topic", "/joint_states");
		_rosJointStatePublisher = Ros2NativeWrapper.CreateJointStatePublisher(_rosNode, topicName);

		_jointState.OnJointStateDataGenerated += HandleNativeJointStateData;

		yield return null;
	}

	private unsafe void HandleNativeJointStateData(messages.JointStateV msg)
	{
		if (_rosJointStatePublisher == IntPtr.Zero || msg == null) return;

		int count = msg.JointStates.Count;
		if (count == 0) return;

		double timestamp = msg.Header.Stamp.Sec + (msg.Header.Stamp.Nsec * 1e-9);

		var nameArray = new string[count];
		var positionArray = new double[count];
		var velocityArray = new double[count];
		var effortArray = new double[count];

		for (int i = 0; i < count; i++)
		{
			var js = msg.JointStates[i];
			nameArray[i] = js.Name;
			positionArray[i] = js.Position;
			velocityArray[i] = js.Velocity;
			effortArray[i] = js.Effort;
		}

		// Marshal string array
		IntPtr namesPtr = Marshal.AllocHGlobal(count * IntPtr.Size);
		IntPtr[] stringPointers = new IntPtr[count];
		for (int i = 0; i < count; i++)
		{
			stringPointers[i] = Marshal.StringToHGlobalAnsi(nameArray[i] ?? "");
		}
		Marshal.Copy(stringPointers, 0, namesPtr, count);

		// Marshal double arrays
		IntPtr posPtr = Marshal.AllocHGlobal(count * sizeof(double));
		Marshal.Copy(positionArray, 0, posPtr, count);

		IntPtr velPtr = Marshal.AllocHGlobal(count * sizeof(double));
		Marshal.Copy(velocityArray, 0, velPtr, count);

		IntPtr effPtr = Marshal.AllocHGlobal(count * sizeof(double));
		Marshal.Copy(effortArray, 0, effPtr, count);

		try
		{
			var data = new JointStateStruct
			{
				timestamp = timestamp,
				frame_id = "",
				name = namesPtr,
				position = posPtr,
				velocity = velPtr,
				effort = effPtr,
				length = count
			};

			Ros2NativeWrapper.PublishJointState(_rosJointStatePublisher, ref data);
		}
		finally
		{
			// Free unmanaged memory
			for (int i = 0; i < count; i++)
			{
				Marshal.FreeHGlobal(stringPointers[i]);
			}
			Marshal.FreeHGlobal(namesPtr);
			Marshal.FreeHGlobal(posPtr);
			Marshal.FreeHGlobal(velPtr);
			Marshal.FreeHGlobal(effPtr);
		}
	}

	new protected void OnDestroy()
	{
		if (_jointState != null) _jointState.OnJointStateDataGenerated -= HandleNativeJointStateData;
		if (_rosJointStatePublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyJointStatePublisher(_rosJointStatePublisher);
		if (_rosNode != IntPtr.Zero) Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void OnReset()
	{
	}

	private void LoadJoints()
	{
		var updateRate = GetPluginParameters().GetValue<float>("update_rate", 20);
		_jointState.SetUpdateRate(updateRate);

		if (GetPluginParameters().GetValues<string>("joints/joint", out var joints))
		{
			foreach (var jointName in joints)
			{
				// UnityEngine.Debug.Log("Joints loaded "+ jointName);
				if (_jointState.AddTargetJoint(jointName, out var targetLink, out var isStatic))
				{
					var parentFrameId = GetPluginParameters().GetAttributeInPath<string>("joints/joint[text()='" + jointName + "']", "parent_frame_id");
					var jointParentLinkName = (string.IsNullOrEmpty(parentFrameId)) ? targetLink.JointParentLinkName : parentFrameId;
					var tf = new TF(targetLink, targetLink.JointChildLinkName, jointParentLinkName);
					if (isStatic)
					{
						staticTfList.Add(tf);
						// UnityEngine.Debug.LogFormat("staticTfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
					}
					else
					{
						tfList.Add(tf);
						// UnityEngine.Debug.LogFormat("tfList Added: {0}::{1}", targetLink.Model.name, targetLink.name);
					}
				}
			}
		}
		// UnityEngine.Debug.Log("Joints loaded");
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "robot_description":
				SetRobotDescription(ref response);
				break;

			default:
				break;
		}
	}

	private void SetRobotDescription(ref DeviceMessage msRos2Info)
	{
		if (msRos2Info == null)
		{
			return;
		}

		var ros2CommonInfo = new messages.Param();
		ros2CommonInfo.Name = "description";
		ros2CommonInfo.Value = new Any { Type = Any.ValueType.String };
		ros2CommonInfo.Value.StringValue = _robotDescription;

		msRos2Info.SetMessage<messages.Param>(ros2CommonInfo);
	}
}