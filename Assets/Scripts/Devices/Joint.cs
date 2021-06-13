/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

namespace SensorDevices
{
	public class Joint : Device
	{

		private JointCommand jointCommand = null;
		private JointState jointState = null;

		protected override void OnAwake()
		{
			Mode = ModeType.NONE;
			DeviceName = "Joint";
		}

		protected override void OnStart()
		{
		}

		protected override void OnReset()
		{
		}

		public JointCommand GetCommand()
		{
			if (jointCommand == null)
			{
				jointCommand = gameObject.AddComponent<JointCommand>();
			}

			return jointCommand;
		}

		public JointState GetState()
		{
			if (jointState == null)
			{
				jointState = gameObject.AddComponent<JointState>();
			}

			return jointState;
		}
	}
}