/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public abstract class DevicesPlugin : DevicePlugin
{
	private Dictionary<string, DevicePlugin> devicePlugins = new Dictionary<string, DevicePlugin>();

	public void AddDevicePlugin(in string deviceName, in DevicePlugin devicePlugin)
	{
		devicePlugins.Add(deviceName, devicePlugin);
	}
}