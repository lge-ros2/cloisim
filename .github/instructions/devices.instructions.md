---
applyTo: "Assets/Scripts/Devices/**"
---

# Device Development Instructions

Devices are sensor/actuator implementations as `Device`-derived MonoBehaviours located in `Assets/Scripts/Devices/`.

## Device Base Class Lifecycle

```
Awake() → OnAwake() [abstract] + InitializeMessages() [virtual]
Start() → SetupMessages() [virtual] + DelayedStart() coroutine
DelayedStart() → acquires Clock → OnStart() [virtual] → starts TX/RX coroutine or thread
OnDestroy() → stops running, clears queues, joins threads, disposes DeviceMessageQueue
Reset() → flushes queues, resets synthetic time, calls OnReset() [virtual]
```

## ModeType Selection

Set `Mode` in `OnAwake()`:

| Mode | Mechanism | When to Use |
|------|-----------|-------------|
| `TX` | Unity coroutine, `WaitForSeconds(UpdatePeriod)` | Simple sensors publishing on main thread |
| `RX` | Unity coroutine, `WaitUntil` queue has data | Simple command receivers |
| `TX_THREAD` | Background thread with high-res timing | Most sensors: Lidar, Camera, IMU, GPS, JointState, Clock |
| `RX_THREAD` | Background thread with spin-wait polling | Command receivers: JointCommand, MicomCommand |

## TX_THREAD Sub-Patterns

### Event-Driven (Camera, Lidar, Contact)
Data arrives via `EnqueueMessage()` which signals `_txDataReady`. TX thread wakes immediately, default `GenerateMessage()` drains `_messageQueue` via `PushDeviceMessage()`.

### Timer-Polled (IMU, GPS, JointState, Clock)
Override `GenerateMessage()` to build and push data directly. TX thread calls it at the configured update rate.

## Required Methods to Override

| Method | Required | Purpose |
|--------|----------|---------|
| `OnAwake()` | **Yes (abstract)** | Set `Mode`, initialize device fields |
| `GenerateMessage()` | For timer-polled TX | Build protobuf message, call `PushDeviceMessage<T>()` |
| `ProcessReceivedDeviceMessage()` | For RX modes | Handle incoming messages |
| `InitializeMessages()` | Optional | Allocate protobuf message objects |
| `SetupMessages()` | Optional | Configure messages after init |
| `OnStart()` | Optional | Post-init setup |
| `OnReset()` | Optional | Reset device state |

## DeviceMessage Pool Pattern

Use the static pool to avoid GC pressure:

```csharp
// In GenerateMessage() or sensor callback:
PushDeviceMessage<messages.LaserScan>(myLaserScanMsg);
// Internally: takes from ConcurrentBag pool (max 128), serializes, pushes to DeviceMessageQueue
// After publishing, SenderThread returns messages to pool via Device.ReturnDeviceMessage()
```

Fast paths bypass protobuf for binary sensor data:
- `ImageStamped` → `SetRawImage()` (zero-copy binary)
- `Segmentation` → `SetRawSegmentation()`
- `ImagesStamped` → `SetRawImagesStamped()`
- Everything else → `SetMessage<T>()` (protobuf serialization)

## Synthetic Timestamps

Use `GetNextSyntheticTime()` for jitter-free timestamps:
- First call snaps to current `Clock.SimTime`
- Subsequent calls advance by exactly `1.0 / UpdateRate` (double precision)
- Reset sets `_syntheticTime = -1` to re-snap
- Thread-safe (internally locked)

## Noise Integration Patterns

| Device Type | Approach | Example |
|-------------|----------|---------|
| Lidar | CPU-side `SensorDevices.Noise` on arrays | `_noise.Apply<double>(ranges)` after GPU readback |
| Camera | GPU shader (`GaussianNoise.shader`) via `CommandBuffer` blit | Material uniforms: `_Mean`, `_StdDev` |
| IMU | Per-axis `SensorDevices.Noise` instances (6 total) | `_noise.Apply<float>(ref value, deltaTime)` in `FixedUpdate()` |

The `Noise` class supports:
- Single-value: `Apply<T>(ref T data, deltaTime)`
- Array: `Apply<T>(T[] data, deltaTime)` — parallelized via `Parallel.For` with `ProcessorCount / 4` threads

## ISensorRenderable Interface

Implemented by Camera & Lidar for centralized render scheduling via `SensorRenderManager`:
- `RenderPeriod` — render interval
- `CanRender` — gate for render readiness
- `ExecuteRenderStep(float)` — called by render manager
- `IsURT` — true for Unified Ray Tracing sensors

## Key Rules

- Always use `AsyncGPUReadback` — never synchronous GPU reads
- Do not allocate in `Update()` / `FixedUpdate()` — use the `DeviceMessage` pool
- Use `ConcurrentQueue<>` for cross-thread data, never bare `Queue<>` or `List<>`
- Set update rate via `SetUpdateRate(float)` — high-res timing (≥50 Hz) uses `Stopwatch`-based spin-yield
- Frame names are built by walking up the transform hierarchy: `"model::link::sensor"`
