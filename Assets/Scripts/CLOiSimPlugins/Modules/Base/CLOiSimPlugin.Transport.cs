/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	protected Publisher Publisher => thread.Publisher;
	protected Subscriber Subscriber => thread.Subscriber;
	protected Requestor Requestor => thread.Requestor;
	protected Responsor Responsor => thread.Responsor;

	private bool PrepareDevice(in string subPartsName, out ushort port, out ulong hash)
	{
		if (BridgeManager.AllocateDevice(type.ToString(), modelName, partsName, subPartsName, out var hashKey, out port))
		{
			allocatedDeviceHashKeys.Add(hashKey);

			hash = DeviceHelper.GetStringHashCode(hashKey);
			// Debug.LogFormat("PrepareDevice - port({0}) hash({1})", port, hash);
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

	protected bool RegisterTxDevice(in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out var port, out var hash))
		{
			thread.InitializePublisher(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Tx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterRxDevice(in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out var port, out var hash))
		{
			thread.InitializeSubscriber(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register Rx device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterServiceDevice(in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out var port, out var hash))
		{
			thread.InitializeResponsor(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register service device {0}, {1}", modelName, partsName);

		return false;
	}

	protected bool RegisterClientDevice(in string subPartsName = "")
	{
		if (PrepareDevice(subPartsName, out var port, out var hash))
		{
			thread.InitializeRequester(port, hash);
			return true;
		}

		Debug.LogErrorFormat("Failed to register client device {0}, {1}", modelName, partsName);

		return false;
	}
}