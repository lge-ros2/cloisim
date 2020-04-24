/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using System.Net.NetworkInformation;
using System.Net;

public class BridgePortManager : DeviceTransporter
{
	public readonly ushort pipePortNumber = 25554;
	public const ushort minPortRange = 49152;
	public const ushort maxPortRange = IPEndPoint.MaxPort;

	private Thread workerThread;

	private Dictionary<string, ushort> portMapTable;

	private IPGlobalProperties properties = null;

	BridgePortManager()
	{
		portMapTable = new Dictionary<string, ushort>();
		properties = IPGlobalProperties.GetIPGlobalProperties();
	}

	public void DeallocateSensorPort(string hashKey)
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
			Debug.LogFormat("Failed to remove HashKey({0})!!!", hashKey);
		}
	}

	public ushort SearchSensorPort(string hashKey)
	{
		ushort port = 0;

		lock (portMapTable)
		{
			foreach (KeyValuePair<string, ushort> each in portMapTable)
			{
				string _Key = each.Key;
				if (_Key == hashKey)
				{
					port = each.Value;
					break;
				}
			}
		}

		return port;
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

	public ushort AllocateSensorPort(string hashKey)
	{
		// check if already occupied
		ushort newPort = SearchSensorPort(hashKey);

		if (newPort != 0)
		{
			Debug.LogFormat("HashKey({0}) is already occupied.", hashKey);
			return newPort;
		}

		// find available port number
		// start with minimum port range
		for (ushort index = 0; index < (maxPortRange - minPortRange); index++)
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

		if (newPort == 0)
		{
			Debug.LogFormat("Failed to allocate port for HashKey({0}).", hashKey);
		}
		else
		{
			lock (portMapTable)
			{
				Debug.LogFormat("Allocated for HashKey({0}), Port({1})", hashKey, newPort);
				portMapTable.Add(hashKey, newPort);
			}
		}

		return newPort;
	}

	private void PortManageWorker()
	{
		// Debug.LogFormat("Start SensorPortManager - {0}::{1}", GetType().Name, MethodBase.GetCurrentMethod().Name);
		while (true)
		{
			// Debug.Log("Waiting for Request Data");
			var hashKey = ReceiveRequest();

			var hashKeyInString = (hashKey == null) ? string.Empty : System.Text.Encoding.Default.GetString(hashKey);
			var port = SearchSensorPort(hashKeyInString);
			var portBuf = System.Convert.ToString(port);

			SendResponse(portBuf);
			Debug.LogFormat("-> Reply for {0} = {1}", hashKeyInString, port);
		}
	}

	void OnDestroy()
	{
		if (workerThread != null)
		{
			workerThread.Abort();
		}
		// Debug.LogFormat("{0}::{1}", GetType().Name, MethodBase.GetCurrentMethod().Name);
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