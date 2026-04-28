---
name: add-sdf-element
description: "Extend the SDF parse → import → implement pipeline with a new element type. Use when: adding support for a new SDF XML element, extending world/model import, creating new Helper MonoBehaviours."
---

# Add a New SDF Element

Procedure for adding support for a new SDF XML element through the full pipeline: Parser → Importer → Implementer → Helper.

## When to Use

- Adding a new SDF element type (e.g., `<heightmap>`, `<population>`, `<atmosphere>`)
- Extending an existing element with new child elements
- Creating a new category of scene object that needs runtime state tracking

## Architecture

```
SDFormat package (Parser)  →  Import.Loader (Importer)  →  Implement.* (Implementer)
        ↓                            ↓                            ↓
  C# domain class           Unity GameObjects created    Components/colliders/renderers
                                     ↓
                              Helper.* MonoBehaviour (runtime state)
```

## Procedure

### 1. Extend the SDFormat Parser Package

The parser lives in the external `com.lge-ros2.sdformat` Unity package. Add or extend a domain class:

```csharp
// In the SDFormat package:
namespace SDFormat
{
    public class MyElement
    {
        public string Name { get; set; }
        public Math.Pose3d RawPose { get; set; }
        public string PoseRelativeTo { get; set; }
        // Add fields matching SDF XML attributes/children
    }
}
```

Ensure the parser populates this class from the corresponding XML element. If the element is a child of `<model>`, `<link>`, `<world>`, etc., add a collection property to the parent:

```csharp
// In the parent class (e.g., Model):
public IReadOnlyList<MyElement> MyElements => _myElements;
```

### 2. Add Virtual Method in Import.Base.Common

Edit `Assets/Scripts/Tools/SDF/Import/Import.Base.Common.cs`:

```csharp
protected virtual object ImportMyElement(in MyElement element, in object parentObject)
{
    PrintNotImported(MethodBase.GetCurrentMethod().Name, element.Name);
    return null;
}
```

If the element needs post-processing (after children are created):

```csharp
protected virtual void AfterImportMyElement(in MyElement element, in object targetObject)
{
    PrintNotImported(MethodBase.GetCurrentMethod().Name, element.Name);
}
```

### 3. Add Iteration in Import.Base

Edit `Assets/Scripts/Tools/SDF/Import/Import.Base.cs` — add a batch import method:

```csharp
private void ImportMyElements(IReadOnlyList<MyElement> items, in object parentObject)
{
    foreach (var item in items)
    {
        var createdObject = ImportMyElement(item, parentObject);
        // If the element has sub-elements:
        // ImportGeometry(item.Geom, createdObject);
        // StorePlugins(item.Plugins, createdObject);
    }
}
```

Wire it into the appropriate parent import method (e.g., in `ImportLinks()`, `ImportModels()`, or `Start()`).

### 4. Create Loader Override

Create `Assets/Scripts/Tools/SDF/Import/Import.MyElement.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
    using Implement;
    namespace Import
    {
        public partial class Loader : Base
        {
            protected override object ImportMyElement(in MyElement element, in object parentObject)
            {
                var targetObject = parentObject as UE.GameObject;

                var newObject = targetObject.CreateMyElement(element);

                return newObject;
            }
        }
    }
}
```

### 5. Create Implement Extension Methods

Create `Assets/Scripts/Tools/SDF/Implement/Implement.MyElement.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
    namespace Implement
    {
        public static class MyElement
        {
            public static UE.GameObject CreateMyElement(
                this UE.GameObject targetObject, in SDFormat.MyElement element)
            {
                var newObject = new UE.GameObject(element.Name);
                newObject.tag = "MyTag";  // Use existing tag or request a new one

                newObject.transform.SetParent(targetObject.transform, false);

                // Apply SDF pose
                if (element.RawPose != null)
                {
                    var (pos, rot) = element.RawPose.ToUnity();
                    newObject.transform.localPosition = pos;
                    newObject.transform.localRotation = rot;
                }

                // Add Helper for runtime state
                var helper = newObject.AddComponent<Helper.MyElement>();
                helper.Pose = element.RawPose;
                helper.PoseRelativeTo = element.PoseRelativeTo;

                // Create Unity components (renderers, colliders, etc.)
                // ...

                return newObject;
            }
        }
    }
}
```

### 6. Create Helper MonoBehaviour (If Needed)

Create `Assets/Scripts/Tools/SDF/Helper/MyElement.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
    namespace Helper
    {
        public class MyElement : Base
        {
            [UE.Header("SDF Properties")]
            public string myProperty;

            new protected void Awake()
            {
                base.Awake();
                // Element-specific initialization
            }

            new protected void Start()
            {
                base.Start();
                // Element-specific startup
            }

            // Override if this element has runtime state to reset
            // public new void Reset()
            // {
            //     base.Reset();
            // }
        }
    }
}
```

### 7. Wire Into Orchestration

Depending on when the element needs to be processed, wire it into the orchestration flow in `Import.Base.cs`:

**Immediate processing** (during parent import):
```csharp
// In ImportLinks() or ImportModels():
ImportMyElements(model.MyElements, newModelObject);
```

**Deferred processing** (needs other elements to exist first, like joints):
```csharp
// Store during import:
protected void StoreMyElements(IReadOnlyList<MyElement> items, in object parentObject)
{
    foreach (var item in items)
    {
        _myElementObjectList.Add(item, parentObject);
    }
}

// Process later in Start():
private void ImportDeferredMyElements()
{
    foreach (var item in _myElementObjectList)
    {
        ImportMyElement(item.Key, item.Value);
    }
}
```

The orchestration order in `Base.Start()` is:
1. `ImportWorld` → `ImportModels` (creates GameObjects)
2. Deferred joints (needs both parent/child links)
3. Deferred grippers
4. `ImportActors`
5. `SpecifyPose` (applies all poses)
6. Deferred plugins (needs full articulation hierarchy)

## Key Patterns

- **Coordinate conversion**: Always use `SDF2Unity` methods (`ToUnity()`, `Position()`, `Rotation()`) — never manually swap axes
- **Partial class**: `Import.Loader` is a partial class split across files. Each new element gets its own `Import.*.cs` file
- **Extension methods**: Implementers are static extension methods on `GameObject` in the `SDFormat.Implement` namespace
- **Helper.Base inheritance**: Always call `base.Awake()` and `base.Start()` — they handle pose control and root model discovery
- **Tags**: Use existing Unity tags (`Model`, `Link`, `Visual`, `Collision`, `Sensor`, `Light`, `Actor`, `Marker`, `Props`, `Geometry`, `Road`). Adding new tags requires editing `ProjectSettings/TagManager.asset`
- **ArticulationBody disabled**: During import, `ArticulationBody` components are disabled. `SpecifyPose()` re-enables them after all poses are applied

## Checklist

- [ ] SDFormat package domain class created/extended
- [ ] Virtual method added in `Import.Base.Common.cs`
- [ ] Iteration method in `Import.Base.cs`
- [ ] Loader override in new `Import.MyElement.cs` partial class file
- [ ] Implement extension methods in `Implement.MyElement.cs`
- [ ] Helper MonoBehaviour (if runtime state needed) in `Helper/MyElement.cs`
- [ ] Wired into orchestration at correct stage
- [ ] Coordinate conversion uses `SDF2Unity` methods
- [ ] License header on all new files
- [ ] Uses tabs for indentation, Allman braces
