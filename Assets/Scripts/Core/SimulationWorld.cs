/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using messages = cloisim.msgs;

[RequireComponent(typeof(Clock))]
public class SimulationWorld : CLOiSimPlugin
{
	private Clock _clock = null;

	private bool _signalReset = false;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.WORLD;
		_modelName = "World";
		_partsName = this.GetType().Name;

		_clock = gameObject.GetComponent<Clock>();
		_attachedDevices.Add(_clock);
	}

	protected override void OnStart()
	{
		if (RegisterTxDevice(out var portTx, "Clock"))
		{
			AddThread(portTx, SenderThread, _clock);
		}

		if (RegisterClientDevice(out var portClient, "Control"))
		{
			AddThread(portClient, ClientThread);
		}
	}

	public Clock GetClock()
	{
		return _clock;
	}

	public void SignalReset()
	{
		Debug.Log("SignalReset");
		_signalReset = true;
	}

	private void ClientThread(System.Object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var requestor = GetTransport().Get<Requestor>(paramObject.targetPort);

		if (requestor == null)
		{
			return;
		}

		var requestResetMessage = new messages.Param();
		requestResetMessage.Name = "reset_simulation";
		requestResetMessage.Value = new messages.Any { Type = messages.Any.ValueType.Boolean, BoolValue = true };

		var deviceMessage = new DeviceMessage();
		while (PluginThread.IsRunning)
		{
			if (_signalReset == false)
			{
				CLOiSimPluginThread.Sleep(500);
				continue;
			}

			deviceMessage.SetMessage<messages.Param>(requestResetMessage);
			try {
				if (requestor.SendRequest(deviceMessage))
				{
					var receivedBuffer = requestor.ReceiveResponse();
					if (receivedBuffer != null)
					{
						var responseMessage = CLOiSimPluginThread.ParseMessageParam(receivedBuffer);
						if (responseMessage.Name == "result")
						{
							Debug.LogFormat("simulation reset result: {0}", responseMessage.Value.StringValue);
						}
					}
				}
				else
				{
					Debug.LogError("SendRequest(ResetMessage) failed");
				}
			}
			catch (NetMQ.FiniteStateMachineException ex)
            {
                Debug.LogErrorFormat($"[SimulationWorld] FSM error: {ex.Message}.");
				var usedTargetPort = requestor.TargetPort;
				var usedHash = requestor.Hash;
				requestor.Dispose();
				requestor = new Requestor(usedHash);
				requestor.Initialize(usedTargetPort);
            }

			_signalReset = false;
		}
		deviceMessage.Dispose();
	}
}