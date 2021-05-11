/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;
using WebSocketSharp.Server;

public class SimulationService : MonoBehaviour
{
	public const string SUCCESS = "ok";
	public const string FAIL = "fail";
	public const string Delimiter = "!%!";

	public int defaultWebSocketServicePort = 8080;

	private WebSocketServer wsServer = null;

	void Awake()
	{
		var envServicePort = Environment.GetEnvironmentVariable("CLOISIM_SERVICE_PORT");
		var servicePort = (envServicePort == null || envServicePort.Equals(""))? defaultWebSocketServicePort : int.Parse(envServicePort);
		wsServer = new WebSocketServer(servicePort);
	}

	// Start is called before the first frame update
	void Start()
	{
		InitializeServices();

		wsServer.KeepClean = true;
		wsServer.WaitTime = TimeSpan.FromMilliseconds(5000);
		wsServer.Start();

		if (wsServer.IsListening)
		{
			var wsLog = new StringBuilder();
			wsLog.AppendFormat("Listening on port {0}, and providing services are:", wsServer.Port);
			wsLog.AppendLine();

			foreach (var path in wsServer.WebSocketServices.Paths)
			{
				wsLog.AppendFormat(" - {0}", path);
				wsLog.AppendLine();
			}

			Debug.Log(wsLog);
		}
	}

	private void InitializeServices()
	{
		if (wsServer == null)
		{
			return;
		}

		var mainComponent = gameObject.GetComponent<Main>();
		var bridgeManagerComponent = gameObject.GetComponent<BridgeManager>();

		wsServer.AddWebSocketService<SimulationControlService>("/control", () => new SimulationControlService()
		{
			IgnoreExtensions = true,
			main = mainComponent,
			bridgeManager = bridgeManagerComponent
		});

		var UIRoot = GameObject.Find("UI");
		var markerVisualizer = UIRoot.GetComponent<MarkerVisualizer>();
		wsServer.AddWebSocketService<MarkerVisualizerService>("/markers", () => new MarkerVisualizerService(markerVisualizer)
		{
			IgnoreExtensions = true
		});
	}

	void OnDestroy()
	{
		if (wsServer != null)
		{
			Debug.Log("Stop WebSocket Server");
			wsServer.RemoveWebSocketService("/control");
			wsServer.RemoveWebSocketService("/markers");
			wsServer.Stop();
		}
	}
}