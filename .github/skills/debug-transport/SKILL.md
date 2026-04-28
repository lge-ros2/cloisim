---
name: debug-transport
description: "Debug and troubleshoot the NetMQ transport layer, port allocation, and device communication. Use when: diagnosing connection failures, debugging message flow, troubleshooting port conflicts, verifying device registration."
---

# Debug Transport Issues

Reference guide for diagnosing and fixing issues in CLOiSim's NetMQ transport layer.

## When to Use

- Plugin fails to start or register its transport ports
- External ROS 2 bridge cannot connect to a device
- Messages are not being received or published
- Port allocation conflicts
- Device appears in `device_list` but data doesn't flow

## Transport Architecture

```
CLOiSimPlugin                    External Bridge (cloisim_ros)
    ↓                                      ↓
 Transporter                          connects to
    ├── Publisher  (PUB, bind)  ←─── Subscriber (SUB, connect)
    ├── Subscriber (SUB, bind) ←─── Publisher  (PUB, connect)
    ├── Responsor  (REP, bind) ←─── Requestor  (REQ, connect)
    └── Requestor  (REQ, bind) ←─── Responsor  (REP, connect)
```

All CLOiSim sockets **bind** on `tcp://*:{port}`. The bridge **connects**.

## Diagnostic Steps

### 1. Check Port Allocation

**Via WebSocket API:**
```bash
# Get all registered devices and ports
echo '{"command":"port_list"}' | websocat ws://localhost:8080/control

# Get device map (grouped by model/plugin/parts)
echo '{"command":"device_list"}' | websocat ws://localhost:8080/control
```

**Via code (Unity Console):**
```csharp
// BridgeManager logs all allocations after world load:
BridgeManager.PrintAllocatedHistory();

// Check if a specific port is allocated:
var port = BridgeManager.SearchSensorPort(hashKey);
```

**Port hash key format:** `{modelName}{partsName}{subPartsName}{controlKey}`

### 2. Check Plugin Startup

Every plugin fires a `Started` event. If a plugin doesn't start:

1. Check `PluginStartTracker` — it logs which plugins haven't started
2. Verify SDF `<plugin filename="ClassName">` matches the actual C# class name exactly
3. Check Unity Console for `"[Plugin] No plugin(xxx) exist"` warnings
4. Verify `OnStart()` coroutine completes (must `yield return null` at the end)

### 3. Verify Thread Health

```csharp
// In a plugin, check if threads are running:
// CLOiSimPluginThread manages background threads
// Each thread has a 50ms timeout join during cleanup

// Thread naming convention:
// Threads are added via AddThread(port, threadFunction, device)
// Thread functions: SenderThread, ReceiverThread, ServiceThread
```

### 4. Common Failure Modes

| Symptom | Cause | Fix |
|---------|-------|-----|
| `"Failed to Bind"` in Console | Port already in use | Kill previous CLOiSim instance; check `ss -tlnp \| grep 49152` |
| Plugin not in `device_list` | `filename` doesn't match class name | Fix SDF `<plugin filename="">` to match C# class name exactly |
| TX data not flowing | Device `Mode` mismatch | Ensure device `Mode` matches plugin thread type (TX_THREAD for SenderThread) |
| RX data not received | Hash mismatch | Verify subscriber hash matches publisher hash |
| Service timeout | `HandleCustomRequestMessage` not handling request | Add case for the request type in the plugin |
| `NullReferenceException` in thread | Device component not found | Check `GetComponent<>()` in `OnAwake()`, ensure device is on same GameObject |

### 5. Message Flow Debugging

**TX (publishing) flow:**
```
Device.GenerateMessage() or Device.EnqueueMessage()
  → DeviceMessageQueue.Enqueue()
  → SenderThread: DeviceMessageQueue.Dequeue()
  → Publisher.Publish(data, hash)
  → NetMQ PUB socket send
```

**RX (receiving) flow:**
```
NetMQ SUB socket recv
  → ReceiverThread: Subscriber.Subscribe()
  → Device.ProcessReceivedDeviceMessage()
```

**Service (request/reply) flow:**
```
NetMQ REP socket recv
  → ServiceThread: Responsor receives request
  → CLOiSimPlugin.HandleCustomRequestMessage()
  → Responsor sends response
```

### 6. Port Range

- Range: `49152–65535` (ephemeral ports)
- Allocation: sequential scan from 49152, checks `IPGlobalProperties.GetActiveTcpConnections()`
- Each device port is unique per CLOiSim instance

### 7. Hash-Based Message Tagging

Each transport connection uses a `ulong` hash for message routing:

```csharp
// Hash is computed from the device path:
// StdHash.Hash64(modelName + partsName + subPartsName + controlKey)

// Publisher tags each message with hash
// Subscriber filters by matching hash
// If hashes don't match, messages are silently dropped
```

### 8. Cleanup Verification

On `OnDestroy()`, plugins automatically:
1. Join all threads (50ms timeout per step, up to 500ms total)
2. Dispose `Transporter` (closes all NetMQ sockets)
3. Deallocate ports from `BridgeManager`

If ports aren't freed, check:
- `_thread.Dispose()` is being called
- `DeregisterDevice()` runs (logs removed hash keys)
- No orphaned `CLOiSimPlugin` instances (destroyed GameObjects should trigger `OnDestroy`)

## Network Debugging Commands

```bash
# Check which ports CLOiSim is binding:
ss -tlnp | grep -E '4915[2-9]|49[2-9][0-9]{2}|[5-6][0-9]{4}'

# Check WebSocket service:
curl -s http://localhost:8080/

# Test WebSocket commands:
echo '{"command":"fps"}' | websocat ws://localhost:8080/control
echo '{"command":"reset"}' | websocat ws://localhost:8080/control
```

## Adding Debug Logging to a Plugin

```csharp
protected override IEnumerator OnStart()
{
	if (RegisterTxDevice(out var portTx, "Data"))
	{
		Debug.Log($"[{name}] TX registered on port {portTx}");
		AddThread(portTx, SenderThread, _sensor);
	}
	else
	{
		Debug.LogError($"[{name}] Failed to register TX device");
	}

	yield return null;
}
```
