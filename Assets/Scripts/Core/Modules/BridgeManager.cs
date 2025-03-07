/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 *
 *
 * Description for deviceMapTable
 * ModelName, CLOiSimPluginType, Devicename, topic : portnumber
 *
 *	{
 *		"ModelName":
 *		{
 *			"CLOiSimPluginType":
 *				{
 *					"PartsName":
 *					{
 *						"topic_name": 12345
 *					}
 *				}
 *			},
 *			...
 *		}
 *	}
 */

using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System;

public class BridgeManager : IDisposable
{
	private const ushort MinPortRange = 49152;
	private const ushort MaxPortRange = IPEndPoint.MaxPort;
	private static StringBuilder sbLogs = new StringBuilder();

	private static Dictionary<string, ushort> haskKeyPortMapTable = new Dictionary<string, ushort>();
	private static Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> deviceMapTable = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>>();

	private static IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

	public BridgeManager()
	{
		ClearLog();
	}

	~BridgeManager()
	{
		Dispose();
	}

	public void Dispose()
	{
		ClearLog();
		GC.SuppressFinalize(this);
	}

	private static void RemoveDevice(in ushort devicePort)
	{
		foreach (var deviceMap in deviceMapTable.ToList())
		{
			var deviceMapValue = deviceMap.Value;
			foreach (var partMaps in deviceMapValue.ToList())
			{
				var partMapsValue = partMaps.Value;
				foreach (var portMaps in partMapsValue.ToList())
				{
					var portMapsValue = portMaps.Value;
					foreach (var portMap in portMapsValue.ToList())
					{
						if (portMap.Value == devicePort)
							portMapsValue.Remove(portMap.Key);
					}

					if (portMapsValue.Count == 0)
						partMapsValue.Remove(portMaps.Key);
				}

				if (partMapsValue.Count == 0)
					deviceMapValue.Remove(partMaps.Key);
			}

			if (deviceMapValue.Count == 0)
				deviceMapTable.Remove(deviceMap.Key);
		}
	}

	public static void DeallocateDevice(in List<ushort> devicePorts, in List<string> hashKeys)
	{
		lock (deviceMapTable)
		{
			foreach (var devicePort in devicePorts)
			{
				RemoveDevice(devicePort);
			}
		}

		DeallocateDevicePort(hashKeys);
	}

	public static void DeallocateDevicePort(in List<string> hashKeys)
	{
		lock (haskKeyPortMapTable)
		{
			var isRemoved = false;
			foreach (var hashKey in hashKeys)
			{
				isRemoved = haskKeyPortMapTable.Remove(hashKey);

				if (!isRemoved)
				{
					Console.Error.Write("Failed to remove HashKey({0})!!!!", hashKey);
				}
				else
				{
					Console.Write("HashKey({0}) Removed.", hashKey);
				}
			}
		}
	}

	public static ushort SearchSensorPort(in string hashKey)
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

	public static bool IsAvailablePort(in ushort port)
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

	private static string MakeHashKey(params string[] data)
	{
		return string.Join("", data);
	}

	public static bool AllocateDevice(
		in string deviceType, in string modelName, in string partsName, in string subPartsNameAndKey,
		out string hashKey, out ushort port)
	{
		hashKey = MakeHashKey(modelName, partsName, subPartsNameAndKey);

		if (string.IsNullOrEmpty(hashKey))
		{
			Console.Error.Write("Impossible empty hashKey");
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
						portsMapTable.Add(subPartsNameAndKey, port);
					}
					else
					{
						var newPortsMapTable = new Dictionary<string, ushort>();
						newPortsMapTable.Add(subPartsNameAndKey, port);
						partsMapTable.Add(partsName, newPortsMapTable);
					}
				}
				else
				{
					var portsMapTable = new Dictionary<string, ushort>();
					portsMapTable.Add(subPartsNameAndKey, port);
					var newPartsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
					newPartsMapTable.Add(partsName, portsMapTable);

					devicesTypeMapTable.Add(deviceType, newPartsMapTable);
				}
			}
			else
			{
				var portsMapTable = new Dictionary<string, ushort>();
				portsMapTable.Add(subPartsNameAndKey, port);

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

	public static ushort AllocateDevicePort(in string hashKey)
	{
		// check if already occupied
		var newPort = SearchSensorPort(hashKey);

		if (newPort > 0)
		{
			var errorMessage = string.Format("HashKey({0}) is already occupied.", hashKey);
			Console.Error.Write(errorMessage);
			Main.UIController?.SetErrorMessage(errorMessage);
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
		sbLogs.AppendLine();

		return newPort;
	}

	public void PrintLog()
	{
		Console.Write(sbLogs);
	}

	public void ClearLog()
	{
		sbLogs.Clear();
		sbLogs.AppendLine("<Allocated information in BridgeManager>");
	}
}