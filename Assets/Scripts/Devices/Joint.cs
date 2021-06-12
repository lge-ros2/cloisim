/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public class Joint : Device
{
	private Dictionary<string, ArticulationBody> jointBodyTable = new Dictionary<string, ArticulationBody>();

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

	public bool AddTarget(in string linkName)
	{
		var childArticulationBodies = gameObject.GetComponentsInChildren<ArticulationBody>();

		foreach (var childArticulatinoBody in childArticulationBodies)
		{
			if (childArticulatinoBody.name.Equals(linkName))
			{
				jointBodyTable.Add(linkName, childArticulatinoBody);
				return true;
			}
		}

		return false;
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