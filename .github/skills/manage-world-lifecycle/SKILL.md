---
name: manage-world-lifecycle
description: "Understand and extend the CLOiSim world load → plugin start → reset → save lifecycle. Use when: debugging world loading failures, troubleshooting plugin startup coordination, fixing reset behavior, extending the world save pipeline, diagnosing physics mode switching issues, understanding PluginStartTracker events."
---

# Manage World Lifecycle

Reference guide for the complete CLOiSim simulation lifecycle: world loading, plugin startup coordination, runtime reset, and world state persistence.

## When to Use

- Debugging why a world fails to load or hangs during plugin startup
- Adding a new component that needs to participate in the reset cycle
- Understanding the `PluginStartTracker` event system
- Extending `WorldSaver` to persist new element types
- Diagnosing physics simulation mode switching issues
- Coordinating multi-plugin dependencies during startup

## Lifecycle Overview

```
┌─────────────────────────────────────────────────────────┐
│ LOAD                                                     │
│   Main.LoadWorld()                                       │
│   ├─ SDF.Root.DoParse() → parse SDF XML                │
│   ├─ Physics.simulationMode = Script                    │
│   ├─ Import.Loader.Start() → create GameObjects         │
│   ├─ Wait: worldRoot.childCount > 0                     │
│   ├─ PluginStartTracker.Bind(worldRoot)                 │
│   ├─ Physics.SyncTransforms()                           │
│   ├─ Physics.simulationMode = FixedUpdate               │
│   ├─ ResetWorld() [initial reset]                       │
│   └─ Wait: AllStarted == true                           │
├─────────────────────────────────────────────────────────┤
│ RUN                                                      │
│   Normal simulation loop                                 │
│   ├─ FixedUpdate: physics                               │
│   ├─ Update: sensors, UI                                │
│   └─ Plugins: TX/RX threads running                     │
├─────────────────────────────────────────────────────────┤
│ RESET (Ctrl+R)                                           │
│   Main.ResetSimulation()                                 │
│   ├─ SensorRenderManager.Pause()                        │
│   ├─ SimulationWorld.SignalReset()                       │
│   ├─ ResetWorld()                                       │
│   │  ├─ Helper.Base.Reset() for each helper             │
│   │  ├─ Device.Reset() for each device                  │
│   │  └─ CLOiSimPlugin.Reset() for each plugin           │
│   ├─ Wait 0.1s                                          │
│   └─ SensorRenderManager.Resume()                       │
├─────────────────────────────────────────────────────────┤
│ FULL RELOAD (Ctrl+Shift+R)                               │
│   Destroys world root, reloads from SDF                  │
├─────────────────────────────────────────────────────────┤
│ SAVE (Ctrl+Shift+S)                                      │
│   WorldSaver.Update()                                    │
│   ├─ ClearAllComments()                                 │
│   ├─ UpdateGUI() → camera pose                          │
│   ├─ UpdateModels() → model poses, static flags         │
│   └─ UpdateRoads() → road spline points                 │
└─────────────────────────────────────────────────────────┘
```

## Plugin Startup Coordination

### PluginStartTracker

Monitors all `CLOiSimPlugin` instances under a loaded root and fires events as they start:

```csharp
// Bind to a loaded world/model root
var tracker = new PluginStartTracker();
tracker.ProgressChanged += OnPluginProgressChanged;   // (started, total)
tracker.AllStartedEvent += OnAllPluginsStarted;        // fires once
tracker.Bind(worldRoot);

// ProgressChanged fires for each plugin that completes OnStart()
// AllStartedEvent fires when StartedCount == TotalCount
```

### Plugin Startup Flow

Each `CLOiSimPlugin` follows this internal sequence:

```
Awake()
  └─ OnAwake() [abstract] — set _type, cache components

Start() [coroutine]
  └─ OnStart() [abstract] — register ports, add threads
     ├─ RegisterTxDevice() / RegisterRxDevice()
     ├─ RegisterServiceDevice() / RegisterClientDevice()
     ├─ AddThread(port, threadFunction, device)
     └─ yield return null

  └─ Raises Started event → PluginStartTracker.OnPluginStarted()
```

### Waiting for All Plugins

```csharp
// In Main.cs — world is not considered loaded until all plugins start
private void OnAllPluginsStarted()
{
	_pluginAllStarted = true;
	BridgeManager.PrintAllocatedHistory();
}

// LoadWorld() waits in a while loop:
while (!_pluginAllStarted)
	yield return null;
```

## Reset Cascade

`ResetWorld()` resets components in three phases:

```csharp
void ResetModel(GameObject targetObject)
{
	// Phase 1: Reset poses (Helper.Base components)
	foreach (var helper in targetObject.GetComponentsInChildren<Helper.Base>())
		helper.Reset();
	// → Restores initial pose via PoseControl
	// → Rewinds animation state

	// Phase 2: Reset devices
	foreach (var device in targetObject.GetComponentsInChildren<Device>())
		device.Reset();
	// → Clears message queues
	// → Resets synthetic timestamps

	// Phase 3: Reset plugins
	foreach (var plugin in targetObject.GetComponentsInChildren<CLOiSimPlugin>())
		plugin.Reset();
	// → Calls OnReset() [virtual]
	// → Plugin-specific state restoration
}
```

### SimulationWorld Reset Signal

`SimulationWorld` sends a reset signal to the external ROS bridge:

```csharp
// Triggered by Main.ResetSimulation()
SimulationWorld.SignalReset();
// → Sets _signalReset = true
// → ClientThread sends messages.Param { "reset_simulation": true }
// → External bridge receives and resets its state
```

## World Save Pipeline

`WorldSaver` serializes current scene state back to the loaded SDF XML:

```csharp
// UpdateModels() for each model in scene:
// 1. Remove stale <include> elements
// 2. Remove <model> elements not present in scene
// 3. For each child in WorldRoot:
//    - Get Helper.Model → original model path
//    - Create/update <include> or <model> element
//    - Set <static> from GameObject.isStatic
//    - Set <pose> via Unity2SDF.Pose(position, rotation)

// UpdateRoads() for each road:
// 1. Remove all <road> elements
// 2. Recreate from Main.RoadsRoot children
// 3. Save spline knots as <point> elements

// UpdateGUI() for camera:
// 1. Get Camera.main transform
// 2. Convert to SDF pose via Unity2SDF.Pose()
// 3. Write to <gui><camera><pose>
```

## Physics Mode Switching

During world loading, physics must be paused to prevent objects from falling while being constructed:

```csharp
// Before construction:
Physics.simulationMode = SimulationMode.Script;
// → Physics does NOT run automatically
// → Objects can be positioned without falling

// After construction:
Physics.SyncTransforms();
// → Syncs all manually-set transforms to physics engine
Physics.simulationMode = SimulationMode.FixedUpdate;
// → Normal physics simulation resumes
```

## Procedure: Adding a New Resetable Component

To make a new component participate in the reset cycle:

1. **If it's a Helper** — inherit from `Helper.Base` and override `Reset()`:
   ```csharp
   public class MyHelper : Base
   {
       private float _initialValue;

       new protected void Start()
       {
           base.Start();
           _initialValue = currentValue;
       }

       public new void Reset()
       {
           base.Reset(); // Resets pose
           currentValue = _initialValue;
       }
   }
   ```

2. **If it's a Device** — override `OnReset()` in your `Device` subclass:
   ```csharp
   protected override void OnReset()
   {
       // Clear accumulated state
       _accumulatedData = 0;
   }
   ```

3. **If it's a Plugin** — override `OnReset()` in your `CLOiSimPlugin` subclass:
   ```csharp
   protected override void OnReset()
   {
       // Reset plugin-specific state
       _commandBuffer.Clear();
   }
   ```

## Procedure: Extending World Save

To save a new element type in `WorldSaver`:

1. Add an `Update*()` method:
   ```csharp
   private void UpdateMyElements()
   {
       // Remove stale elements
       var staleNodes = _worldNode.SelectNodes("my_element");
       foreach (XmlNode node in staleNodes)
           _worldNode.RemoveChild(node);

       // Add current elements from scene
       foreach (var element in FindMyElements())
       {
           var xmlNode = _doc.CreateElement("my_element");
           xmlNode.SetAttribute("name", element.Name);

           // Convert pose to SDF
           var sdfPose = Unity2SDF.Pose(
               element.transform.position, element.transform.rotation);
           // Write pose to XML...

           _worldNode.AppendChild(xmlNode);
       }
   }
   ```

2. Call it from `Update()`:
   ```csharp
   public void Update()
   {
       ClearAllComments();
       UpdateGUI();
       UpdateModels();
       UpdateRoads();
       UpdateMyElements(); // Add here
   }
   ```

## Critical Pitfalls

| Pitfall | Details | Mitigation |
|---------|---------|------------|
| **Plugin start order is undefined** | Plugins start in parallel via background threads. Do NOT assume plugin A starts before plugin B. | Use `AllStartedEvent` to synchronize cross-plugin dependencies. |
| **Reset called during initial load** | `ResetWorld()` runs once during `LoadWorld()` before plugins finish starting. | Ensure `OnReset()` handles being called before `OnStart()` completes. |
| **Stale plugin references** | `PluginStartTracker.Bind()` walks the scene once at load time. Dynamically added plugins are NOT tracked. | Rebind if plugins are added post-load. |
| **Transform sync timing** | Manual transform changes during runtime are not synced to physics automatically (`autoSyncTransforms` is off). | Call `Physics.SyncTransforms()` after manual teleport operations. |
| **Physics mode must bracket construction** | Objects fall during construction if physics is `FixedUpdate`. | Set `SimulationMode.Script` before creating objects, restore after. |
| **SensorRenderManager must pause during reset** | Sensors continue rendering during reset, producing stale/corrupt data. | Always `Pause()` before reset, `Resume()` after. |
| **WorldSaver removes XML comments** | `ClearAllComments()` strips all `<!-- -->` from the saved file. | Don't rely on XML comments surviving save cycles. |

## Key Events Reference

| Event | Source | Fired When | Listeners |
|-------|--------|-----------|-----------|
| `CLOiSimPlugin.Started` | Each plugin | `OnStart()` coroutine completes | `PluginStartTracker` |
| `PluginStartTracker.ProgressChanged(int, int)` | Tracker | Each plugin starts | `Main` → UI progress |
| `PluginStartTracker.AllStartedEvent` | Tracker | All plugins started | `Main` → finalize load |
