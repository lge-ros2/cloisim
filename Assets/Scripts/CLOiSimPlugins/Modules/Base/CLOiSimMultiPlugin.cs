/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

public abstract class CLOiSimMultiPlugin : CLOiSimPlugin
{
	private Dictionary<string, CLOiSimPlugin> _CLOiSimPlugins = new Dictionary<string, CLOiSimPlugin>();

	public void AddPlugin(in string deviceName, in CLOiSimPlugin CLOiSimPlugin)
	{
		_CLOiSimPlugins.Add(deviceName, CLOiSimPlugin);
	}
}