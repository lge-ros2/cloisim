---
name: add-new-sensor
description: "Add a new sensor device to CLOiSim, including the Device subclass, SDF pipeline wiring, and transport plugin. Use when: adding a new sensor type, implementing a new SDF sensor, creating a new device class."
---

# Add a New Sensor Device

End-to-end procedure for adding a new sensor type to CLOiSim. This requires coordinated changes across 4 layers: Device, Implement, Import, and Plugin.

## When to Use

- Adding a new SDF-defined sensor type (e.g., force/torque, magnetometer, altimeter)
- Porting a sensor from another simulator or from ROS sensor_msgs
- The SDFormat package already supports the sensor type, or you will add support

## Procedure

### 1. Create the Device Class

Create `Assets/Scripts/Devices/MyNewSensor.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

namespace SensorDevices
{
	public partial class MyNewSensor : Device
	{
		private messages.MyMessage _msg;

		// Per-axis noise holders (if applicable)
		private Noise _noiseX;

		protected override void OnAwake()
		{
			Mode = ModeType.TX_THREAD;
			DeviceName = name;
		}

		protected override void OnStart()
		{
			// Cache initial transforms, register with managers
		}

		protected override void OnReset()
		{
			// Restore initial state
		}

		protected override void InitializeMessages()
		{
			_msg = new messages.MyMessage();
			_msg.Stamp = new messages.Time();
			// Allocate all nested sub-messages explicitly
		}

		protected override void SetupMessages()
		{
			_msg.EntityName = DeviceName;
		}

		// For timer-polled TX_THREAD: override GenerateMessage
		protected override void GenerateMessage()
		{
			// Populate message fields from current state
			_msg.Stamp.Set(GetNextSyntheticTime());
			PushDeviceMessage<messages.MyMessage>(_msg);
		}

		// For event-driven TX_THREAD: call EnqueueMessage() from
		// async callbacks instead, and let default GenerateMessage drain the queue

		private void FixedUpdate()
		{
			// Physics-based sensing here (IMU, force/torque)
			// Apply noise: _noiseX.Apply<float>(ref value, Time.fixedDeltaTime);
		}
	}
}
```

**Key decisions:**
- **Timer-polled** (IMU, GPS): override `GenerateMessage()` to build + push messages directly. TX thread calls it at `UpdateRate`.
- **Event-driven** (camera, lidar): call `EnqueueMessage()` from async callbacks (e.g., `AsyncGPUReadback`). Default `GenerateMessage()` drains the queue.
- **Physics-based**: use `FixedUpdate()` for transform/velocity sampling. Non-physics: use `Update()`.

### 2. Add Noise Support (Optional)

If the sensor needs noise, add setup methods:

```csharp
public void SetupNoise(in SDFormat.Noise noise)
{
	_noiseX = new Noise(noise);
}

// Or per-axis:
public void SetupNoises(in SDFormat.XxxSensor sensor)
{
	if (sensor.XNoise != null)
		_noiseX = new Noise(sensor.XNoise);
}
```

Apply noise in `FixedUpdate()` or `GenerateMessage()`:
```csharp
_noiseX.Apply<float>(ref value, Time.fixedDeltaTime);  // single value
_noise.Apply<double>(dataArray);                         // array (parallelized)
```

### 3. Add the Implement Extension Method

Edit `Assets/Scripts/Tools/SDF/Implement/Implement.Sensor.cs` — add a new static extension method:

```csharp
public static Device AddMyNewSensor(this GameObject targetObject, in SDFormat.MyNewSensorType element)
{
	var newSensorObject = new GameObject();
	targetObject.AttachSensor(newSensorObject);

	var sensor = newSensorObject.AddComponent<SensorDevices.MyNewSensor>();
	sensor.DeviceName = newSensorObject.GetFrameName();

	// Configure sensor-specific parameters from SDF element
	// sensor.SomeParameter = element.SomeField;

	// Setup noise if applicable
	if (element.Noise != null)
		sensor.SetupNoise(element.Noise);

	return sensor;
}
```

### 4. Add the Import Case

Edit `Assets/Scripts/Tools/SDF/Import/Import.Sensor.cs` — add a case in the sensor type switch:

```csharp
case "my_new_sensor":
	var myData = sensor.GetMyNewSensorData();  // or sensor.MyNewSensorData
	device = targetObject.AddMyNewSensor(myData);
	break;
```

The import layer automatically handles after the switch:
- Setting `UpdateRate` from SDF
- Setting `EnableVisualize`
- Applying sensor pose offset
- Tagging the GameObject as `"Sensor"`

### 5. Create the Plugin

Create `Assets/Scripts/CLOiSimPlugins/MyNewSensorPlugin.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;

public class MyNewSensorPlugin : CLOiSimPlugin
{
	private SensorDevices.MyNewSensor _sensor;

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.SENSOR;  // or a new specific type
		_sensor = gameObject.GetComponent<SensorDevices.MyNewSensor>();
	}

	protected override IEnumerator OnStart()
	{
		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _sensor);
		}

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
			default:
				break;
		}
	}
}
```

### 6. Verify the SDF Package

Ensure the `com.lge-ros2.sdformat` package supports the sensor type. If not:
1. Add the domain class to the SDFormat package
2. Add the sensor type string mapping
3. Update the package version

### 7. Verify SDF Plugin Resolution

The SDF `<plugin filename="MyNewSensorPlugin">` attribute must match the C# class name exactly, since the importer uses `Type.GetType(pluginLibraryName)`.

## Checklist

- [ ] Device class in `Devices/` with correct `Mode`, `OnAwake`, `InitializeMessages`, `SetupMessages`, `GenerateMessage`
- [ ] Noise support (if applicable)
- [ ] `AddMyNewSensor()` extension method in `Implement.Sensor.cs`
- [ ] Case in `Import.Sensor.cs` type-switch
- [ ] Plugin class in `CLOiSimPlugins/` with `OnAwake`, `OnStart`, `HandleCustomRequestMessage`
- [ ] Protobuf message type exists (or added via `.gen_proto_code.sh`)
- [ ] SDFormat package supports the sensor type
- [ ] Plugin class name matches SDF `<plugin filename="...">` attribute
- [ ] License header on all new files
- [ ] Uses tabs for indentation, Allman braces
