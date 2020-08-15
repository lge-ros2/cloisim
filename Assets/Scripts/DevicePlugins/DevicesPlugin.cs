/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using UnityEngine;

public abstract class DevicesPlugin : MonoBehaviour, IDevicePlugin
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

	public void Reset() { }

	public void AddDevicePlugin(in string deviceName, in DevicePlugin devicePlugin)
	{
		devicePlugins.Add(deviceName, devicePlugin);
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
}