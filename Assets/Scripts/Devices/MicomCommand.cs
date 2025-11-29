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
		private MotorControl _motorControl = null;
		private MowingBlade _mowingBlade = null;

		#region Constant for SelfBalancedDrive
		private const float RollRotationUnit = 0.25f; // deg
		private const float PitchRotationUnit = 0.01f; // rad
		private const float HeightMovementUnit = 0.5f; // deg
		#endregion

		#region Constant mapping index for JoyStick
		private const int JoyUpKeyIndex = 11;
		private const int JoyDownKeyIndex = 12;
		private const int JoyLeftKeyIndex = 13;
		private const int JoyRightKeyIndex = 14;
		private const int JoyTriangleKeyIndex = 3;
		private const int JoyCircleKeyIndex = 1;
		#endregion

		protected override void OnAwake()
		{
			Mode = ModeType.RX_THREAD;
			DeviceName = "MicomCommand";
		}

		protected override void OnReset()
		{
			DoWheelDrive(Vector3.zero, Vector3.zero);
		}

		public void SetMotorControl(in dynamic motorControl)
		{
			this._motorControl = motorControl;
		}

		public void SetMowingBlade(in dynamic mowingBlade)
		{
			this._mowingBlade = mowingBlade;
		}

		protected override void ProcessDevice()
		{
			if (PopDeviceMessage(out var receivedMessage))
			{
				var cmdVelocity = receivedMessage.GetMessage<messages.Twist>();

				if (cmdVelocity != null)
				{
					var linearVelocity = SDF2Unity.Position(cmdVelocity.Linear);
					var angularVelocity = SDF2Unity.Position(cmdVelocity.Angular);

					DoWheelDrive(linearVelocity, angularVelocity);
				}
				else
				{
					var joystick = receivedMessage.GetMessage<messages.Joystick>();
					if (joystick != null)
					{
						ControlJoystick(joystick);
					}
					else
					{
						var customCmd = receivedMessage.GetMessage<messages.Param>();
						if (customCmd != null)
						{
							if (customCmd.Name.StartsWith("mowing"))
							{
								ControlMowing(customCmd.Name, customCmd.Value);
							}
							else if (customCmd.Name.StartsWith("display"))
							{
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

			_motorControl?.Drive(targetLinearVelocity, targetAngularVelocity);
		}

		/// <summary>
		/// Control robot by joystick command based on PS5 controller
		/// </summary>
		/// <param name="message">Joystick message</param>
		/// <remarks>
		/// Currently supported buttons are:
		/// - Triangle button: start balancing
		/// </remarks>
		private void ControlJoystick(in cloisim.msgs.Joystick message)
		{
			var balancedDrive = _motorControl as SelfBalancedDrive;
			if (balancedDrive != null)
			{
#if false
				var tmp = new System.Text.StringBuilder();
				tmp.Clear();
				foreach (var item in message.Buttons)
					tmp.Append($"{item},");
				Debug.Log(tmp.ToString());
#endif

				var buttonUpPressed = message.Buttons[JoyUpKeyIndex]; // Up Button
				var buttonDownPressed = message.Buttons[JoyDownKeyIndex]; // Down Button

				if (buttonUpPressed > 0 || buttonDownPressed > 0)
				{
					var heightAmount = ((buttonUpPressed > 0) ? -HeightMovementUnit : HeightMovementUnit);
					balancedDrive.HeightTarget += heightAmount;
				}

				var buttonLeftPressed = message.Buttons[JoyLeftKeyIndex]; // Left Button
				var buttonRightPressed = message.Buttons[JoyRightKeyIndex]; // Right Button

				if (buttonLeftPressed > 0 || buttonRightPressed > 0)
				{
					var rollAmount = ((buttonLeftPressed > 0) ? -RollRotationUnit : RollRotationUnit);
					balancedDrive.RollTarget += rollAmount;
				}

				var buttonTrianglePressed = message.Buttons[JoyTriangleKeyIndex];
				if (buttonTrianglePressed > 0)
				{
					balancedDrive.Balancing = !balancedDrive.Balancing;
				}

				var buttonCirclePressed = message.Buttons[JoyCircleKeyIndex];
				if (buttonCirclePressed > 0)
				{
					balancedDrive.DoResetPose();
				}

				if (message.Translation != null)
				{
					var stickTranslation = SDF2Unity.Position(message.Translation);
					if (Mathf.Abs(stickTranslation.y) > float.Epsilon)
					{
						var headsetTarget = Mathf.Abs(stickTranslation.y) *
							((stickTranslation.y >= 0) ? balancedDrive.HeadsetTargetMin : balancedDrive.HeadsetTargetMax);
						balancedDrive.HeadsetTarget = headsetTarget;
					}
				}

				if (message.Rotation != null)
				{
					var stickRotation = SDF2Unity.Rotation(message.Rotation);
					// Debug.Log(stickRotation.ToString("F5"));
					if (Mathf.Abs(stickRotation.x) > float.Epsilon)
					{
						balancedDrive.PitchTarget += PitchRotationUnit * SDF2Unity.CurveOrientationAngle(stickRotation.x);
						// Debug.Log($"Joy-PitchTarget={balancedDrive.PitchTarget}");
					}

					// if (Mathf.Abs(stickRotation.z) > float.Epsilon)
					// {
					// }
				}
			}
		}

		private void ControlMowing(in string target, in cloisim.msgs.Any value)
		{
			if (target.Equals("mowing_blade_height"))
			{
				if (_mowingBlade != null && value.Type == messages.Any.ValueType.Double)
				{
					_mowingBlade.Height = (float)value.DoubleValue;
					// Debug.Log($"mowing_blade_height {value} -> {_mowingBlade.Height}");
				}
			}
			else if (target.Equals("mowing_blade_rev_speed"))
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

#if UNITY_EDITOR
		#region Constant for SelfBalancedDrive
		private const float HeadsetRotationUnit = 1f; // deg
		private const float RollRotationUnitKeyboard = 0.15f; // deg
		private const float HeightMovementUnitKeyboard = 0.15f; // deg
		#endregion

		void LateUpdate()
		{
			var balancedDrive = _motorControl as SelfBalancedDrive;
			if (balancedDrive != null)
			{
				if (Input.GetKey(KeyCode.H))
				{
					if (Input.GetKey(KeyCode.UpArrow))
					{
						balancedDrive.HeadsetTarget -= HeadsetRotationUnit;
					}
					else if (Input.GetKey(KeyCode.DownArrow))
					{
						balancedDrive.HeadsetTarget += HeadsetRotationUnit;
					}
					// Debug.Log(balancedDrive.HeadsetTarget);
				}
				else if (Input.GetKey(KeyCode.P))
				{
					if (Input.GetKey(KeyCode.UpArrow))
					{
						balancedDrive.PitchTarget += PitchRotationUnit;
					}
					else if (Input.GetKey(KeyCode.DownArrow))
					{
						balancedDrive.PitchTarget -= PitchRotationUnit;
					}
					// Debug.Log($"PitchTarget={balancedDrive.PitchTarget}");
				}
				else if (Input.GetKey(KeyCode.M))
				{
					if (Input.GetKey(KeyCode.LeftArrow))
					{
						balancedDrive.RollTarget -= RollRotationUnitKeyboard;
					}
					else if (Input.GetKey(KeyCode.RightArrow))
					{
						balancedDrive.RollTarget += RollRotationUnitKeyboard;
					}
					// Debug.Log($"RollTarget={balancedDrive.RollTarget}");

					if (Input.GetKey(KeyCode.UpArrow))
					{
						balancedDrive.HeightTarget -= HeightMovementUnitKeyboard;
					}
					else if (Input.GetKey(KeyCode.DownArrow))
					{
						balancedDrive.HeightTarget += HeightMovementUnitKeyboard;
					}
					// Debug.Log($"HeightTarget={balancedDrive.HeightTarget}");
				}
				else if (Input.GetKeyUp(KeyCode.B))
				{
					balancedDrive.Balancing = !balancedDrive.Balancing;
					Debug.LogWarning($"{name}::Toggle Balancing [{balancedDrive.Balancing}] ");
				}
			}
		}
#endif
	}
}