/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using UnityEngine;
using messages = cloisim.msgs;

[RequireComponent(typeof(Clock))]
public class SimulationWorld : CLOiSimPlugin
{
	private Clock _clock = null;

	private volatile bool _signalReset = false;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.WORLD;
		_modelName = "World";
		_partsName = GetType().Name;

		_clock = gameObject.GetComponent<Clock>();
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterTxDevice(out var portTx, "Clock"))
		{
			AddThread(portTx, SenderThread, _clock);
		}

		if (RegisterClientDevice(out var portClient, "Control"))
		{
			AddThread(portClient, ClientThread);
		}

		yield return null;
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

	private void ClientThread(object threadObject)
	{
		var paramObject = threadObject as CLOiSimPluginThread.ParamObject;
		var requestor = GetTransport().Get<Requestor>(paramObject.targetPort);

		if (requestor == null)
		{
			return;
		}

		var resetParam = new messages.Param();
		resetParam.Params["reset_simulation"] = new messages.Any { Type = messages.Any.ValueType.Boolean, BoolValue = true };

		var deviceMessage = new DeviceMessage();
		while (PluginThread.IsRunning)
		{
			if (_signalReset == false)
			{
				CLOiSimPluginThread.Sleep(500);
				continue;
			}

			deviceMessage.SetMessage(resetParam);
			try
			{
				if (requestor.SendRequest(deviceMessage))
				{
					var receivedBuffer = requestor.ReceiveResponse();
					if (receivedBuffer != null)
					{
						var responseMessage = CLOiSimPluginThread.ParseMessageParam(receivedBuffer);
						if (responseMessage.Params.ContainsKey("result"))
						{
							Debug.LogFormat("simulation reset result: {0}", responseMessage.Params["result"].StringValue);
						}
					}
					else
					{
						// ReceiveResponse timed out: REQ socket FSM is stuck in "waiting for reply".
						// The only recovery is to recreate the socket — drain is insufficient when
						// the remote end never sent a reply.
						GetTransport().ReinitializeRequester(paramObject.targetPort);
						requestor = GetTransport().Get<Requestor>(paramObject.targetPort);
					}
				}
				else
				{
					Debug.LogError("SendRequest(ResetMessage) failed");
				}
			}
			catch (NetMQ.FiniteStateMachineException)
			{
				GetTransport().ReinitializeRequester(paramObject.targetPort);
				requestor = GetTransport().Get<Requestor>(paramObject.targetPort);
			}

			_signalReset = false;
		}
		deviceMessage.Dispose();
	}
}