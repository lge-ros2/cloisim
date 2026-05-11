---
name: configure-joint-articulation
description: "Configure Unity ArticulationBody joints from SDF joint definitions. Use when: adding a new joint type, debugging joint axis alignment, tuning spring/damping/limits, fixing revolute or prismatic joint behavior, setting up parent-child articulation hierarchy."
---

# Configure Joint Articulation

Reference and procedure for configuring Unity `ArticulationBody` joints from SDF `<joint>` definitions. Covers the full import → implement flow, joint type dispatch, axis alignment, drive configuration, and limit handling.

## When to Use

- Adding support for a new SDF joint type (e.g., gearbox, screw)
- Debugging incorrect joint axis orientation or motion direction
- Tuning spring stiffness, damping, friction, or force limits
- Fixing revolute joint limit inversion issues
- Setting up parent-child `ArticulationBody` hierarchy for a new robot model
- Understanding how SDF joint parameters map to Unity `ArticulationDrive`

## Architecture

```
SDF <joint>
  │
  ├─ Import.Joint.cs: ImportJoint()
  │  ├─ Resolve parent/child links by name
  │  ├─ Create ArticulationBody on child link
  │  ├─ Call Implement.Joint methods
  │  └─ Store joint metadata on Helper.Link
  │
  └─ Implement.Joint.cs: MakeJoint()
     ├─ SetArticulationBodyRelationship() → parent-child hierarchy + anchor pose
     ├─ SetAnchor() → anchor position/rotation
     └─ Type dispatch:
        ├─ Revolute/Continuous → MakeRevoluteJoint()
        ├─ Prismatic → MakePrismaticJoint()
        ├─ Ball → MakeBallJoint()
        ├─ Universal/Revolute2 → MakeRevoluteJoint2()
        └─ Fixed → MakeFixedJoint()
```

## Joint Type Reference

| SDF Type | Unity Type | DOF | Notes |
|----------|-----------|-----|-------|
| `revolute` | SphericalJoint (1-DOF locked) | 1 rotational | Limits via `CurveOrientation` swap |
| `continuous` | SphericalJoint (1-DOF free) | 1 rotational | No limits, free rotation |
| `prismatic` | PrismaticJoint | 1 linear | Direct limit mapping (no swap) |
| `ball` | SphericalJoint (3-DOF free) | 3 rotational | All axes free |
| `universal` / `revolute2` | SphericalJoint (2-DOF) | 2 rotational | Two-axis via `MakeRevoluteJoint2()` |
| `fixed` | FixedJoint | 0 | Zero solver iterations |

## Procedure

### 1. Import Phase — Link Resolution and Hierarchy

`Import.Joint.cs` resolves parent/child links by name and establishes the `ArticulationBody` hierarchy:

```csharp
// Link resolution — searches by name in the model hierarchy
var linkObjectParent = targetObject.FindTransformByName(joint.ParentName);
var linkObjectChild = targetObject.FindTransformByName(joint.ChildName);

// Create ArticulationBody if not present
var articulationBodyChild = linkObjectChild.GetComponent<ArticulationBody>();
if (articulationBodyChild == null)
	articulationBodyChild = CreateArticulationBody(linkObjectChild.gameObject);

// Establish parent-child and compute anchor pose
var anchorPose = Implement.Joint.SetArticulationBodyRelationship(
	joint, linkObjectParent, linkObjectChild);
articulationBodyChild.SetAnchor(anchorPose);

// Configure joint type
articulationBodyChild.MakeJoint(joint);
```

### 2. Parent-Child Hierarchy Setup

`SetArticulationBodyRelationship()` decides hierarchy based on model scope:

```csharp
// Same model → child link becomes child of parent link
linkChild.SetParent(linkParent);

// Cross-model → child model root becomes child of parent link
modelTransformChild.SetParent(linkParent);

// Anchor pose starts at identity, then adds joint pose offset
var (jointPos, jointRot) = joint.RawPose.ToUnity();
anchorPose.position += jointPos;
anchorPose.rotation *= jointRot;
```

### 3. Joint Type Configuration

#### Revolute Joint

Uses `SphericalJoint` with two axes locked. Key pattern: **limit values are swapped** through `SDF2Unity.CurveOrientation()`:

```csharp
private static void MakeRevoluteJoint(this ArticulationBody body, in JointAxis axis)
{
	body.jointType = ArticulationJointType.SphericalJoint;
	body.linearDamping = 1.5f;
	body.angularDamping = 2f;

	var drive = new ArticulationDrive();

	// CRITICAL: Limits are swapped via CurveOrientation
	if (axis.HasJointLimits())
	{
		drive.lowerLimit = SDF2Unity.CurveOrientation((float)axis.Upper); // Upper → lower
		drive.upperLimit = SDF2Unity.CurveOrientation((float)axis.Lower); // Lower → upper
	}

	drive.forceLimit = double.IsInfinity(axis.Effort)
		? float.MaxValue : (float)axis.Effort;
	drive.stiffness = (float)axis.SpringStiffness;
	drive.target = SDF2Unity.CurveOrientation((float)axis.SpringReference);
	drive.damping = (float)axis.Damping;

	body.jointFriction = (float)axis.Friction;
	body.maxJointVelocity = (float)axis.MaxVelocity;

	// Axis alignment — determines which DOF is active
	var jointAxis = axis.Xyz.ToUnity().normalized;
	// Assign to xDrive/yDrive/zDrive based on dominant axis component
}
```

#### Axis Alignment Pattern

The dominant axis component determines which drive and DOF lock to use:

```csharp
var absX = Mathf.Abs(jointAxis.x);
var absY = Mathf.Abs(jointAxis.y);
var absZ = Mathf.Abs(jointAxis.z);

if (absX >= absY && absX >= absZ)
{
	// X-dominant: rotate anchor to align, use xDrive
	body.anchorRotation *= Quaternion.FromToRotation(Vector3.right, jointAxis);
	body.xDrive = drive;
	body.twistLock = hasLimits
		? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
	body.swingYLock = ArticulationDofLock.LockedMotion;
	body.swingZLock = ArticulationDofLock.LockedMotion;
}
else if (absY >= absX && absY >= absZ)
{
	// Y-dominant: use yDrive, lock twist and swingZ
	body.anchorRotation *= Quaternion.FromToRotation(Vector3.up, jointAxis);
	body.yDrive = drive;
	body.twistLock = ArticulationDofLock.LockedMotion;
	body.swingYLock = hasLimits
		? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
	body.swingZLock = ArticulationDofLock.LockedMotion;
}
else
{
	// Z-dominant: use zDrive, lock twist and swingY
	body.anchorRotation *= Quaternion.FromToRotation(Vector3.forward, jointAxis);
	body.zDrive = drive;
	body.twistLock = ArticulationDofLock.LockedMotion;
	body.swingYLock = ArticulationDofLock.LockedMotion;
	body.swingZLock = hasLimits
		? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
}
```

#### Prismatic Joint

Linear motion — limits are **not swapped** (unlike revolute):

```csharp
private static void MakePrismaticJoint(this ArticulationBody body, in JointAxis axis)
{
	body.jointType = ArticulationJointType.PrismaticJoint;

	var drive = new ArticulationDrive();

	// Direct limit mapping (no CurveOrientation swap)
	if (axis.HasJointLimits())
	{
		drive.lowerLimit = (float)axis.Lower;
		drive.upperLimit = (float)axis.Upper;
	}

	drive.target = (float)axis.SpringReference; // No CurveOrientation
	// ... same force/stiffness/damping pattern as revolute

	// Axis alignment uses linearLockX/Y/Z instead of twist/swing
	if (absX >= absY && absX >= absZ)
	{
		body.xDrive = drive;
		body.linearLockX = hasLimits
			? ArticulationDofLock.LimitedMotion : ArticulationDofLock.FreeMotion;
		body.linearLockY = ArticulationDofLock.LockedMotion;
		body.linearLockZ = ArticulationDofLock.LockedMotion;
	}
	// ... similar for Y and Z
}
```

#### Other Joint Types

```csharp
// Ball — 3-DOF free rotation
body.jointType = ArticulationJointType.SphericalJoint;
body.swingYLock = ArticulationDofLock.FreeMotion;
body.swingZLock = ArticulationDofLock.FreeMotion;
body.twistLock = ArticulationDofLock.FreeMotion;

// Fixed — no motion, zero solver iterations
body.jointType = ArticulationJointType.FixedJoint;
body.solverIterations = 0;
body.solverVelocityIterations = 0;

// Revolute2/Universal — apply axis1 then overlay axis2
MakeRevoluteJoint(body, axis1);
// Override second DOF with axis2 parameters
```

### 4. Helper.Link Metadata

After joint creation, the importer stores metadata on `Helper.Link` for runtime use:

```csharp
var linkHelper = linkObjectChild.GetComponent<Helper.Link>();
linkHelper.JointName = joint.Name;
linkHelper.JointParentLinkName = joint.ParentName;
linkHelper.JointChildLinkName = joint.ChildName;

// Axis orientation conversion differs by joint type
if (joint.Type == JointType.Prismatic)
	axisSpringReference = (float)joint.Axis.SpringReference; // Direct
else
	axisSpringReference = SDF2Unity.CurveOrientation(...);    // Curve transform

linkHelper.SetJointPoseTarget(axis1xyz, axisSpringReference,
	axis2xyz, axis2SpringReference);
```

### 5. Adding a New Joint Type

To add support for an unsupported joint type (e.g., `screw`, `gearbox`):

1. Add a case in `MakeJoint()` dispatch:
   ```csharp
   case JointType.Screw:
       body.MakeScrewJoint(joint.Axis);
       break;
   ```

2. Create the implementation method:
   ```csharp
   private static void MakeScrewJoint(this ArticulationBody body, in JointAxis axis)
   {
       // Choose appropriate ArticulationJointType
       // Configure drives, limits, damping
       // Handle axis alignment
   }
   ```

3. Decide limit semantics: revolute-style (swap via `CurveOrientation`) or prismatic-style (direct).

## Critical Rules

1. **Revolute limits are always swapped** — `lowerLimit = CurveOrientation(Upper)`, `upperLimit = CurveOrientation(Lower)`. This accounts for the SDF→Unity coordinate handedness change.
2. **Prismatic limits are never swapped** — direct mapping from SDF values.
3. **Axis alignment** — always use `axis.Xyz.ToUnity().normalized` for coordinate conversion, then select drive by dominant component.
4. **Anchor rotation** — `Quaternion.FromToRotation()` aligns the drive axis to the joint axis direction.
5. **Continuous joints** have no limits — use `ArticulationDofLock.FreeMotion` instead of `LimitedMotion`.
6. **Force limits** — check `double.IsInfinity(axis.Effort)` and map to `float.MaxValue`.

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Joint rotates on wrong axis | Dominant axis detection chose wrong component | Check `axis.Xyz` in SDF, verify `.ToUnity()` conversion |
| Joint hits limits at wrong angles | Limit swap not applied (or applied to prismatic) | Revolute: must swap. Prismatic: must not swap |
| Joint doesn't move | All DOFs locked | Verify at least one lock is `FreeMotion` or `LimitedMotion` |
| Child link flies away | Missing `ArticulationBody` on parent chain | Ensure all links from root to child have `ArticulationBody` |
| Spring target wrong direction | `CurveOrientation` not applied to spring reference | Revolute targets need `CurveOrientation`; prismatic do not |
| Cross-model joint breaks hierarchy | Wrong parent chosen in `SetArticulationBodyRelationship` | Check model scope logic in import phase |
