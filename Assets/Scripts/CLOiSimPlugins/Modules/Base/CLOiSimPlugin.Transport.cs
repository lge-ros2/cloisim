/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public abstract partial class CLOiSimPlugin : MonoBehaviour, ICLOiSimPlugin
{
	private Transporter _transport = new Transporter();

	[SerializeField]
	private string _subPartsName = string.Empty;

	public string SubPartsName
	{
		get => this._subPartsName;
		set => this._subPartsName = value;
	}

	public Transporter GetTransport()
	{
		return _transport;
	}

	private bool PrepareDevice(in string subPartsAndKey, out ushort port, out ulong hash)
	{
		if (BridgeManager.AllocateDevice(_type.ToString(), _modelName, _partsName, subPartsAndKey, out var hashKey, out port))
		{
			_allocatedDeviceHashKeys.Add(hashKey);
			_allocatedDevicePorts.Add(port);

			hash = DeviceHelper.GetStringHashCode(hashKey);
			// Debug.LogFormat("PrepareDevice - port({0}) hashKey({1}) hashValue({2:X})", port, hashKey, hash);
			return true;
		}

		Debug.LogError($"Port for device is not allocated !!! {hashKey}");
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
		if (PrepareDevice(_subPartsName + key, out port, out var hash))
		{
			_transport.InitializePublisher(port, hash);
			return true;
		}

		Debug.LogErrorFormat($"Failed to register Tx device {_modelName}, {_partsName}, {_subPartsName}");

		return false;
	}

	protected bool RegisterRxDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(_subPartsName + key, out port, out var hash))
		{
			_transport.InitializeSubscriber(port, hash);
			return true;
		}

		Debug.LogErrorFormat($"Failed to register Rx device {_modelName}, {_partsName}, {_subPartsName}");

		return false;
	}

	protected bool RegisterServiceDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(_subPartsName + key, out port, out var hash))
		{
			_transport.InitializeResponsor(port, hash);
			return true;
		}

		Debug.LogErrorFormat($"Failed to register service device {_modelName}, {_partsName}, {_subPartsName}");

		return false;
	}

	protected bool RegisterClientDevice(out ushort port, in string key = "")
	{
		if (PrepareDevice(_subPartsName + key, out port, out var hash))
		{
			_transport.InitializeRequester(port, hash);
			return true;
		}

		Debug.LogErrorFormat($"Failed to register client device {_modelName}, {_partsName}, {_subPartsName}");

		return false;
	}
}