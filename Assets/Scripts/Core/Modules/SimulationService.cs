/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;
using WebSocketSharp.Server;

[DefaultExecutionOrder(100)]
public class SimulationService
{
	public const string SUCCESS = "ok";
	public const string FAIL = "fail";
	public const string Delimiter = "!%!";
	public const string SERVICE_PORT_ENVIRONMENT_NAME = "CLOISIM_SERVICE_PORT";

	public int defaultWebSocketServicePort;

	private WebSocketServer wsServer = null;

	public SimulationService(in int port = 8080)
	{
		this.defaultWebSocketServicePort = port;

		var envServicePort = Environment.GetEnvironmentVariable(SERVICE_PORT_ENVIRONMENT_NAME);
		var servicePort = (envServicePort == null || envServicePort.Equals("")) ? defaultWebSocketServicePort : int.Parse(envServicePort);
		wsServer = new WebSocketServer(servicePort);

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

		wsServer.AddWebSocketService<SimulationControlService>("/control", () => new SimulationControlService()
		{
			IgnoreExtensions = true
		});

		var UIRoot = Main.UIObject;
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