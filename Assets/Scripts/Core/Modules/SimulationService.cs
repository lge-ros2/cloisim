/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;
using WebSocketSharp.Server;

public class SimulationService : IDisposable
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
		wsServer.ReuseAddress = true;
		wsServer.KeepClean = true;
		wsServer.WaitTime = TimeSpan.FromMilliseconds(5000);

		InitializeServices();

 		wsServer.Start();

		if (wsServer.IsListening)
		{
			var wsLog = new StringBuilder();
			wsLog.Append("Listening on port ");
			wsLog.Append(wsServer.Port);
			wsLog.Append(", and providing services are:");
			wsLog.AppendLine();

			foreach (var path in wsServer.WebSocketServices.Paths)
			{
				wsLog.Append(" - ");
				wsLog.Append(path);
				wsLog.AppendLine();
			}

			Debug.Log(wsLog);
		}
	}

	~SimulationService()
	{
		Dispose();
	}

	public void Dispose()
	{
		if (wsServer != null)
		{
			Debug.Log("Stop WebSocket Server");
			wsServer.RemoveWebSocketService("/control");
			wsServer.RemoveWebSocketService("/markers");
			wsServer.Stop();
			wsServer = null;
		}

		GC.SuppressFinalize(this);
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
}