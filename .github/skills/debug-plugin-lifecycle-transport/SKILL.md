---
name: debug-plugin-lifecycle-transport
description: "Debug CLOiSim plugin startup, Started-event hangs, transport registration failures, BridgeManager port issues, request-handler failures, and teardown leaks. Use when: a plugin never starts, world/model load times out, ports are missing or duplicated, device transport stops flowing, request/reply handlers fail, or plugin destroy/reset behavior looks wrong."
argument-hint: "Describe the plugin, symptom, timeout, missing port, request failure, or teardown issue."
---

# Debug Plugin Lifecycle and Transport

Use this skill when the failure is in the bridge layer between devices and the external ROS 2 side, not in scene-only behavior. The goal is to identify the exact lifecycle stage or transport registration step that failed, then validate that slice with the cheapest discriminating check.

## When to Use

- World or model loading stalls on plugin startup
- A plugin never reaches `Started`
- `port_list` or `device_list` is missing expected entries
- A plugin binds ports but no data flows
- Service requests fail, time out, or return the wrong payload
- Destroy or reload paths leave ports allocated or threads alive

## Current Runtime Contract

### Plugin startup path

`Awake()` -> `SetCustomHandleRequestMessage()` -> `OnAwake()` -> `Start()` -> `OnPluginLoad()` -> name resolution -> `DelayedOnStart()` -> stagger by `_globalSequence` frames -> `OnStart()` -> `_thread.Start()` -> `IsStarted = true` -> `Started` event

### Load-side coordination

- `PluginStartTracker.Bind(...)` subscribes to all `CLOiSimPlugin` instances under the loaded root.
- `Main.LoadWorld()` and `Main.LoadModel()` wait up to 30 seconds for all plugins to report `Started`.
- If a plugin never completes `OnStart()`, the load path times out and the world/model does not finish loading.

### Transport registration path

- `RegisterTxDevice`, `RegisterRxDevice`, `RegisterServiceDevice`, and `RegisterClientDevice` all flow through `PrepareDevice()`.
- `PrepareDevice()` allocates the hash key and port via `BridgeManager.AllocateDevice(...)`, stores them in the plugin's local tracking lists, then initializes the corresponding NetMQ socket in `Transporter`.
- On destroy, the base class disposes threads, disposes transport, then deregisters the cached ports and hash keys.

## Diagnostic Workflow

### 1. Start from the concrete symptom

Pick one entry point:

- timeout while loading a world or model
- missing port in `port_list`
- missing device in `device_list`
- request handler failure
- duplicate allocation or stale port after reload

Do not start by reading unrelated plugin families. Step directly to the plugin instance named by the world, test, stack trace, or log.

### 2. Identify the failing lifecycle stage

Ask which stage the plugin reached:

1. `Awake()` never ran or failed early
2. `OnAwake()` ran, but `Start()` setup did not complete
3. `DelayedOnStart()` began, but `OnStart()` never completed
4. `OnStart()` completed, but transport registration or thread startup is wrong
5. plugin started, but service or data flow is broken later
6. destroy or reload leaked ports or handlers

Use the cheapest local evidence you already have:

- timeout message from `Main`
- `PluginStartTracker` progress count
- `BridgeManager.PrintAllocatedHistory()`
- WebSocket `port_list` / `device_list`
- one failing request path or message flow

### 3. Form one local hypothesis

Write one claim that can fail. Examples:

- `OnStart()` exits early because `RegisterTxDevice()` fails and no fallback path reports the failure.
- The plugin never fires `Started` because `OnStart()` throws before `_thread.Start()`.
- The request handler is wired, but the plugin-specific overload is not the one actually invoked.
- Teardown is re-entering with stale cached ports because deregistration did not clear the local lists.

Do not keep multiple speculative theories unless one nearby read is required to separate them.

### 4. Choose the cheapest discriminating check

Prefer checks in this order:

1. the exact failing load, request, or teardown behavior
2. the narrow EditMode test for the touched plugin
3. `port_list` / `device_list` or `BridgeManager` allocation history
4. a local compile of the touched plugin or test slice

`git diff` is not a transport or lifecycle check.

## Symptom-Driven Checks

### A. Load times out waiting for plugins

Focus on the plugin's `OnStart()` body.

Check:

- Does `OnAwake()` set `_type` correctly before registration?
- Does `OnStart()` always reach `yield return null`?
- Are `Register*Device(...)` return values checked, or does the plugin silently proceed after failure?
- Does the plugin depend on a device/component that can be null on this GameObject?
- Is `Started` being blocked because `OnStart()` throws or yields forever?

Why it matters:

- `Started` is fired only after `OnStart()` returns and `_thread.Start()` runs.
- The load path now has a 30-second timeout, so a hung plugin blocks the world/model load instead of hanging forever.

### B. Port missing, duplicated, or wrong

Focus on `PrepareDevice()` and `BridgeManager.AllocateDevice(...)`.

Check:

- `_modelName`, `_partsName`, `_subPartsName`, and control key compose the expected hash key.
- `SubPartsName` is set when multiple plugins under one model need disambiguation.
- The plugin registers the expected transport types for the feature under test.
- The lookup is checked through `port_list` or `BridgeManager` history, not guesswork.

Why it matters:

- The transport layer routes by hash key and port. Wrong naming or missing subparts can point the bridge at the wrong endpoint even when sockets bind successfully.

### C. Data path or request path is broken after startup

Focus on the specific thread or handler:

- `SenderThread` for TX paths
- `ReceiverThread` for RX paths
- `ServiceThread` plus request handler wiring for service calls

Check:

- The plugin added the correct thread type for each registered port.
- The device passed to `AddThread(...)` is the object the thread logic expects.
- The plugin-specific `HandleCustomRequestMessage(...)` signature matches the active overload.
- The plugin response helpers serialize the exact message expected by the bridge.

Why it matters:

- A plugin can reach `Started` and still be functionally broken if the wrong thread or handler path is wired.

### D. Reload or destroy leaks transport state

Focus on the base `OnDestroy()` path and plugin-specific teardown side effects.

Check:

- The plugin did not retain extra port state outside `_allocatedDevicePorts` and `_allocatedDeviceHashKeys`.
- Deregistration runs once and clears the local caches.
- Repeated destroy/reset paths are idempotent for the touched plugin.

Why it matters:

- Stale ports or hash keys create false duplicate-allocation failures on later loads.

## Cheap External Checks

Use these when the runtime is available:

```bash
echo '{"command":"port_list"}' | websocat ws://localhost:8080/control
echo '{"command":"device_list"}' | websocat ws://localhost:8080/control
ss -tlnp | grep -E '4915[2-9]|49[2-9][0-9]{2}|[5-6][0-9]{4}'
```

Use them to confirm actual registration state, not as a substitute for finding the deciding code path.

## Known Repo-Specific Risks

- Startup success is only valid after `OnStart()` completes. Do not treat a partially-run `OnStart()` as success.
- `PluginStartTracker` is a coordination layer, not the root cause. Step through it to the plugin that failed to report `Started`.
- `BridgeManager` query methods are expected to return copies for service serialization safety.
- Repeated teardown must clear the plugin's cached allocated ports and hash keys to stay idempotent.
- EditMode request-handler tests must bind the exact `HandleCustomRequestMessage(in string, in cloisim.msgs.Any, ref DeviceMessage)` signature.

## Completion Checklist

- [ ] I identified the exact plugin and failing stage.
- [ ] I reduced the problem to one lifecycle or transport hypothesis.
- [ ] I used one concrete check that could disprove it.
- [ ] I stepped past `PluginStartTracker` or websocket wiring to the code that actually decides the behavior.
- [ ] I verified naming inputs used for hash-key allocation.
- [ ] I checked thread or handler wiring when startup succeeded but behavior still failed.
- [ ] I treated destroy and reload as part of the same contract, not as an afterthought.