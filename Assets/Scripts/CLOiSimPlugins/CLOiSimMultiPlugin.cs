/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

public abstract class CLOiSimMultiPlugin : CLOiSimPlugin
{
	private Dictionary<string, CLOiSimPlugin> CLOiSimPlugins = new Dictionary<string, CLOiSimPlugin>();

	public void AddCLOiSimPlugin(in string deviceName, in CLOiSimPlugin CLOiSimPlugin)
	{
		CLOiSimPlugins.Add(deviceName, CLOiSimPlugin);
	}
}