/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class MicomCommand : Device
	{
		private MotorControl.DifferentialDrive _motorControl = null;
		private MowingBlade _mowingBlade = null;

		protected override void OnAwake()
		{
			Mode = ModeType.RX_THREAD;
			DeviceName = "MicomCommand";
		}

		protected override void OnStart()
		{
			_mowingBlade = GetComponentInChildren<MowingBlade>();
		}

		protected override void OnReset()
		{
			DoWheelDrive(Vector3.zero, Vector3.zero);
		}

		public void SetMotorControl(in MotorControl.DifferentialDrive motorControl)
		{
			this._motorControl = motorControl;
		}

		protected override void ProcessDevice()
		{
			if (PopDeviceMessage(out var receivedMessage))
			{
				var cmdVelocity = receivedMessage.GetMessage<messages.Twist>();

				if (cmdVelocity != null)
				{
					var linear = cmdVelocity.Linear;
					var angular = cmdVelocity.Angular;

					var linearVelocity = SDF2Unity.Position(linear.X, linear.Y, linear.Z);
					var angularVelocity = SDF2Unity.Position(angular.X, angular.Y, angular.Z);

					DoWheelDrive(linearVelocity, angularVelocity);
				}
				else
				{
					var cmdMowing = receivedMessage.GetMessage<messages.Param>();

					if (cmdMowing != null)
					{
						if (!string.IsNullOrEmpty(cmdMowing.Name))
						{
							ControlMowing(cmdMowing.Name, cmdMowing.Value);
						}
					}
#if UNITY_EDITOR
					else
					{
						Debug.LogWarning("ERROR: failed to pop device message");
					}
#endif
				}
			}
		}

		/// <param name="linearVelocity">m/s</param>
		/// <param name="angularVelocity">rad/s</param>
		private void DoWheelDrive(in Vector3 linearVelocity, in Vector3 angularVelocity)
		{
			if (_motorControl == null)
			{
				Debug.LogWarning("micom device for wheel drive is not ready!!");
				return;
			}

			var targetLinearVelocity = linearVelocity.z;
			var targetAngularVelocity = angularVelocity.y;

			_motorControl.SetTwistDrive(targetLinearVelocity, targetAngularVelocity);
		}

		private void ControlMowing(in string target, in cloisim.msgs.Any value)
		{
			if (string.Compare(target, "mowing_blade_height") == 0)
			{
				if (_mowingBlade != null && value.Type == messages.Any.ValueType.Double)
				{
					_mowingBlade.Height = (float)value.DoubleValue;
					// Debug.Log($"mowing_blade_height {value} -> {_mowingBlade.Height}");
				}
			}
			else if (string.Compare(target, "mowing_blade_rev_speed") == 0)
			{
				if (_mowingBlade != null && value.Type == messages.Any.ValueType.Int32)
				{
					_mowingBlade.RevSpeed = System.Convert.ToUInt16(value.IntValue);
					// Debug.Log($"mowing_blade_rev_speed {value} -> {_mowingBlade.RevSpeed}");
				}
			}
			else
			{
				Debug.LogWarning($"Invalid Control Mowing message received: {target}");
			}
		}
	}
}