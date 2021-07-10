/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private Transporter transport = new Transporter();

	public Transporter GetTransport()
	{
		return transport;
	}

	private bool PrepareDevice(in string subPartsName, out ushort port, out ulong hash)
	{
		if (BridgeManager.AllocateDevice(type.ToString(), modelName, partsName, subPartsName, out var hashKey, out port))
		{
			allocatedDeviceHashKeys.Add(hashKey);

			hash = DeviceHelper.GetStringHashCode(hashKey);
			// Debug.LogFormat("PrepareDevice - port({0}) hashKey({1}) hashValue({2:X})", port, hashKey, hash);
			return true;
		}

		Debug.LogError("Port for device is not allocated!!!!!!!! - " + hashKey);
		hash = 0;
		return false;
	}

	protected static bool DeregisterDevice(in string hashKey)
	{
		BridgeManager.DeallocateDevice(hashKey);
		return true;
	}

	protected bool RegisterTxDevice(out ushort port, in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out port, out var hash))
		{
			transport.InitializePublisher(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Tx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterRxDevice(out ushort port, in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out port, out var hash))
		{
			transport.InitializeSubscriber(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Rx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterServiceDevice(out ushort port, in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out port, out var hash))
		{
			transport.InitializeResponsor(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register service device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterClientDevice(out ushort port, in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out port, out var hash))
		{
			transport.InitializeRequester(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register client device {0}, {1}", modelName, partsName);

		return false;
	}
}