/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using UnityEngine;

[DefaultExecutionOrder(35)]
public class BridgeManager
{
	private const ushort MinPortRange = 49152;
	private const ushort MaxPortRange = IPEndPoint.MaxPort;
	private SimulationDisplay simulationDisplay = null;
	private StringBuilder sbLogs = new StringBuilder();

	private Dictionary<string, ushort> haskKeyPortMapTable = new Dictionary<string, ushort>();

	/*
	 * ModelName, CLOiSimPluginType, Devicename, topic : portnumber
	 *
	 * {
	 * 	"ModelName":
	 *	{
	 * 		"CLOiSimPluginType":
	 * 		{
	 * 			"PartsName":
	 * 			{
	 * 				"topic_name": 12345
	 * 			}
	 * 		}
	 * 	},
	 * 	...
	 * }
	 */
	private Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> deviceMapTable = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>>();

	private IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

	void Awake()
	{
		var UIRoot = Main.UIObject;
		simulationDisplay = UIRoot.GetComponentInChildren<SimulationDisplay>();
		ClearLog();
	}

	public void DeallocateDevice(in string hashKey)
	{
		var isRemoved = false;

		lock (haskKeyPortMapTable)
		{
			isRemoved = haskKeyPortMapTable.Remove(hashKey);
		}

		if (!isRemoved)
		{
			Debug.LogWarningFormat("Failed to remove HashKey({0})!!!!", hashKey);
		}
		else
		{
			// Debug.LogFormat("HashKey({0}) Removed.", hashKey);
		}
	}

	public ushort SearchSensorPort(in string hashKey)
	{
		lock (haskKeyPortMapTable)
		{
			if (haskKeyPortMapTable.TryGetValue(hashKey, out var port))
			{
				return port;
			}
		}

		return 0;
	}

	public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> GetDeviceMapList(string filter = "")
	{
		lock (deviceMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return deviceMapTable;
			}
			else
			{
				return deviceMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(p => p.Key, p => p.Value);
			}
		}
	}

	public Dictionary<string, ushort> GetDevicePortList(string filter = "")
	{
		lock (haskKeyPortMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return haskKeyPortMapTable;
			}
			else
			{
				return haskKeyPortMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(p => p.Key, p => p.Value);
			}
		}
	}

	public bool IsAvailablePort(in ushort port)
	{
		if (properties != null)
		{
			var connections = properties.GetActiveTcpConnections();
			foreach (var connection in connections)
			{
				// Debug.Log("TCP conn Local: " + connection.LocalEndPoint.Port);
				// Debug.Log("TCP conn Remote: " + connection.RemoteEndPoint.Port);
				if (connection.LocalEndPoint.Port == port)
				{
					return false;
				}
			}
		}

		return true;
	}

	private static string MakeHashKey(in string modelName, in string partsName, in string subPartName)
	{
		return modelName + partsName + subPartName;
	}

	public bool AllocateDevice(in string deviceType, in string modelName, in string partsName, in string subPartName, out string hashKey, out ushort port)
	{
		hashKey = BridgeManager.MakeHashKey(modelName, partsName, subPartName);

		if (string.IsNullOrEmpty(hashKey))
		{
			Debug.LogError("Impossible empty hashKey");
			port = 0;
			return false;
		}

		port = AllocateDevicePort(hashKey);

		if (port > 0)
		{
			if (deviceMapTable.TryGetValue(modelName, out var devicesTypeMapTable))
			{
				if (devicesTypeMapTable.TryGetValue(deviceType, out var partsMapTable))
				{
					if (partsMapTable.TryGetValue(partsName, out var portsMapTable))
					{
						portsMapTable.Add(subPartName, port);
					}
					else
					{
						var newPortsMapTable = new Dictionary<string, ushort>();
						newPortsMapTable.Add(subPartName, port);
						partsMapTable.Add(partsName, newPortsMapTable);
					}
				}
				else
				{
					var portsMapTable = new Dictionary<string, ushort>();
					portsMapTable.Add(subPartName, port);
					var newPartsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
					newPartsMapTable.Add(partsName, portsMapTable);

					devicesTypeMapTable.Add(deviceType, newPartsMapTable);
				}
			}
			else
			{
				var portsMapTable = new Dictionary<string, ushort>();
				portsMapTable.Add(subPartName, port);
				var partsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
				partsMapTable.Add(partsName, portsMapTable);
				var devicesTypeMap = new Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>();
				devicesTypeMap.Add(deviceType, partsMapTable);

				deviceMapTable.Add(modelName, devicesTypeMap);
			}

			return true;
		}

		return false;
	}

	public ushort AllocateDevicePort(in string hashKey)
	{
		// check if already occupied
		var newPort = SearchSensorPort(hashKey);

		if (newPort > 0)
		{
			var errorMessage = string.Format("HashKey({0}) is already occupied.", hashKey);
			Debug.Log(errorMessage);
			simulationDisplay?.SetErrorMessage(errorMessage);
			return 0;
		}

		// find available port number and start with minimum port range
		for (var index = 0; index < (MaxPortRange - MinPortRange); index++)
		{
			var port = (ushort)(MinPortRange + index);
			var isContained = false;

			lock (haskKeyPortMapTable)
			{
				isContained = haskKeyPortMapTable.ContainsValue(port);
			}

			// check if already binded
			if (!isContained && IsAvailablePort(port))
			{
				newPort = port;
				break;
			}
		}

		if (newPort > 0)
		{
			lock (haskKeyPortMapTable)
			{
				haskKeyPortMapTable.Add(hashKey, newPort);
			}

			sbLogs.AppendFormat("Allocated for HashKey({0}) Port({1})", hashKey, newPort);
		}
		else
		{
			sbLogs.AppendFormat("Failed to allocate port for HashKey({0}).", hashKey);
		}
		sbLogs.AppendLine("");

		return newPort;
	}

	public void PrintLog()
	{
		Debug.Log(sbLogs);
	}

	public void ClearLog()
	{
		sbLogs.Clear();
		sbLogs.AppendLine("<Allocated information in BridgeManager>");
	}
}