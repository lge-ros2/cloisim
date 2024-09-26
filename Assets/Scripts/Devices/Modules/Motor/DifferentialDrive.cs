/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;
using messages = cloisim.msgs;

namespace MotorControl
{
	public class DifferentialDrive
	{
		public enum WheelLocation { NONE, LEFT, RIGHT, REAR_LEFT, REAR_RIGHT };

		#region <Motor Related>
		protected Dictionary<WheelLocation, Motor> _wheelList = new Dictionary<WheelLocation, Motor>()
		{
			{WheelLocation.LEFT, null},
			{WheelLocation.RIGHT, null},
			{WheelLocation.REAR_LEFT, null},
			{WheelLocation.REAR_RIGHT, null}
		};

		private Odometry _odometry = null;

		#endregion

		private Transform _baseTransform = null;

		public DifferentialDrive(in Transform controllerTransform)
		{
			_baseTransform = controllerTransform;
		}

		public virtual void Reset()
		{
			if (_odometry != null)
			{
				_odometry.Reset();
			}

			foreach (var wheel in _wheelList)
			{
				var motor = wheel.Value;
				if (motor != null)
				{
					motor.Reset();
				}
			}
		}

		public virtual void SetWheelInfo(in float radius, in float separation)
		{
			this._odometry = new Odometry(radius, separation);
		}

		private bool IsWheelAttached()
		{
			foreach (var wheel in _wheelList)
			{
				if (wheel.Value != null)
				{
					return true;
				}
			}

			Debug.LogWarning("There is no Wheel, AttachWheel() first");
			return false;
		}

		public void SetPID(
			in float p, in float i, in float d,
			in float integralMin, in float integralMax,
			in float outputMin, in float outputMax)
		{
			if (IsWheelAttached())
			{
				if (!float.IsNaN(p) && !float.IsNaN(i) && !float.IsNaN(d) &&
					!float.IsInfinity(p) && !float.IsInfinity(i) && !float.IsInfinity(d))
				{
					foreach (var wheel in _wheelList)
					{
						wheel.Value?.SetPID(p, i, d, integralMin, integralMax, outputMin, outputMax);
					}
				}
				else
				{
					Debug.LogWarning("One of PID Gain value is NaN or Infinity. Set to default value");
				}
			}
		}

		public void AttachWheel(
			in WheelLocation location,
			in GameObject targetMotorObject)
		{
			_wheelList[location] = new Motor(targetMotorObject);
		}

		/// <summary>Set differential driver</summary>
		/// <remarks>rad per second for wheels</remarks>
		public void SetDifferentialDrive(in float linearVelocityLeft, in float linearVelocityRight)
		{
			var angularVelocityLeft = SDF2Unity.CurveOrientation(linearVelocityLeft * _odometry.InverseWheelRadius);
			var angularVelocityRight = SDF2Unity.CurveOrientation(linearVelocityRight * _odometry.InverseWheelRadius);
			SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
		}

		public void SetTwistDrive(in float linearVelocity, in float angularVelocity)
		{
			// m/s, rad/s
			// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
			// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
			var angularCalculation = (angularVelocity * _odometry.WheelSeparation * 0.5f);

			var linearVelocityLeft = linearVelocity - angularCalculation;
			var linearVelocityRight = linearVelocity + angularCalculation;
			SetDifferentialDrive(linearVelocityLeft, linearVelocityRight);
		}

		private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
		{
			foreach (var wheel in _wheelList)
			{
				var motor = wheel.Value;
				if (motor != null)
				{
					if (wheel.Key.Equals(WheelLocation.RIGHT) || wheel.Key.Equals(WheelLocation.REAR_RIGHT))
					{
						motor.SetTargetVelocity(angularVelocityRight);
					}

					if (wheel.Key.Equals(WheelLocation.LEFT) || wheel.Key.Equals(WheelLocation.REAR_LEFT))
					{
						motor.SetTargetVelocity(angularVelocityLeft);
					}
				}
			}
		}

		/// <summary>Get target Motor Velocity</summary>
		/// <remarks>radian per second</remarks>
		public float GetCurrentAngularVelocity(in WheelLocation location)
		{
			var motor = _wheelList[location];
			if (motor != null)
			{
				// Debug.Log(location.ToString() + " => " + angularVelocity.ToString("F8"));
				return motor.GetCurrentAngularVelocity();
			}

			return float.NaN;
		}

		public virtual bool Update(messages.Micom.Odometry odomMessage, in float duration, SensorDevices.IMU imuSensor = null)
		{
			foreach (var wheel in _wheelList)
			{
				var motor = wheel.Value;
				if (motor != null)
				{
					motor.Update(duration);
				}
			}

			var angularVelocityLeft =  GetCurrentAngularVelocity(DifferentialDrive.WheelLocation.LEFT);
			var angularVelocityRight =  GetCurrentAngularVelocity(DifferentialDrive.WheelLocation.RIGHT);

			return (_odometry != null) ? _odometry.Update(odomMessage, angularVelocityLeft, angularVelocityRight, duration, imuSensor) : false;
		}
	}
}