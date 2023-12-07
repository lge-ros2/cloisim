/*
 * Copyright (c) 2023 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public class Battery
	{
		private string name;
		private float maxVoltage = 0;
		private float minVoltage = 0;
		private float minThresholdRate = 0.1f; // 10%
		private float currentVoltage = 0;

		private float consumeVoltage = 0;

		public string Name => name;
		public float CurrentVoltage => currentVoltage;

		public Battery(in string name = "")
		{
			this.name = name;
		}

		public void SetMax(in float startVoltage = 0)
		{
			this.maxVoltage = startVoltage;
			this.minVoltage = startVoltage * minThresholdRate;
			this.currentVoltage = maxVoltage;
		}

		public void Discharge(in float value)
		{
			this.consumeVoltage = (value <= 0) ? value : -value;
		}

		public void Charge(in float value)
		{
			this.consumeVoltage = (value <= 0) ? value : -value;
		}

		private float elapsedTime = 0;
		public float Update(in float deltaTime)
		{
			elapsedTime += deltaTime;
			if (elapsedTime > 1)
			{
				currentVoltage += consumeVoltage;
				currentVoltage = Mathf.Clamp(currentVoltage, minVoltage, maxVoltage);
				elapsedTime = 0;
			}

			return currentVoltage;
		}
	}
}