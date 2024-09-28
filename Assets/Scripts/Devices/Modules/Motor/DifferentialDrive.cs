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
	public enum Location
	{
		NONE,
		LEFT = 1, RIGHT = 2,
		FRONT_LEFT = 1, FRONT_RIGHT = 2,
		REAR_LEFT = 3, REAR_RIGHT = 4
	};

	public class DifferentialDrive
	{
		#region <Motor Related>
		protected Dictionary<Location, Motor> _wheelList = new Dictionary<Location, Motor>()
		{
			{Location.LEFT, null},
			{Location.RIGHT, null},
			{Location.REAR_LEFT, null},
			{Location.REAR_RIGHT, null}
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

		public void SetWheel(in string wheelNameLeft, in string wheelNameRight)
		{
			AttachWheel(Location.LEFT, wheelNameLeft);
			AttachWheel(Location.RIGHT, wheelNameRight);
		}

		public void SetWheel(
			in string frontWheelLeftName, in string frontWheelRightName,
			in string rearWheelLeftName, in string rearWheelRightName)
		{
			SetWheel(frontWheelLeftName, frontWheelRightName);

			AttachWheel(Location.REAR_LEFT, rearWheelLeftName);
			AttachWheel(Location.REAR_RIGHT, rearWheelRightName);
		}

		private void AttachWheel(in Location targetWheelLocation, in string targetWheelName)
		{
			var linkList = _baseTransform.GetComponentsInChildren<SDF.Helper.Link>();
			foreach (var link in linkList)
			{
				if (link.name.Equals(targetWheelName) || link.Model.name.Equals(targetWheelName))
				{
					var motorObject = (link.gameObject != null) ? link.gameObject : link.Model.gameObject;
					_wheelList[targetWheelLocation] = new Motor(motorObject);
					return;
				}
			}
		}

		public void TwistDrive(in float linearVelocity, in float angularVelocity)
		{
			// m/s, rad/s
			// var linearVelocityLeft = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
			// var linearVelocityRight = ((2 * linearVelocity) + (angularVelocity * WheelSeparation)) / (2 * wheelRadius);
			var angularCalculation = (angularVelocity * _odometry.WheelSeparation * 0.5f);

			// Velocity(rad per second) for wheels
			var linearVelocityLeft = linearVelocity - angularCalculation;
			var linearVelocityRight = linearVelocity + angularCalculation;

			var angularVelocityLeft = SDF2Unity.CurveOrientation(linearVelocityLeft * _odometry.InverseWheelRadius);
			var angularVelocityRight = SDF2Unity.CurveOrientation(linearVelocityRight * _odometry.InverseWheelRadius);

			SetMotorVelocity(angularVelocityLeft, angularVelocityRight);
		}

		private void SetMotorVelocity(in float angularVelocityLeft, in float angularVelocityRight)
		{
			foreach (var wheel in _wheelList)
			{
				var motor = wheel.Value;
				if (motor != null)
				{
					if (wheel.Key.Equals(Location.RIGHT) || wheel.Key.Equals(Location.REAR_RIGHT))
					{
						motor.SetTargetVelocity(angularVelocityRight);
					}

					if (wheel.Key.Equals(Location.LEFT) || wheel.Key.Equals(Location.REAR_LEFT))
					{
						motor.SetTargetVelocity(angularVelocityLeft);
					}
				}
			}
		}

		/// <summary>Get target Motor Velocity</summary>
		/// <remarks>radian per second</remarks>
		public float GetCurrentAngularVelocity(in Location location)
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

			var angularVelocityLeft =  GetCurrentAngularVelocity(Location.LEFT);
			var angularVelocityRight =  GetCurrentAngularVelocity(Location.RIGHT);

			return (_odometry != null) ? _odometry.Update(odomMessage, angularVelocityLeft, angularVelocityRight, duration, imuSensor) : false;
		}
	}
}