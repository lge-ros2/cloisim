---
applyTo: "Assets/Scripts/Tools/SDF/**"
---

# SDF Pipeline Instructions

The SDF pipeline converts XML world/model files into Unity scenes through three sequential stages: **Parse → Import → Implement**, with **Helper** MonoBehaviours for runtime state.

## Pipeline Stages

### 1. Parser (`Tools/SDF/Parser/`)
- External `com.lge-ros2.sdformat` package handles XML → C# domain objects
- `RootLoader` orchestrates: loads XML, resolves `<include>` nodes recursively, converts URIs to absolute paths, invokes `SdfParser.Parse()`
- Model include resolution uses `_resourceModelTable` populated from `model.config` files across `CLOISIM_MODEL_PATH` directories
- `<include merge="true">` flattens children into parent scope; non-merge adds `is_nested="true"` attribute

### 2. Importer (`Tools/SDF/Import/`)
- `Import.Base` (abstract) defines virtual methods per SDF element
- `Import.Loader` (concrete, partial class) is split across files: `Import.World.cs`, `Import.Model.cs`, `Import.Link.cs`, `Import.Joint.cs`, `Import.Sensor.cs`, `Import.Plugin.cs`, etc.
- **Deferred processing**: joints, grippers, and plugins are stored during model import but applied later (joints need both links to exist; plugins need full articulation hierarchy)
- Orchestration order in `Base.Start()`: ImportWorld → ImportModels → deferred joints → deferred grippers → ImportActors → SpecifyPose → deferred plugins

### 3. Implementer (`Tools/SDF/Implement/`)
- Static extension methods that create low-level Unity components
- `Implement.Sensor`: `Add*()` methods create child GameObjects with `SensorDevices.*` components
- `Implement.Joint`: `MakeJoint()` dispatches by `JointType` to `MakeRevoluteJoint()`, `MakePrismaticJoint()`, `MakeFixedJoint()`, `MakeBallJoint()`, `MakeContinuousJoint()`
- `Implement.Geometry`: dispatches to `MeshLoader` (Assimp) for external meshes, `ProceduralMesh` for primitives
- `Implement.Collision`: VHACD convex decomposition for dynamic, merged mesh colliders for static; prefers native primitive colliders when possible

### 4. Helpers (`Tools/SDF/Helper/`)
- `Helper.Base` — stores SDF `Pose`, `PoseRelativeTo`, manages `PoseControl` frame list, provides `ResetPose()`
- `Helper.Model` — stores `modelNameInPath`, `isStatic`, `isNested`; creates `ArticulationBody`/`Rigidbody` on root
- `Helper.Link` — stores joint metadata, axis limits, mimic constraints; manages self-collision ignoring
- `Helper.Visual`, `Helper.Collision` — tag-bearing components for visual/collision GameObjects

## Coordinate Conversion

SDF (right-hand: X-forward, Y-left, Z-up) → Unity (left-hand: X-right, Y-up, Z-forward):

```
Unity.X = -SDF.Y
Unity.Y =  SDF.Z
Unity.Z =  SDF.X
```

Use `SDF2Unity` for SDF→Unity, `Unity2SDF` for Unity→SDF. Never manually swap axes.

## Adding a New Sensor Type

1. **Device** — create `Assets/Scripts/Devices/MyNewSensor.cs` inheriting from `Device`
2. **Implement** — add `AddMyNewSensor()` extension method in `Implement.Sensor.cs`
3. **Import** — add case in `Import.Sensor.cs` type-switch dispatching to the implement method
4. **Plugin** — create `Assets/Scripts/CLOiSimPlugins/MyNewSensorPlugin.cs` inheriting from `CLOiSimPlugin`
5. **SDFormat package** — ensure the `com.lge-ros2.sdformat` package supports the sensor type

## Adding a New SDF Element

1. Extend `com.lge-ros2.sdformat` package with a new domain class
2. Add virtual method in `Import.Base.Common.cs`, override in a new `Import.Loader` partial class file
3. Add static extension methods in `Implement` namespace
4. If runtime state needed, create `Helper.*` MonoBehaviour inheriting from `Helper.Base`
5. Wire into orchestration in `Base.Start()` or appropriate parent import method

## Key Rules

- All changes must flow through the parse → import → implement pipeline; do not bypass with scene-only logic
- `ArticulationBody` components are kept disabled during import to prevent physics interference
- Pose application (`SpecifyPose()`) runs after all GameObjects are created but before physics
- Nested model pose resolution: searches within nearest enclosing nested model first, then falls back to root model scope
- `Type.GetType(pluginLibraryName)` resolves plugin classes by name from SDF `filename` attribute
