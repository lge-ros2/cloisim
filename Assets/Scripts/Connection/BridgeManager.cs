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

	private Dictionary<string, ushort> portMapTable = new Dictionary<string, ushort>();

	private IPGlobalProperties properties = IPGlobalProperties.GetIPGlobalProperties();

	public void DeallocateSensorPort(in string hashKey)
	{
		bool isRemoved = false;

		lock (portMapTable)
		{
			isRemoved = portMapTable.Remove(hashKey);
		}

		if (isRemoved)
		{
			Debug.LogFormat("HashKey({0}) Removed.", hashKey);
		}
		else
		{
			Debug.LogWarningFormat("Failed to remove HashKey({0})!!!!", hashKey);
		}
	}

	public ushort SearchSensorPort(in string hashKey)
	{
		lock (portMapTable)
		{
			if (portMapTable.TryGetValue(hashKey, out var port))
			{
				return port;
			}
		}

		return 0;
	}

	public Dictionary<string, ushort> GetSensorPortList(string filter = "")
	{
		lock (portMapTable)
		{
			if (string.IsNullOrEmpty(filter))
			{
				return portMapTable;
			}
			else
			{
				return portMapTable.Where(p => p.Key.StartsWith(filter)).ToDictionary(p => p.Key, p => p.Value);
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

	public ushort AllocateSensorPort(in string hashKey)
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

			lock (portMapTable)
			{
				isContained = portMapTable.ContainsValue(port);
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
			lock (portMapTable)
			{
				portMapTable.Add(hashKey, newPort);
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