/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using System;
using UnityEngine;

public abstract class DevicesPlugin : MonoBehaviour
{
	protected PluginParameters parameters;

	private Dictionary<string, DevicePlugin> devicePlugins;

	protected abstract void OnAwake();

	protected abstract void OnStart();

	void Awake()
	{
		parameters = new PluginParameters();
		devicePlugins = new Dictionary<string, DevicePlugin>();

		OnAwake();
	}

	void Start()
	{
		OnStart();
	}

	public void SetPluginParameters(in XmlNode node)
	{
		if (parameters != null)
		{
			parameters.SetRootData(node);
		}
		else
		{
			Debug.LogWarning("Cannot set plugin parameters");
		}
	}

	public DevicePlugin AddDevicePlugin(in string deviceName, in string pluginTypeName)
	{
		var pluginType = Type.GetType(pluginTypeName);
		if (pluginType != null)
		{
			var pluginObject = gameObject.AddComponent(pluginType) as DevicePlugin;
			devicePlugins.Add(deviceName, pluginObject);

			return pluginObject as DevicePlugin;
		}
		else
		{
			return null;
		}
	}
}