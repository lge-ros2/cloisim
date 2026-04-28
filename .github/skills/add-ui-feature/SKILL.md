---
name: add-ui-feature
description: "Add a new UI panel, control, or overlay to CLOiSim's HUD. Use when: adding a new button or panel to UIController, creating a new camera mode, adding an info overlay."
---

# Add a New UI Feature

Procedure for adding UI elements to CLOiSim's HUD using Unity UI Toolkit or extending camera controls.

## When to Use

- Adding a new button, toggle, or panel to the main HUD
- Creating a new info display or overlay
- Adding a new camera view mode
- Extending the following target list or model importer UI

## UI Architecture

```
UI (scene root)
├── Main Canvas (UIDocument + UIController)
│   └── UI Toolkit visual tree (UXML)
├── InfoDisplay (legacy TextMeshPro overlay)
└── FollowingTargetList
```

**Two UI systems in use:**
- **UI Toolkit** — main HUD (`UIController.cs`): buttons, toggles, text fields, labels
- **Legacy UGUI/TextMeshPro** — info overlay (`InfoDisplay.cs`): FPS counter, status text

Do not mix UI Toolkit and UGUI in the same component.

## Procedure

### Option A: Add to Main HUD (UI Toolkit)

#### 1. Add Element to UXML

Edit the UXML document (referenced by the `UIDocument` component on the `Main Canvas` object). Add your element:

```xml
<ui:Button name="MyFeature" text="My Feature" class="toolbar-button" />
```

#### 2. Wire in UIController

Edit `Assets/Scripts/UI/UIController.cs`:

```csharp
// Add field
private Button _buttonMyFeature = null;

// In Start(), after other button wiring:
_buttonMyFeature = _rootVisualElement.Q<Button>("MyFeature");
_buttonMyFeature.clickable.clicked += () => OnMyFeatureClicked();

// Optionally add hover effects:
_buttonMyFeature.RegisterCallback<MouseEnterEvent>(
    delegate { ChangeBackground(ref _buttonMyFeature, Color.gray); });
_buttonMyFeature.RegisterCallback<MouseLeaveEvent>(
    delegate { ChangeBackground(ref _buttonMyFeature, Color.clear); });
```

#### 3. Implement the Handler

```csharp
private void OnMyFeatureClicked()
{
    _buttonMyFeature.ToggleInClassList("selected");
    // Interact with Main singleton:
    Main.Instance.MyNewMethod();
}
```

#### UI Toolkit Query Patterns

```csharp
// Query by name and type:
_rootVisualElement.Q<Button>("CameraView")
_rootVisualElement.Q<Toggle>("LockVerticalMoving")
_rootVisualElement.Q<TextField>("ScaleField")
_rootVisualElement.Q<Label>("StatusMessage")
_rootVisualElement.Q<EnumField>("MyEnum")
_rootVisualElement.Q<ScrollView>("MyList")
_rootVisualElement.Q<VisualElement>("MyContainer")
```

#### Event Registration Patterns

```csharp
// Click
button.clickable.clicked += () => { ... };

// Value change
toggle.RegisterValueChangedCallback(x => DoSomething(x.newValue));

// Focus events
textField.RegisterCallback<FocusOutEvent>(OnFocusOut);

// Mouse events
element.RegisterCallback<MouseEnterEvent>(delegate { ... });
element.RegisterCallback<MouseLeaveEvent>(delegate { ... });
```

#### Styling Patterns

```csharp
// Visibility
element.style.display = DisplayStyle.None;     // hidden, no layout
element.style.display = DisplayStyle.Flex;      // visible
element.style.visibility = Visibility.Hidden;   // hidden, keeps layout

// Background color
element.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 1f);

// CSS class toggling
element.ToggleInClassList("selected");
element.EnableInClassList("active", isActive);
element.AddToClassList("highlight");
element.RemoveFromClassList("highlight");
```

### Option B: Add a New Camera Mode

#### 1. Create CameraControl Subclass

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class MyCameraControl : CameraControl
{
    protected override void HandleMouseWheelScroll(in float value)
    {
        // Custom zoom behavior
    }

    protected override void HandleKeyboardDirection(in float duration)
    {
        // Custom movement (WASD)
    }
}
```

#### 2. Register in Main.cs

Camera switching is done in `Main.cs` via `SetCameraPerspective()` / `SetCameraOrthographic()` pattern. Add your mode similarly.

### Option C: Status Messages

Use the existing status message system:

```csharp
// From anywhere with access to Main:
Main.UIController?.SetStatusMessage("Loading model...");
Main.UIController?.SetWarningMessage("Warning text");
Main.UIController?.SetErrorMessage("Error text");
Main.UIController?.ClearMessage();
```

## Input System

Use **New Input System** APIs only:

```csharp
// Keyboard
if (Keyboard.current[Key.Space].wasPressedThisFrame) { ... }
if (Keyboard.current[Key.LeftCtrl].isPressed) { ... }

// Mouse
var scrollDelta = Mouse.current.scroll.ReadValue().y;
var mousePosition = Mouse.current.position.ReadValue();
if (Mouse.current.leftButton.wasPressedThisFrame) { ... }
```

Never use legacy `Input.GetKey()`, `Input.GetAxis()`, etc.

## Key Rules

- The `UI` root object and `Main Canvas` child must not be renamed
- Camera controls set `_blockControl = true` when UI text input has focus
- `UIController` accesses singletons via `Main.Instance`, `Main.ObjectSpawning`, etc.
- Use `LateUpdate()` for camera control input processing

## Checklist

- [ ] Element added to UXML document
- [ ] Field declared and queried in `UIController.Start()`
- [ ] Event handler registered
- [ ] Uses New Input System (`Keyboard.current`, `Mouse.current`)
- [ ] Does not mix UI Toolkit and UGUI in same component
- [ ] Does not rename `UI` or `Main Canvas` scene objects
- [ ] License header on new files
- [ ] Tabs for indentation, Allman braces
