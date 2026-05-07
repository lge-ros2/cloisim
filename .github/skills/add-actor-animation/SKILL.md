---
name: add-actor-animation
description: "Add or modify SDF actors with skeletal animation and waypoint-based navigation. Use when: importing animated characters, setting up trajectory waypoints, configuring NavMesh agents for actors, debugging animation playback, adding skinned mesh support, working with Assimp bone hierarchies."
---

# Add Actor and Animation

Procedure for adding SDF actors with skeletal animation, waypoint trajectory following, and NavMesh-based AI navigation.

## When to Use

- Adding a new animated character/actor to a simulation world
- Importing skeletal animations from DAE/FBX files via Assimp
- Setting up waypoint trajectory following for actors
- Configuring NavMesh agent parameters for obstacle avoidance
- Debugging animation playback (wrong bones, missing curves, coordinate issues)
- Understanding the skin mesh → bone hierarchy → AnimationClip pipeline

## Architecture

```
SDF <actor>
  │
  ├─ Import.Actor.cs: ImportActor()
  │  ├─ Create GameObject (tag="Actor")
  │  ├─ Apply SDF pose
  │  ├─ Add Helper.Actor component
  │  ├─ Create skin mesh → Implement.Actor.CreateSkin()
  │  ├─ Load animations → Implement.Actor.SetAnimation()
  │  └─ Pass script data → actorHelper.SetScript()
  │
  ├─ Implement.Actor.cs
  │  ├─ CreateSkin() → MeshLoader.CreateSkinObject()
  │  ├─ SetAnimation() → build bone hierarchy, load AnimationClip
  │  └─ GetBoneHierachy() → relative path dictionary
  │
  ├─ Assimp.Animation.cs: LoadAnimation()
  │  ├─ KeyFramesPosition → localPosition curves
  │  ├─ KeyFramesRotation → localRotation curves (quaternion XYZW)
  │  └─ KeyFramesScale → localScale curves
  │
  └─ Helper.Actor.cs (runtime)
     ├─ Waypoint following (LateUpdate)
     ├─ NavMeshAgent configuration
     └─ Reset handling
```

## SDF Actor Definition

```xml
<actor name="walking_person">
  <pose>0 0 0 0 0 0</pose>

  <skin>
    <filename>meshes/person.dae</filename>
    <scale>1.0</scale>
  </skin>

  <animation name="walk">
    <filename>meshes/walk.dae</filename>
    <scale>1.0</scale>
    <interpolate_x>true</interpolate_x>
  </animation>

  <script>
    <loop>true</loop>
    <delay_start>0.0</delay_start>
    <auto_start>true</auto_start>

    <trajectory id="0" type="walk">
      <waypoint>
        <time>0</time>
        <pose>0 0 0 0 0 0</pose>
      </waypoint>
      <waypoint>
        <time>5</time>
        <pose>5 0 0 0 0 0</pose>
      </waypoint>
    </trajectory>
  </script>
</actor>
```

## Procedure

### 1. Skin Mesh Import

The skin mesh is loaded via Assimp with skeletal support:

```csharp
// In Implement.Actor.cs
static GameObject CreateSkin(in string skinFilename)
{
	return MeshLoader.CreateSkinObject(skinFilename);
	// Returns GameObject with:
	//   - SkinnedMeshRenderer (bone-weighted mesh)
	//   - Bone hierarchy as child transforms
	//   - Materials applied from mesh file
}
```

The skin object is parented under the actor and scaled:

```csharp
// In Import.Actor.cs
var newSkinObject = Implement.Actor.CreateSkin(actor.SkinFilename);
newSkinObject.transform.SetParent(actorObject.transform, false);
newSkinObject.transform.localScale = Vector3.one * (float)actor.SkinScale;
```

### 2. Bone Hierarchy Resolution

Before loading animations, the bone hierarchy is mapped to relative paths:

```csharp
// GetBoneHierachy() walks all descendants of rootBone
// Builds dictionary: { "boneName": "Parent/Child/boneName" }
//
// Example:
// {
//   "Hips":        "Hips",
//   "Spine":       "Hips/Spine",
//   "LeftArm":     "Hips/Spine/LeftArm",
//   "LeftForeArm": "Hips/Spine/LeftArm/LeftForeArm"
// }

var rootBone = skinnedMeshRenderer.rootBone;
var relativePaths = GetBoneHierachy(rootBone);
```

### 3. Animation Loading

`Assimp.Animation.cs` extracts keyframes from the mesh file and creates Unity `AnimationClip`:

```csharp
// LoadAnimation() workflow:
// 1. Load scene via Assimp
// 2. Create AnimationClip (legacy=true, wrapMode=Loop, frameRate=30)
// 3. For each NodeAnimationChannel (bone animation):
//    a. Look up bone path from relativePaths dictionary
//    b. Extract position keyframes → KeyFramesPosition
//    c. Extract rotation keyframes → KeyFramesRotation
//    d. Extract scale keyframes → KeyFramesScale
//    e. Bind curves via clip.SetCurve()

var clip = MeshLoader.LoadAnimation(
	animation.Name,
	animation.Filename,
	relativePaths,
	(float)animation.Scale);
```

### 4. Keyframe Coordinate Conversion

Position keyframes are converted from SDF to Unity coordinates:

```csharp
// KeyFramesPosition.Add():
// SDF (X-forward, Y-left, Z-up) → Unity (X-right, Y-up, Z-forward)
// Same mapping as SDF2Unity.Position()

// Root bone special handling:
// Root X position is SKIPPED (set by trajectory system instead)
// Only Y and Z position curves are bound for root bone
if (!isRoot)
	clip.SetCurve(relativeName, typeof(Transform), "localPosition.x", curveX);
clip.SetCurve(relativeName, typeof(Transform), "localPosition.y", curveY);
clip.SetCurve(relativeName, typeof(Transform), "localPosition.z", curveZ);

// Rotation keyframes — all 4 quaternion components bound:
clip.SetCurve(relativeName, typeof(Transform), "localRotation.x", curveRotX);
clip.SetCurve(relativeName, typeof(Transform), "localRotation.y", curveRotY);
clip.SetCurve(relativeName, typeof(Transform), "localRotation.z", curveRotZ);
clip.SetCurve(relativeName, typeof(Transform), "localRotation.w", curveRotW);
```

### 5. Animation Setup on Actor

```csharp
// In Implement.Actor.SetAnimation():
var animComponent = targetObject.GetComponent<Animation>();
if (animComponent == null)
	animComponent = targetObject.AddComponent<Animation>();

animComponent.AddClip(clip, animation.Name);
animComponent.clip = clip;
animComponent.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
animComponent.animatePhysics = false;
animComponent.playAutomatically = autoStart;

if (autoStart)
	animComponent.Play();
else
	animComponent.Stop();
```

### 6. Trajectory Waypoint System

`Helper.Actor` processes SDF trajectories into waypoints for runtime following:

```csharp
// SetScript() preprocesses trajectory data:
// For each trajectory waypoint (skip first if Time==0):
//   - Compute linear speed = distance / deltaTime
//   - Compute angular speed = angle / deltaTime
//   - Store as WaypointToward { linearSpeed, angularSpeed, translateTo, rotateTo }

// Runtime following in LateUpdate():
// 1. Check delay timer (_scriptDelayStart)
// 2. Get current waypoint[_waypointTowardsIndex]
// 3. Vector3.MoveTowards(current, target, speed * deltaTime)
// 4. Quaternion.RotateTowards(current, target, speed * deltaTime)
// 5. SetActorPose(nextPos, nextRot)
// 6. Check convergence:
//    - distance < DistanceEpsilon (Vector3.kEpsilon * 10)
//    - angle < AngleEpsilon (Quaternion.kEpsilon * 50000)
// 7. If converged → advance to next waypoint
// 8. If loop && reached end → RestartWayPointFollowing()
```

### 7. NavMesh Agent Configuration

Actors automatically get a `NavMeshAgent` for obstacle avoidance:

```csharp
// In Helper.Actor.SetAgentColliderSize() coroutine:
// 1. Wait one FixedUpdate for skinMesh bounds to settle
// 2. Compute radius from skinMesh bounds (min of X, Z extents)
// 3. Configure CapsuleCollider (direction=Y, auto-sized)
// 4. Add/configure NavMeshAgent:

_navMeshAgent.radius = capsuleRadius * AgentMarginRatio; // 1.5x margin
_navMeshAgent.height = capsuleCollider.height;
_navMeshAgent.obstacleAvoidanceType =
	ObstacleAvoidanceType.MedQualityObstacleAvoidance;
_navMeshAgent.avoidancePriority = globalCounter++; // Unique per actor
_navMeshAgent.autoBraking = true;
_navMeshAgent.autoRepath = true;
_navMeshAgent.autoTraverseOffMeshLink = false;
```

### 8. Reset Handling

Actors reset pose and restart trajectories:

```csharp
// Helper.Actor.Reset():
public new void Reset()
{
	base.Reset();  // Restores initial pose via PoseControl
	GetComponent<Animation>().Rewind();
	RestartWayPointFollowing();
	// → StopWaypointFollowing()
	// → Restore initial pose from GetPose(0)
	// → ClearPose() + SetPose(initPose)
	// → StartWaypointFollowing()
}
```

## Procedure: Adding a New Actor to a World

1. Create the SDF `<actor>` element in your world file (see SDF definition above)
2. Place skin mesh file (DAE/FBX) in the model's meshes directory
3. Place animation file(s) in the same directory
4. Ensure the bone hierarchy in the animation file matches the skin file
5. Define trajectory waypoints with time-based poses
6. Set `<auto_start>true</auto_start>` for immediate playback
7. Bake NavMesh via `WorldNavMeshBuilder` for obstacle avoidance

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Actor T-poses (no animation) | Bone names don't match between skin and animation | Verify bone hierarchy matches in both files |
| Animation plays but actor doesn't move | Root X position is skipped | Use trajectory waypoints for actor movement |
| Actor slides without walking | Animation speed doesn't match trajectory speed | Adjust waypoint timing to match walk cycle |
| Actor falls through floor | Missing CapsuleCollider or NavMesh | Verify collider is added; bake NavMesh |
| Animation jitters | Keyframe timing mismatch | Check `TicksPerSecond` in animation file (default: 25) |
| Bones are in wrong position | Coordinate conversion issue | Check `KeyFramesPosition.Add()` axis mapping |
| Actor doesn't avoid obstacles | NavMesh not baked or agent not configured | Run `WorldNavMeshBuilder`; check agent radius/height |
| Actor doesn't loop | `<loop>false</loop>` in SDF | Set `<loop>true</loop>` and `wrapMode = Loop` |
| Reset doesn't restart animation | `Rewind()` not called | Ensure `Reset()` calls both `Rewind()` and `RestartWayPointFollowing()` |
