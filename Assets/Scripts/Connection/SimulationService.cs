/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Net;
using System;
using UnityEngine;
using WebSocketSharp.Server;

public class SimulationService : MonoBehaviour
{
	public const string SUCCESS = "ok";
	public const string FAIL = "fail";
	public const string Delimiter = "!%!";

	public string defaultWebSocketAddress = "127.0.0.1";
	public int defaultWebSocketServicePort = 8080;

	private WebSocketServer wsServer = null;

	void Awake()
	{
		var wsAddress = IPAddress.Parse(defaultWebSocketAddress);
		wsServer = new WebSocketServer(wsAddress, defaultWebSocketServicePort);
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
			Debug.LogFormat("Listening on port {0}, and providing services:", wsServer.Port);
			foreach (var path in wsServer.WebSocketServices.Paths)
			{
				Debug.LogFormat(" - {0}", path);
			}
		}
	}

	private void InitializeServices()
	{
		if (wsServer == null)
		{
			return;
		}

		var modelLoaderComponent = gameObject.GetComponent<ModelLoader>();
		var bridgeManagerComponent = gameObject.GetComponent<BridgeManager>();

		wsServer.AddWebSocketService<SimulationControlService>("/control", () => new SimulationControlService()
		{
			IgnoreExtensions = true,
			modelLoader = modelLoaderComponent,
			bridgeManager = bridgeManagerComponent
		});

		var markerVisualizer = gameObject.GetComponent<MarkerVisualizer>();
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
			wsServer.Stop();
		}
	}
}