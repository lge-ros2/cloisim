/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System.Net;
using System;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;
using Encoding = System.Text.Encoding;

public class BridgeManager : DeviceTransporter
{
	public readonly ushort pipePortNumber = 25554;
	public const ushort minPortRange = 49152;
	public const ushort maxPortRange = IPEndPoint.MaxPort;

	private bool runningWorkerThread = true;
	private Thread workerThread;

	private Dictionary<string, ushort> haskKeyPortMapTable = new Dictionary<string, ushort>();

	/*
	 * ModelName, DevicePluginType, Devicename, topic : portnumber
	 *
	 * {
	 * 	"ModelName":
	 *	{
	 * 		"DevicePluginType":
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
		InitializeTransporter();
	}

	void OnDestroy()
	{
		runningWorkerThread = false;

		if (workerThread != null)
		{
			if (workerThread.IsAlive)
			{
				workerThread.Join();
			}
		}

		DestroyTransporter();
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
			// if (string.IsNullOrEmpty(filter))
			{
				return deviceMapTable;
			}
			// else
			{
				// return haskKeyPortMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(p => p.Key, p => p.Value);
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

	public bool AllocateDevice(in string deviceType, in string modelName, in string partName, in string subPartName, out string hashKey, out ushort port)
	{
		hashKey = modelName + partName + subPartName;
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
					if (partsMapTable.TryGetValue(partName, out var portsMapTable))
					{
						portsMapTable.Add(subPartName, port);
					}
					else
					{
						var newPortsMapTable = new Dictionary<string, ushort>();
						newPortsMapTable.Add(subPartName, port);
						partsMapTable.Add(partName, newPortsMapTable);
					}
				}
				else
				{
					var portsMapTable = new Dictionary<string, ushort>();
					portsMapTable.Add(subPartName, port);
					var newPartsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
					newPartsMapTable.Add(partName, portsMapTable);

					devicesTypeMapTable.Add(deviceType, newPartsMapTable);
				}
			}
			else
			{
				var portsMapTable = new Dictionary<string, ushort>();
				portsMapTable.Add(subPartName, port);
				var partsMapTable = new Dictionary<string, Dictionary<string, ushort>>();
				partsMapTable.Add(partName, portsMapTable);
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
			Debug.LogFormat("HashKey({0}) is already occupied.", hashKey);
			return newPort;
		}

		// find available port number and start with minimum port range
		for (var index = 0; index < (maxPortRange - minPortRange); index++)
		{
			var port = (ushort)(minPortRange + index);
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

			Debug.LogFormat("Allocated for HashKey({0}), Port({1})", hashKey, newPort);
		}
		else
		{
			Debug.LogFormat("Failed to allocate port for HashKey({0}).", hashKey);
		}

		return newPort;
	}

	private void PortManageWorker()
	{
		var sw = new Stopwatch();

		// Debug.LogFormat("Start SensorPortManager - {0}::{1}", GetType().Name, MethodBase.GetCurrentMethod().Name);
		try
		{
			while (runningWorkerThread)
			{
				sw.Restart();

				var hashKey = ReceiveRequest();
				if (hashKey != null)
				{
					var hashKeyInString = Encoding.Default.GetString(hashKey);
					var port = SearchSensorPort(hashKeyInString);

					var portBuf = Convert.ToString(port);
					SendResponse(portBuf);

					sw.Stop();
					var timeElapsed = sw.ElapsedMilliseconds;
					Debug.LogFormat("-> Reply for {0} = {1} ({2} ms)", hashKeyInString, port, timeElapsed);
				}
			}
		}
		catch (ThreadInterruptedException)
		{
			Debug.LogWarning("Thread:Interrunpted");
			return;
		}
	}

	// Start is called before the first frame update
	void Start()
	{
		SetTagSize(0);

		InitializeResponsor(pipePortNumber);

		workerThread = new Thread(PortManageWorker);
		workerThread.Start();
	}
}