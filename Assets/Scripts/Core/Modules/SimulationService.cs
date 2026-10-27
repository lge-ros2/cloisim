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
	public static readonly string SUCCESS = "ok";
	public static readonly string FAIL = "fail";
	public static readonly string Delimiter = "!%!";
	public static readonly string SERVICE_PORT_ENVIRONMENT_NAME = "CLOISIM_SERVICE_PORT";
	private const string BenignHandshakeReadFailure = "The header cannot be read from the data source.";
	private const int MaxPortRetry = 100;

	private int _servicePort;
	public int ServicePort => _servicePort;
	public string PortSwitchMessage { get; private set; } = null;

	private WebSocketServer wsServer = null;
	private readonly object _disposeLock = new();
	private bool _isDisposed = false;

	public SimulationService(in int defaultWebSocketServicePort = SimServicePaths.DefaultPort)
	{
		var envServicePort = Environment.GetEnvironmentVariable(SERVICE_PORT_ENVIRONMENT_NAME);
		if (string.IsNullOrEmpty(envServicePort))
		{
			_servicePort = defaultWebSocketServicePort;
		}
		else if (!int.TryParse(envServicePort, out _servicePort))
		{
			_servicePort = defaultWebSocketServicePort;
			var warningMessage = $"Invalid {SERVICE_PORT_ENVIRONMENT_NAME} value '{envServicePort}'. Using default port {_servicePort}.";
			Main.UIController?.SetWarningMessage(warningMessage);
			Debug.LogWarning(warningMessage);
		}

		var requestedPort = _servicePort;
		var lastPort = Math.Min(requestedPort + MaxPortRetry, ushort.MaxValue);

		try
		{
			while (true)
			{
				wsServer = CreateServer(_servicePort);

				try
				{
					wsServer.Start();
					break;
				}
				catch (System.Net.Sockets.SocketException sex)
					when (sex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse
						&& _servicePort < lastPort)
				{
					Debug.LogWarning($"SimulationService port {_servicePort} is already in use. Trying {_servicePort + 1}.");
					DisposeServer();
					_servicePort++;
				}
			}

			if (_servicePort != requestedPort)
			{
				var warningMessage = $"SimulationService port {requestedPort} is already in use. Switched to port {_servicePort}.";
				PortSwitchMessage = warningMessage;
				Main.UIController?.SetWarningMessage(warningMessage);
				Debug.LogWarning(warningMessage);
			}

			if (IsStarted())
			{
				var wsLog = new StringBuilder();
				wsLog.Append(string.Concat("Listening on port ", wsServer.Port, ", and providing services are:"));
				wsLog.AppendLine();

				foreach (var path in wsServer.WebSocketServices.Paths)
				{
					wsLog.Append(string.Concat(" - ", path));
					wsLog.AppendLine();
				}

				Debug.Log(wsLog);
			}
		}
		catch (System.Net.Sockets.SocketException sex)
			when (sex.SocketErrorCode == System.Net.Sockets.SocketError.AddressAlreadyInUse)
		{
			var errMessage = $"SimulationService ports {requestedPort}-{_servicePort} are all in use. Change port via {SERVICE_PORT_ENVIRONMENT_NAME} env var.";
			Main.UIController?.SetErrorMessage(errMessage);
			Main.UIController?.ShowLoadingOverlay("SimulationService Failed", errMessage);
			Debug.LogError(errMessage);
		}
		catch (Exception ex) {
			var errMessage = "Failed to start SimulationService: " + ex.Message;
			Main.UIController?.SetErrorMessage(errMessage);
			Debug.LogError(errMessage);
		}
	}

	private WebSocketServer CreateServer(int port)
	{
		var server = new WebSocketServer(port)
		{
			ReuseAddress = true,
			KeepClean = true,
			WaitTime = TimeSpan.FromMilliseconds(5000)
		};

		server.Log.Output = (logData, _) =>
		{
			var msg = $"[WebSocket] {logData.Level}: {logData.Message}";
			var isBenignDisconnect = logData.Level == WebSocketSharp.LogLevel.Fatal &&
				logData.Message != null &&
				logData.Message.Contains(BenignHandshakeReadFailure, StringComparison.Ordinal);

			if (isBenignDisconnect)
			{
				Debug.Log($"[WebSocket] Disconnect before handshake completed: {logData.Message}");
				return;
			}

			switch (logData.Level)
			{
				case WebSocketSharp.LogLevel.Fatal:
				case WebSocketSharp.LogLevel.Error:
					Debug.LogWarning(msg);
					break;
				case WebSocketSharp.LogLevel.Warn:
					Debug.LogWarning(msg);
					break;
				default:
					Debug.Log(msg);
					break;
			}
		};

		InitializeServices(server);
		return server;
	}

	private void DisposeServer()
	{
		if (wsServer == null)
		{
			return;
		}

		try
		{
			wsServer.Stop();
		}
		catch (Exception)
		{
		}

		wsServer = null;
	}

	~SimulationService()
	{
		Dispose();
	}

	public bool IsStarted()
	{
		return (wsServer != null) ? wsServer.IsListening : false;
	}

	public void Dispose()
	{
		lock (_disposeLock)
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			if (wsServer != null)
			{
				Debug.Log("Stop WebSocket Server");
				wsServer.RemoveWebSocketService(SimServicePaths.Control);
				wsServer.RemoveWebSocketService(SimServicePaths.Markers);
				wsServer.Stop();
				wsServer = null;
			}
		}

		GC.SuppressFinalize(this);
	}

	private static void InitializeServices(WebSocketServer wsServer)
	{
		wsServer.AddWebSocketService(SimServicePaths.Control, () => new SimulationControlService()
		{
			IgnoreExtensions = true
		});

		SimulationControlService.SimVersion = UnityEngine.Application.version;

		var markerVisualizer = Main.UIObject?.GetComponent<MarkerVisualizer>();
		wsServer.AddWebSocketService(SimServicePaths.Markers, () => new MarkerVisualizerService(markerVisualizer)
		{
			IgnoreExtensions = true
		});
	}
}