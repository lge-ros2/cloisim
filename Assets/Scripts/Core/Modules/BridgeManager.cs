/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
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
	private static StringBuilder _sbAllocatedHistory = new();
	private static StringBuilder _sbDeallocatedLogs = new();

	private static Dictionary<string, ushort> _haskKeyPortMapTable = new Dictionary<string, ushort>();
	private static Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> _deviceMapTable = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>>();
	private static IPGlobalProperties _properties = IPGlobalProperties.GetIPGlobalProperties();

	public BridgeManager()
	{
		ClearAllocatedHistory();
	}

	~BridgeManager()
	{
		Dispose();
	}

	public void Dispose()
	{
		GC.SuppressFinalize(this);
	}

	private static void RemoveDevice(in ushort devicePort)
	{
		lock (_deviceMapTable)
		{
			foreach (var deviceMap in _deviceMapTable.ToList())
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
					_deviceMapTable.Remove(deviceMap.Key);
			}
		}
	}

	public static void DeallocateDevice(in List<ushort> devicePorts, in List<string> hashKeys)
	{
		lock (_deviceMapTable)
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
		_sbDeallocatedLogs.Clear();
		_sbDeallocatedLogs.AppendLine("HashKey Removed list");
		lock (_haskKeyPortMapTable)
		{
			var isRemoved = false;
			foreach (var hashKey in hashKeys)
			{
				isRemoved = _haskKeyPortMapTable.Remove(hashKey);

				if (!isRemoved)
				{
					Console.Error.Write($"Failed to remove HashKey({hashKey})!!!!");
				}
				else
				{
					_sbDeallocatedLogs.AppendLine($"- {hashKey}");
				}
			}
		}
		Console.Write(_sbDeallocatedLogs.ToString());
	}

	public static ushort SearchSensorPort(in string hashKey)
	{
		lock (_haskKeyPortMapTable)
		{
			if (_haskKeyPortMapTable.TryGetValue(hashKey, out var port))
			{
				return port;
			}
		}

		return 0;
	}

	public Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> GetDeviceMapList(string filter = "")
	{
		lock (_deviceMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return _deviceMapTable;
			}
			else
			{
				return _deviceMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(k => k.Key, v => v.Value);
			}
		}
	}

	public Dictionary<string, ushort> GetDevicePortList(string filter = "")
	{
		lock (_haskKeyPortMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return _haskKeyPortMapTable;
			}
			else
			{
				return _haskKeyPortMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(k => k.Key, v => v.Value);
			}
		}
	}

	public static bool IsAvailablePort(in ushort port)
	{
		if (_properties != null)
		{
			var connections = _properties.GetActiveTcpConnections();
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
		in string deviceType, in string modelName, in string partsName, in string subPartsName, in string controlKey,
		out string hashKey, out ushort port)
	{
		var fullPartsName = partsName + subPartsName;
		hashKey = MakeHashKey(modelName, fullPartsName, controlKey);

		if (string.IsNullOrEmpty(hashKey))
		{
			Console.Error.Write("Impossible empty hashKey");
			port = 0;
			return false;
		}

		port = AllocateDevicePort(hashKey);

		if (port > 0)
		{
			if (_deviceMapTable.TryGetValue(modelName, out var devicesTypeMapTable))
			{
				if (devicesTypeMapTable.TryGetValue(deviceType, out var partsMapTable))
				{
					if (partsMapTable.TryGetValue(partsName, out var portsMapTable))
					{
						portsMapTable.Add(subPartsName + controlKey, port);
					}
					else if (partsMapTable.TryGetValue(fullPartsName, out portsMapTable))
					{
						portsMapTable.Add(controlKey, port);
					}
					else
					{
						var newPortsMapTable = new Dictionary<string, ushort>();
						newPortsMapTable.Add(controlKey, port);
						partsMapTable.Add(fullPartsName, newPortsMapTable);
					}
				}
				else
				{
					var portsMapTable = new Dictionary<string, ushort>();
					portsMapTable.Add(controlKey, port);
					var newPartsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
					newPartsMapTable.Add(fullPartsName, portsMapTable);

					devicesTypeMapTable.Add(deviceType, newPartsMapTable);
				}
			}
			else
			{
				var portsMapTable = new Dictionary<string, ushort>();
				portsMapTable.Add(controlKey, port);

				var partsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
				partsMapTable.Add(fullPartsName, portsMapTable);

				var devicesTypeMap = new Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>();
				devicesTypeMap.Add(deviceType, partsMapTable);

				_deviceMapTable.Add(modelName, devicesTypeMap);
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

			lock (_haskKeyPortMapTable)
			{
				isContained = _haskKeyPortMapTable.ContainsValue(port);
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
			lock (_haskKeyPortMapTable)
			{
				_haskKeyPortMapTable.Add(hashKey, newPort);
			}

			_sbAllocatedHistory.AppendFormat("Allocated for HashKey({0}) Port({1})", hashKey, newPort);
		}
		else
		{
			Console.Error.WriteLine($"Failed to allocate port for HashKey({hashKey}).");
		}
		_sbAllocatedHistory.AppendLine();

		return newPort;
	}

	public void ClearAllocatedHistory()
	{
		_sbAllocatedHistory.Clear();
		_sbAllocatedHistory.AppendLine("<Allocated information in BridgeManager>");
	}

	public void PrintAllocatedHistory()
	{
		Console.Write(_sbAllocatedHistory);
	}
}