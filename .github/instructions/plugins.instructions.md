---
applyTo: "Assets/Scripts/CLOiSimPlugins/**"
---

# Plugin Development Instructions

Plugins are the transport/control bridge layer between devices and the external ROS 2 bridge. They inherit from `CLOiSimPlugin` and manage NetMQ sockets, background threads, and port registration.

## Plugin Lifecycle

```
Awake() → SetCustomHandleRequestMessage() → OnAwake() [abstract]
Start() → OnPluginLoad() [virtual] → resolve names → DelayedOnStart() coroutine
DelayedOnStart() → OnStart() [abstract coroutine] → _thread.Start() → IsStarted = true → Started event
Reset() → OnReset() [virtual]
OnDestroy() → _thread.Dispose() → join threads → _transport.Dispose() → DeregisterDevice()
```

Startup is staggered: each plugin yields `_globalSequence` frames before `OnStart()` to prevent simultaneous port registration.

## OnAwake() Pattern

Set `_type` and cache device references:

```csharp
protected override void OnAwake()
{
    _type = ICLOiSimPlugin.Type.LASER;
    _lidar = GetComponent<Lidar>();
}
```

## OnStart() Pattern

Register transport and add threads:

```csharp
protected override IEnumerator OnStart()
{
    // 1. Register service port for info/config queries
    if (RegisterServiceDevice(out var portService, "Info"))
        AddThread(portService, ServiceThread);

    // 2. Register TX port for sensor data publishing
    if (RegisterTxDevice(out var portTx, "Data"))
        AddThread(portTx, SenderThread, _device);

    // 3. (Optional) Register RX port for command reception
    if (RegisterRxDevice(out var portRx, "Rx"))
        AddThread(portRx, ReceiverThread, _commandDevice);

    yield return null;
}
```

## Transport Registration API

| Method | Socket Type | Direction | Use Case |
|--------|------------|-----------|----------|
| `RegisterTxDevice(out port, key)` | `Publisher` (PUB) | Sim → Bridge | Sensor data streaming |
| `RegisterRxDevice(out port, key)` | `Subscriber` (SUB) | Bridge → Sim | Command reception |
| `RegisterServiceDevice(out port, key)` | `Responsor` (REP) | Bridge ↔ Sim | Request/reply queries |
| `RegisterClientDevice(out port, key)` | `Requestor` (REQ) | Sim → Bridge | Outbound requests |

The `key` parameter (e.g., `"Data"`, `"Info"`, `"Rx"`) becomes part of the hash key: `modelName + partsName + subPartsName + key`.

All sockets use **bind** (not connect) on `tcp://*:{port}`. The external bridge connects to these ports.

## Thread Functions

Pre-built thread functions in the base class:

| Function | What It Does |
|----------|-------------|
| `SenderThread` | Gets `Publisher` from transport, drains device's `DeviceMessageQueue`, publishes each message |
| `ReceiverThread` | Gets `Subscriber` from transport, receives messages, pushes to device queue |
| `ServiceThread` | Gets `Responsor` from transport, receives requests, dispatches to handler delegates |

`AddThread(port, threadFunction, pluginObject)` — the device passed as `pluginObject` is available as `paramObject.param` in the thread function.

## Service Request Handler Pattern

Set up automatically in `Awake()`. Base class handles:
- `"request_ros2"` → reads ROS 2 config from SDF `<ros2>` XML block
- `"request_static_transforms"` → serializes `_staticTfList`

Override `HandleCustomRequestMessage()` for plugin-specific requests:

```csharp
protected override void HandleCustomRequestMessage(
    in string requestType, in string requestValue, ref DeviceMessage response)
{
    switch (requestType)
    {
        case "request_output_type":
            SetTransformInfoResponse(ref response);
            break;
        case "request_transform":
            SetTransformInfoResponse(ref response);
            break;
    }
}
```

Common response helpers (static methods in base class):
- `SetTransformInfoResponse()` — serializes device pose + parent frame ID
- `SetCameraInfoResponse()` — serializes `CameraSensor` message
- `SetROS2CommonInfoResponse()` — serializes topic name + frame IDs
- `SetEmptyResponse()` — sends a boolean ack

## Port Management

- `BridgeManager` allocates ports in range `49152–65535`
- Hash key is built from: `modelName + partsName + subPartsName + controlKey`
- Port availability verified via `IPGlobalProperties.GetActiveTcpConnections()`
- Deregistration is automatic in `OnDestroy()` via `DeregisterDevice()`

## Plugin Type Enum

Set `_type` to one of: `WORLD`, `SENSOR`, `LASER`, `CAMERA`, `DEPTH_CAMERA`, `MULTICAMERA`, `SEGMENTATION_CAMERA`, `MICOM`, `GPS`, `IMU`, `SONAR`, `CONTACT`, `ACTOR`, `ELEVATOR`, `GROUNDTRUTH`

## Key Rules

- Every plugin must set `_type` in `OnAwake()`
- Every plugin must register ports and add threads in `OnStart()`
- Cleanup is handled by base `OnDestroy()` — threads are joined with 50ms timeout, transport disposed, ports deallocated
- Use `_thread.HandleRequestTypeValue` and `_thread.HandleRequestTypeChildren` delegates for service dispatch
- Messages use `messages = cloisim.msgs` namespace alias
- All message frames are prefixed with an 8-byte hash tag for routing
- Plugin class names must be resolvable via `Type.GetType()` from SDF `filename` attribute
