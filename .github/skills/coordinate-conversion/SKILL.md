---
name: coordinate-conversion
description: "Convert between SDF (right-hand) and Unity (left-hand) coordinate systems. Use when: implementing pose transforms, converting sensor data between coordinate frames, debugging position/rotation mismatches."
---

# Coordinate System Conversion Reference

Quick reference for converting between SDF and Unity coordinate systems in CLOiSim.

## When to Use

- Converting positions, rotations, or scales between SDF and Unity
- Debugging pose mismatches after import
- Implementing sensor data output in the correct frame
- Converting joint axes or velocities

## Coordinate Systems

**SDF (right-hand):** X-forward, Y-left, Z-up
**Unity (left-hand):** X-right, Y-up, Z-forward

## Conversion Formulas

### SDF → Unity (`SDF2Unity`)

```
Unity.X = -SDF.Y
Unity.Y =  SDF.Z
Unity.Z =  SDF.X
```

**Position:**
```csharp
// Static method:
Vector3 pos = SDF2Unity.Position(in double x, in double y, in double z);
// → new Vector3(-(float)y, (float)z, (float)x)

// Extension method:
Vector3 pos = sdfVector3d.ToUnity();
Vector3 pos = protobufVector3d.ToUnity();
```

**Rotation (quaternion):**
```csharp
// Static method:
Quaternion rot = SDF2Unity.Rotation(in double w, in double x, in double y, in double z);
// → new Quaternion((float)y, (float)-z, (float)-x, (float)w)

// Extension method:
Quaternion rot = sdfQuaterniond.ToUnity();
```

**Pose (position + rotation):**
```csharp
var (position, rotation) = sdfPose3d.ToUnity();
```

**Scale:**
```csharp
// Scale preserves absolute values but swaps axes:
Vector3 scale = SDF2Unity.Scalar(in double x, in double y, in double z);
// → Abs of Position mapping

// Signed scale (for mesh scaling):
Vector3 scale = SDF2Unity.Scale(sdfVector3d);
```

**Curve orientation (for roads/splines):**
```csharp
float angleDeg = SDF2Unity.CurveOrientation(in float radians);
float angleRad = SDF2Unity.CurveOrientationAngle(in float radians);
```

### Unity → SDF (`Unity2SDF`)

```
SDF.X =  Unity.Z
SDF.Y = -Unity.X
SDF.Z =  Unity.Y
```

**Position:**
```csharp
SDFormat.Math.Vector3d sdfPos = Unity2SDF.Position(in Vector3 value);
// → new Vector3d(value.z, -value.x, value.y)
```

**Rotation:**
```csharp
SDFormat.Math.Quaterniond sdfRot = Unity2SDF.Rotation(in Quaternion value);
// → new Quaterniond(value.w, -value.z, value.x, -value.y)
```

**Pose:**
```csharp
SDFormat.Math.Pose3d sdfPose = Unity2SDF.Pose(in Vector3 pos, in Quaternion rot);
```

**Direction reversal (for road/curve orientation):**
```csharp
Vector3 reversed = Unity2SDF.Direction.Reverse(in Vector3 value);  // negates
float curveDir = Unity2SDF.Direction.Curve(in float value);        // negates
```

**Joint prismatic direction:**
```csharp
float val = Unity2SDF.Direction.Joint.Prismatic(in float value, in Vector3 rotation);
// Negates if any rotation axis is 180°
```

## Common Patterns

### In Sensor Devices

Sensor data output typically needs Unity→SDF conversion before publishing:

```csharp
// Convert Unity position to SDF for protobuf message:
var sdfPosition = Unity2SDF.Position(transform.position);
_msg.Position.Set(sdfPosition.X, sdfPosition.Y, sdfPosition.Z);

// Or use SDF2Unity in reverse for the protobuf Vector3d.Set() extension:
// The Set() methods on protobuf types already expect Unity-space vectors
```

### In SDF Import

During import, always use `ToUnity()` extension methods:

```csharp
var (localPosition, localRotation) = sdfElement.RawPose.ToUnity();
newObject.transform.localPosition = localPosition;
newObject.transform.localRotation = localRotation;
```

### In Joint Axes

Joint axis directions are converted during import:

```csharp
var jointAxis = joint.Axis.Xyz.ToUnity().normalized;
// Spring reference angles need orientation conversion:
var springRef = SDF2Unity.CurveOrientation((float)joint.Axis.SpringReference);
```

## Color Conversion

```csharp
Color unityColor = sdfColor.ToUnity();
// → new Color(R, G, B, A) — no axis swapping, just type conversion

Color fromString = "0.5 0.5 0.5 1.0".ToColor();
```

## 2D Conversions

```csharp
Vector2 size = SDF2Unity.Size(sdfVector2d);   // direct mapping (X, Y)
Vector2 point = SDF2Unity.Point(sdfVector2d);  // swapped (Y, X)
```

## Critical Rules

1. **Never manually swap axes** — always use `SDF2Unity` / `Unity2SDF` methods
2. **Extension methods** are available on `SDFormat.Math.Vector3d`, `Quaterniond`, `Pose3d`, and `cloisim.msgs.Vector3d`
3. **Scale conversion** preserves signs for mesh scaling but uses absolute values for collider sizing
4. **Protobuf message `.Set()` extensions** handle conversion internally — check whether they expect Unity-space or SDF-space input before calling
5. **Partial classes**: `SDF2Unity` is split across `SDF2Unity.cs`, `SDF2Unity.Material.cs`, `SDF2Unity.Mesh.cs`, `SDF2Unity.Model.cs`
