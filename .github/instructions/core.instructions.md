---
applyTo: "Assets/Scripts/Core/**"
---

# Core Module Instructions

Core modules provide simulation orchestration, port management, WebSocket services, and world-level coordination.

## BridgeManager — Device Port Registry

**Port range:** `49152–65535` (ephemeral)

Two data structures (both static, both locked):
- `_haskKeyPortMapTable` — `Dictionary<string, ushort>`: flat hashKey → port map
- `_deviceMapTable` — 4-level nested dictionary: `ModelName → DeviceType → PartsName → TopicName → Port`

**API:**
```csharp
// Allocate port: builds hashKey, scans for first available port, stores in both maps
static bool AllocateDevice(
    in string deviceType, in string modelName, in string partsName,
    in string subPartsName, in string controlKey,
    out string hashKey, out ushort port)

// Deallocate: removes from both maps
static void DeallocateDevice(in List<ushort> devicePorts, in List<string> hashKeys)

// Lookup by hashKey
static ushort SearchSensorPort(in string hashKey)

// Query maps (with optional prefix filter)
Dictionary<...> GetDeviceMapList(string filter = "")
Dictionary<string, ushort> GetDevicePortList(string filter = "")
```

Thread safety: `lock(_deviceMapTable)` and `lock(_haskKeyPortMapTable)`. Port availability checked via `IPGlobalProperties.GetActiveTcpConnections()`.

## SimulationService — WebSocket Server

- Uses **WebSocketSharp** library on port `8080` (override: `CLOISIM_SERVICE_PORT` env var)
- Two endpoints:
  - `/control` — `SimulationControlService`: handles `reset`, `device_list`, `port_list`, `fps`, `start_record`, `stop_record`
  - `/markers` — `MarkerVisualizerService`: runtime marker creation (line, text, box, sphere)
- Config: `ReuseAddress = true`, `KeepClean = true`, `WaitTime = 5000ms`
- Constants: `SUCCESS = "ok"`, `FAIL = "fail"`, `Delimiter = "!%!"`

## PluginStartTracker — Startup Coordination

- Discovers all `CLOiSimPlugin` components under a loaded root via `Bind()`
- Subscribes to each plugin's `Started` event
- Emits progress updates, fires `AllStartedEvent` when all plugins report started
- World is not considered "loaded" until `AllStarted == true`

## SimulationWorld — World Clock + Reset

- Inherits from `CLOiSimPlugin` with `_type = WORLD`
- Registers a Clock TX channel and a Control client channel
- Broadcasts simulation time and reset signals

## Key Patterns

- `BridgeManager` is a singleton created in `Main.Awake()`
- All port allocation/deallocation is thread-safe
- `SimulationService` disposes cleanly: removes services, stops server, nulls reference
- New services must extend `WebSocketBehavior` and be registered via `wsServer.AddWebSocketService<T>(path, factory)`
- Use `SimulationControlService.Delimiter` when parsing multi-part control messages
