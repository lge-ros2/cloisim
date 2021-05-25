/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using UnityEngine;

public class ActorControlPlugin : CLOiSimPlugin
{
	public static Dictionary<string, List<SDF.Helper.Model>> StaticModelList = new Dictionary<string, List<SDF.Helper.Model>>();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.ACTOR;
		partName = "ActorControlPlugin";
	}

	protected override void OnStart()
	{
		RegisterServiceDevice("Control");
		AddThread(RequestThread);
	}
}