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
	// Reverse index of ports currently allocated in _haskKeyPortMapTable.
	// Kept in sync under lock(_haskKeyPortMapTable) so the allocation scan can test
	// port-in-use in O(1) instead of an O(n) Dictionary.ContainsValue() per candidate.
	private static HashSet<ushort> _allocatedPorts = new HashSet<ushort>();
	private static Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> _deviceMapTable = new Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>>();
	private static IPGlobalProperties _properties = IPGlobalProperties.GetIPGlobalProperties();

	// Reverse index: port -> exact route into _deviceMapTable. Lets removal locate
	// and prune the nested levels in O(1) instead of walking all four levels.
	private static readonly Dictionary<ushort, DeviceRouteKey> _portRouteTable = new Dictionary<ushort, DeviceRouteKey>();

	private readonly struct DeviceRouteKey : System.IEquatable<DeviceRouteKey>
	{
		public readonly string Model;
		public readonly string DeviceType;
		public readonly string PartsKey;
		public readonly string TopicKey;

		public DeviceRouteKey(in string model, in string deviceType, in string partsKey, in string topicKey)
		{
			Model = model;
			DeviceType = deviceType;
			PartsKey = partsKey;
			TopicKey = topicKey;
		}

		public bool Equals(DeviceRouteKey other) =>
			Model == other.Model && DeviceType == other.DeviceType && PartsKey == other.PartsKey && TopicKey == other.TopicKey;

		public override bool Equals(object obj) => obj is DeviceRouteKey key && Equals(key);

		public override int GetHashCode()
		{
			unchecked
			{
				var hash = 17;
				hash = hash * 31 + (Model?.GetHashCode() ?? 0);
				hash = hash * 31 + (DeviceType?.GetHashCode() ?? 0);
				hash = hash * 31 + (PartsKey?.GetHashCode() ?? 0);
				hash = hash * 31 + (TopicKey?.GetHashCode() ?? 0);
				return hash;
			}
		}
	}

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
			if (!_portRouteTable.TryGetValue(devicePort, out var route))
			{
				return;
			}

			if (_deviceMapTable.TryGetValue(route.Model, out var devicesTypeMapTable) &&
				devicesTypeMapTable.TryGetValue(route.DeviceType, out var partsMapTable) &&
				partsMapTable.TryGetValue(route.PartsKey, out var portsMapTable))
			{
				portsMapTable.Remove(route.TopicKey);

				if (portsMapTable.Count == 0)
					partsMapTable.Remove(route.PartsKey);

				if (partsMapTable.Count == 0)
					devicesTypeMapTable.Remove(route.DeviceType);

				if (devicesTypeMapTable.Count == 0)
					_deviceMapTable.Remove(route.Model);
			}

			_portRouteTable.Remove(devicePort);
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
			foreach (var hashKey in hashKeys)
			{
				if (_haskKeyPortMapTable.TryGetValue(hashKey, out var removedPort))
				{
					_haskKeyPortMapTable.Remove(hashKey);
					_allocatedPorts.Remove(removedPort);
					_sbDeallocatedLogs.AppendLine($"- {hashKey}");
				}
				else
				{
					Console.Error.Write($"Failed to remove HashKey({hashKey})!!!!");
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
				return CloneDeviceMapTable(_deviceMapTable);
			}
			else
			{
				return CloneDeviceMapTable(_deviceMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(k => k.Key, v => v.Value));
			}
		}
	}

	public Dictionary<string, ushort> GetDevicePortList(string filter = "")
	{
		lock (_haskKeyPortMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return new Dictionary<string, ushort>(_haskKeyPortMapTable);
			}
			else
			{
				return _haskKeyPortMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(k => k.Key, v => v.Value);
			}
		}
	}

	private static Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> CloneDeviceMapTable(
		Dictionary<string, Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>> source)
	{
		return source.ToDictionary(
			modelEntry => modelEntry.Key,
			modelEntry => modelEntry.Value.ToDictionary(
				deviceTypeEntry => deviceTypeEntry.Key,
				deviceTypeEntry => deviceTypeEntry.Value.ToDictionary(
					partsEntry => partsEntry.Key,
					partsEntry => new Dictionary<string, ushort>(partsEntry.Value))));
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

	/// <summary>
	/// Snapshot all local ports currently bound by active TCP connections.
	/// Taken once per allocation so the candidate scan does not call
	/// GetActiveTcpConnections() (a kernel syscall) for every candidate port.
	/// </summary>
	private static HashSet<int> SnapshotActiveTcpLocalPorts()
	{
		var ports = new HashSet<int>();
		if (_properties != null)
		{
			var connections = _properties.GetActiveTcpConnections();
			foreach (var connection in connections)
			{
				ports.Add(connection.LocalEndPoint.Port);
			}
		}
		return ports;
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
			lock (_deviceMapTable)
			{
				var partsKey = fullPartsName;
				var topicKey = controlKey;

				// Existing short-name parts entry keys its ports by subPartsName + controlKey;
				// otherwise ports live under fullPartsName keyed by controlKey.
				if (_deviceMapTable.TryGetValue(modelName, out var devicesTypeMapTable) &&
					devicesTypeMapTable.TryGetValue(deviceType, out var partsMapTable) &&
					partsMapTable.ContainsKey(partsName))
				{
					partsKey = partsName;
					topicKey = subPartsName + controlKey;
				}

				RegisterDeviceRoute(modelName, deviceType, partsKey, topicKey, port);
			}

			return true;
		}

		return false;
	}

	private static void RegisterDeviceRoute(in string modelName, in string deviceType, in string partsKey, in string topicKey, in ushort port)
	{
		if (!_deviceMapTable.TryGetValue(modelName, out var devicesTypeMapTable))
		{
			devicesTypeMapTable = new Dictionary<string, Dictionary<string, Dictionary<string, ushort>>>();
			_deviceMapTable.Add(modelName, devicesTypeMapTable);
		}

		if (!devicesTypeMapTable.TryGetValue(deviceType, out var partsMapTable))
		{
			partsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
			devicesTypeMapTable.Add(deviceType, partsMapTable);
		}

		if (!partsMapTable.TryGetValue(partsKey, out var portsMapTable))
		{
			portsMapTable = new Dictionary<string, ushort>();
			partsMapTable.Add(partsKey, portsMapTable);
		}

		portsMapTable.Add(topicKey, port);
		_portRouteTable[port] = new DeviceRouteKey(modelName, deviceType, partsKey, topicKey);
	}

	public static ushort AllocateDevicePort(in string hashKey)
	{
		lock (_haskKeyPortMapTable)
		{
			if (_haskKeyPortMapTable.TryGetValue(hashKey, out var occupiedPort))
			{
				var errorMessage = string.Format("HashKey({0}) is already occupied.", hashKey);
				Console.Error.Write(errorMessage);
				Main.UIController?.SetErrorMessage(errorMessage);
				return 0;
			}

			ushort newPort = 0;

			// One syscall snapshot of OS-occupied ports, then an O(1) check per
			// candidate against both that snapshot and our own allocated-port set.
			// Avoids holding this lock across up to ~16k GetActiveTcpConnections()
			// syscalls + O(n) ContainsValue() scans (a main-thread-stalling hazard).
			var activeTcpPorts = SnapshotActiveTcpLocalPorts();

			for (var index = 0; index < (MaxPortRange - MinPortRange); index++)
			{
				var port = (ushort)(MinPortRange + index);
				if (!_allocatedPorts.Contains(port) && !activeTcpPorts.Contains(port))
				{
					newPort = port;
					break;
				}
			}

			if (newPort > 0)
			{
				_haskKeyPortMapTable.Add(hashKey, newPort);
				_allocatedPorts.Add(newPort);
				_sbAllocatedHistory.AppendFormat("Allocated for HashKey({0}) Port({1})", hashKey, newPort);
			}
			else
			{
				Console.Error.WriteLine($"Failed to allocate port for HashKey({hashKey}).");
			}

			_sbAllocatedHistory.AppendLine();
			return newPort;
		}
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