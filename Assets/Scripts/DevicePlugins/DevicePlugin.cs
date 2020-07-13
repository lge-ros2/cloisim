/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System;
using UnityEngine;

public abstract class DevicePlugin : DeviceTransporter
{
	public string modelName = String.Empty;
	public string partName = string.Empty;

	private XmlNode pluginData;

	private BridgePortManager bridgePortManager = null;

	private List<Thread> threadList = null;

	protected DevicePlugin()
	{
		threadList = new List<Thread>();
	}

	protected abstract void OnAwake();
	protected abstract void OnStart();
	protected virtual void OnReset() { }

	void OnDestroy()
	{
		// Debug.Log("OnDestroy - abort thread" + name);
		if (threadList != null)
		{
			foreach (var thread in threadList)
			{
				if (thread != null && thread.IsAlive)
					thread.Abort();
			}
		}
	}

	protected bool AddThread(in ThreadStart function)
	{
		if (threadList != null && function != null)
		{
			threadList.Add(new Thread(function));
			// thread.Priority = System.Threading.ThreadPriority.AboveNormal;
			return true;
		}

		return false;
	}

	private void StartThreads()
	{
		if (threadList != null)
		{
			foreach (var thread in threadList)
			{
				if (thread != null && !thread.IsAlive)
					thread.Start();
			}
		}
	}

	public void SetPluginData(XmlNode node)
	{
		pluginData = node.SelectSingleNode(".");
	}

	private static T XmlNodeToValue<T>(XmlNode node)
	{
		if (node == null)
		{
			return default(T);
		}
		var value = node.InnerXml.Trim();
		return SDF.Entity.ConvertValueType<T>(value);
	}

	protected T GetPluginAttribute<T>(in string xpath, in string attributeName, in T defaultValue = default(T))
	{
		var node = pluginData.SelectSingleNode(xpath);
		if (node != null)
		{
			var attributes = node.Attributes;
			var attributeNode = attributes[attributeName];
			if (attributeNode != null)
			{
				var attributeValue = attributeNode.Value;
				return SDF.Entity.ConvertValueType<T>(attributeValue);
			}
		}

		return defaultValue;
	}

	protected T GetPluginValue<T>(in string xpath, T defaultValue = default(T))
	{
		if (string.IsNullOrEmpty(xpath) || pluginData == null)
		{
			return defaultValue;
		}

		try
		{
			var node = pluginData.SelectSingleNode(xpath);
			return XmlNodeToValue<T>(node);
		}
		catch (XmlException ex)
		{
			Debug.LogErrorFormat("ERROR: GetPluginValue with {0} : {1} ", xpath, ex.Message);
			return defaultValue;
		}
	}

	protected bool GetPluginValues<T>(in string xpath, out List<T> valueList)
	{
		valueList = null;

		var result = GetPluginValues(xpath, out var nodeList);
		valueList = nodeList.ConvertAll(s => XmlNodeToValue<T>(s));

		return result;
	}

	protected bool GetPluginValues(in string xpath, out List<XmlNode> valueList)
	{
		valueList = null;

		if (string.IsNullOrEmpty(xpath) || pluginData == null)
		{
			return false;
		}

		try
		{
			valueList = new List<XmlNode>(pluginData.SelectNodes(xpath).Cast<XmlNode>());
			if (valueList == null)
			{
				return false;
			}

			return true;
		}
		catch (XmlException ex)
		{
			Debug.LogErrorFormat("ERROR: GetPluginValue with {0} : {1} ", xpath, ex.Message);
			return false;
		}
	}

	protected void PrintPluginData()
	{
		if (pluginData != null)
		{
			Debug.LogWarning("Plugin Data is empty");
		}
		else
		{
			// Print all SDF contents
			StringWriter sw = new StringWriter();
			XmlTextWriter xw = new XmlTextWriter(sw);
			pluginData.WriteTo(xw);
			Debug.Log(sw.ToString());
		}
	}

	protected bool PrepareDevice(in string hashKey, out ushort port, out ulong hash)
	{
		port = bridgePortManager.AllocateSensorPort(hashKey);
		hash = DeviceHelper.GetStringHashCode(hashKey);

		if (port == 0)
		{
			Debug.LogError("Port for device is not allocated!!!!!!!!");
			return false;
		}
		// Debug.LogFormat("PrepareDevice - port({0}) hash({1})", port, hash);

		return true;
	}

	protected string MakeHashKey(in string partName = "", string subPartName = "")
	{
		return modelName + partName + subPartName;
	}

	protected bool RegisterTxDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out ushort port, out ulong hash))
		{
			SetHashForSend(hash);
			InitializePublisher(port);
			return true;
		}

		return false;
	}

	protected bool RegisterRxDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out ushort port, out ulong hash))
		{
			SetHashForReceive(hash);
			InitializeSubscriber(port);
			return 	true;
		}

		return true;
	}

	protected bool RegisterServiceDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out ushort port, out ulong hash))
		{
			SetHashForReceive(hash);
			InitializeResponsor(port);

			return 	true;
		}

		return true;
	}

	protected bool RegisterClientDevice(in string hashKey)
	{
		if (PrepareDevice(hashKey, out ushort port, out ulong hash))
		{
			SetHashForSend(hash);
			InitializeRequester(port);
			return 	true;
		}

		return true;
	}

	void Awake()
	{
		var coreObject = GameObject.Find("Core");

		if (coreObject == null)
		{
			Debug.LogError("Failed to Find 'Core'!!!!");
		}
		else
		{
			bridgePortManager = coreObject.GetComponent<BridgePortManager>();
			if (bridgePortManager == null)
			{
				Debug.LogError("Failed to get 'bridgePortManager'!!!!");
			}

			if (string.IsNullOrEmpty(modelName))
			{
				modelName = DeviceHelper.GetModelName(gameObject);
			}
		}

		OnAwake();
	}

	// Start is called before the first frame update
	void Start()
	{
		// PrintPluginData();

		OnStart();

		StartThreads();
	}

	public void Reset() {
		OnReset();
	}
}