---
name: add-new-plugin
description: "Add a new CLOiSimPlugin for bridging a device to the ROS 2 transport layer. Use when: creating a transport bridge for an existing device, adding bidirectional control, implementing a new actuator plugin."
---

# Add a New Plugin

Step-by-step procedure for creating a new `CLOiSimPlugin` subclass that bridges a device (sensor or actuator) to the external ROS 2 bridge via NetMQ transport.

## When to Use

- Bridging an existing `Device` subclass to the ROS 2 ecosystem
- Adding a new actuator/controller plugin (e.g., elevator, gripper, arm)
- Creating a bidirectional plugin that both publishes sensor data and receives commands

## Procedure

### 1. Determine Plugin Architecture

Decide the transport topology:

| Pattern | Ports | Use Case |
|---------|-------|----------|
| **TX-only** | Service + TX | Read-only sensors (GPS, IMU, Lidar) |
| **TX+RX** | Service + TX + RX | Bidirectional (MicomPlugin: publishes encoder data, receives twist commands) |
| **Service-only** | Service | Config/query endpoints |
| **TX+RX+Client** | Service + TX + RX + Client | Full control (SimulationWorld: clock TX, control client) |

### 2. Create the Plugin Class

Create `Assets/Scripts/CLOiSimPlugins/MyPlugin.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using Any = cloisim.msgs.Any;

public class MyPlugin : CLOiSimPlugin
{
	private SensorDevices.MySensor _sensor;
	// For bidirectional: private SensorDevices.MyCommand _command;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.SENSOR;
		_sensor = gameObject.GetComponent<SensorDevices.MySensor>();
		// _command = gameObject.GetComponent<SensorDevices.MyCommand>();
	}

	protected override IEnumerator OnStart()
	{
		// 1. Service port (always first — handles info/config queries)
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		// 2. TX port (sensor data publishing)
		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _sensor);
		}

		// 3. RX port (command reception — bidirectional only)
		// if (RegisterRxDevice(out var portRx, "Rx"))
		// {
		//     AddThread(portRx, ReceiverThread, _command);
		// }

		yield return null;
	}

	protected override void HandleCustomRequestMessage(
		in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_transform":
				var devicePose = _sensor.GetPose();
				var deviceName = _sensor.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			// Add plugin-specific request handlers:
			// case "request_output_type":
			//     SetMyCustomResponse(ref response);
			//     break;

			default:
				break;
		}
	}

	// Overload for children-based requests (messages.Param with sub-params)
	protected override void HandleCustomRequestMessage(
		in string requestType, in List<messages.Param> requestChildren, ref DeviceMessage response)
	{
		switch (requestType)
		{
			default:
				break;
		}
	}
}
```

### 3. Read Plugin Parameters from SDF (Optional)

If the plugin needs SDF `<plugin>` parameters:

```csharp
protected override IEnumerator OnStart()
{
	var pluginParameters = GetPluginParameters();
	if (pluginParameters != null)
	{
		// Read specific elements
		var myParam = pluginParameters.GetValue<float>("my_parameter");
		// Read nested elements
		var noiseElem = pluginParameters.GetElement("noise");
	}

	// ... register devices and threads ...
	yield return null;
}
```

### 4. Wire to SDF

The SDF world/model file references the plugin:

```xml
<plugin name="MyPlugin" filename="MyPlugin">
  <my_parameter>42.0</my_parameter>
  <ros2>
    <topic_name>/my_topic</topic_name>
    <frame_id>my_frame</frame_id>
  </ros2>
</plugin>
```

The `filename` attribute must exactly match the C# class name — the importer uses `Type.GetType()`.

### 5. Add Common Response Helpers

Use base class static helpers for standard responses:

```csharp
// Transform info (pose + frame_id)
SetTransformInfoResponse(ref response, deviceName, pose, parentLinkName);

// Camera info (intrinsics, distortion)
SetCameraInfoResponse(ref response, cameraSensor);

// ROS 2 common info (topic, frame_id)
SetROS2CommonInfoResponse(ref response, topicName, frameId);

// Empty ack (boolean true)
SetEmptyResponse(ref response);
```

### 6. Handle Cleanup

Cleanup is automatic via the base class `OnDestroy()`:
- Threads joined with 50ms timeout per step
- Transport sockets closed
- Ports deallocated from `BridgeManager`

Override `OnReset()` only if the plugin has state that needs resetting:

```csharp
protected override void OnReset()
{
	// Reset plugin-specific state
}
```

## Multi-Device Plugin

For plugins managing multiple devices (e.g., `MicomPlugin` with motors + encoders), use `CLOiSimMultiPlugin` base class or manage multiple device references:

```csharp
protected override void OnAwake()
{
	_type = ICLOiSimPlugin.Type.MICOM;
	_sensorDevice = gameObject.GetComponent<SensorDevices.MicomSensor>();
	_commandDevice = gameObject.GetComponent<SensorDevices.MicomCommand>();
}

protected override IEnumerator OnStart()
{
	if (RegisterServiceDevice(out var portService, "Info"))
		AddThread(portService, ServiceThread);

	if (RegisterTxDevice(out var portTx, "Tx"))
		AddThread(portTx, SenderThread, _sensorDevice);

	if (RegisterRxDevice(out var portRx, "Rx"))
		AddThread(portRx, ReceiverThread, _commandDevice);

	yield return null;
}
```

## Checklist

- [ ] `_type` set in `OnAwake()` to appropriate `ICLOiSimPlugin.Type`
- [ ] Device component cached via `GetComponent<>()` in `OnAwake()`
- [ ] Service port registered and `ServiceThread` added
- [ ] TX/RX ports registered with correct keys
- [ ] `HandleCustomRequestMessage()` handles at least `"request_transform"`
- [ ] SDF `<plugin filename="">` matches class name exactly
- [ ] `OnStart()` ends with `yield return null`
- [ ] License header on file
- [ ] No cleanup code needed — base class handles it
