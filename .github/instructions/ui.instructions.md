---
applyTo: "Assets/Scripts/UI/**"
---

# UI Development Instructions

The UI layer uses both **Unity UI Toolkit** (for the main HUD) and **legacy TextMeshPro/UGUI** (for the info overlay).

## UI Toolkit — UIController

`UIController.cs` manages the main HUD via UI Toolkit:

- Gets `UIDocument` component in `Awake()`, extracts `rootVisualElement`
- Query elements by name: `_rootVisualElement.Q<Button>("CameraView")`, `Q<Toggle>(...)`, `Q<TextField>(...)`, `Q<Label>(...)`, `Q<EnumField>(...)`, `Q<ScrollView>(...)`, `Q<VisualElement>(...)`
- Event registration patterns:
  ```csharp
  button.clickable.clicked += () => { ... };
  toggle.RegisterValueChangedCallback(x => { ... });
  field.RegisterCallback<FocusOutEvent>(evt => { ... });
  element.RegisterCallback<MouseEnterEvent>(evt => { ... });
  element.RegisterCallback<MouseLeaveEvent>(evt => { ... });
  ```
- Styling: direct style manipulation
  ```csharp
  element.style.display = DisplayStyle.None;
  element.style.visibility = Visibility.Hidden;
  element.style.backgroundColor = new Color(...);
  ```
- CSS class toggling:
  ```csharp
  element.ToggleInClassList("selected");
  element.EnableInClassList("selected", isSelected);
  element.AddToClassList("active");
  element.RemoveFromClassList("active");
  ```

## Legacy UGUI — InfoDisplay

`InfoDisplay.cs` (partial class, split with `InfoDisplay.FPS.cs`) uses TextMeshPro:

- `TMP_InputField` found via `GetComponentsInChildren<TMP_InputField>()`, matched by `.name`
- FPS: frame-counting approach with 1-second update period
- `EventTrigger` for click events on `TMP_Text` labels

## Camera Control Hierarchy

```
CameraControl (abstract MonoBehaviour)
├── PerspectiveCameraControl   — 3D fly-cam (WASD + mouse wheel zoom)
├── OrthographicCameraControl  — 2D pan (WASD pan + mouse wheel ortho size)
└── FollowingCamera            — Separate class (not subclass), tracks a target
```

- `LateUpdate()` loop handles input: Space for vertical lock, mouse orbiting, keyboard movement
- Abstract methods: `HandleMouseWheelScroll()`, `HandleKeyboardDirection()`
- Input System: `Keyboard.current[Key.X]` and `Mouse.current` (not legacy `Input`)
- Camera transitions: `StartCameraChange(Pose)` — coroutine-based smooth lerp
- Raycasting: `_targetLayerMask = LayerMask.GetMask("Default")`

## Key Rules

- Do not mix UI Toolkit and UGUI in the same component
- Use New Input System APIs (`Keyboard.current`, `Mouse.current`), never legacy `Input`
- `UIController` references the `Main Canvas` child under the `UI` root object — do not rename
- Camera controls block when `_blockControl` is set (e.g., during UI text input focus)
