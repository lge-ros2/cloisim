/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private Transporter transport = new Transporter();

	private string subPartsName = string.Empty;

	public string SubPartsName
	{
		get => this.subPartsName;
		set => this.subPartsName = value;
	}

	public Transporter GetTransport()
	{
		return transport;
	}

	private bool PrepareDevice(in string subPartsAndKey, out ushort port, out ulong hash)
	{
		if (BridgeManager.AllocateDevice(type.ToString(), modelName, partsName, subPartsAndKey, out var hashKey, out port))
		{
			allocatedDeviceHashKeys.Add(hashKey);
			allocatedDevicePorts.Add(port);

			hash = DeviceHelper.GetStringHashCode(hashKey);
			// Debug.LogFormat("PrepareDevice - port({0}) hashKey({1}) hashValue({2:X})", port, hashKey, hash);
			return true;
		}

		Debug.LogError("Port for device is not allocated!!!!!!!! - " + hashKey);
		hash = 0;
		return false;
	}

	protected static bool DeregisterDevice(in List<ushort> allocatedPorts, in List<string> hashKeys)
	{
		BridgeManager.DeallocateDevice(allocatedPorts, hashKeys);
		return true;
	}

	protected bool RegisterTxDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(subPartsName + key, out port, out var hash))
		{
			transport.InitializePublisher(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Tx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterRxDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(subPartsName + key, out port, out var hash))
		{
			transport.InitializeSubscriber(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Rx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterServiceDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(subPartsName + key, out port, out var hash))
		{
			transport.InitializeResponsor(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register service device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterClientDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(subPartsName + key, out port, out var hash))
		{
			transport.InitializeRequester(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register client device {0}, {1}", modelName, partsName);

		return false;
	}
}