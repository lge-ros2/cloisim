---
name: add-motor-controller
description: "Add a new motor or drive controller for robot locomotion. Use when: implementing a new drive type (e.g., mecanum, Ackermann, legged), adding a new actuator controller, creating a custom motion model."
---

# Add a New Motor/Drive Controller

Procedure for creating a new drive controller that manages motors and provides odometry for a robot locomotion system.

## When to Use

- Implementing a new drive type (mecanum, Ackermann, swerve, legged)
- Adding a custom actuator controller
- Creating a new motion model for a robot platform

## Architecture Overview

```
MicomPlugin (transport bridge)
  └── MotorControl (abstract drive controller)
        └── Motor[] (per-wheel PID + drive)
              └── Articulation (ArticulationBody wrapper)
```

## Procedure

### 1. Create the Drive Controller

Create `Assets/Scripts/Devices/Modules/Motor/MyDriveControl/MyDrive.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SensorDevices
{
	public class MyDrive : MotorControl
	{
		private Odometry _odometry;

		public MyDrive(Transform controllerTransform)
			: base(controllerTransform)
		{
		}

		public override void SetWheelInfo(in float radius, in float separation)
		{
			// Store wheel geometry, configure motor constraints
			base.SetWheelInfo(radius, separation);

			_odometry = new Odometry(wheelRadius, wheelSeparation);
		}

		public override void Drive(in float linearVelocity, in float angularVelocity)
		{
			// Convert twist to per-wheel angular velocities
			var leftWheelVel = (linearVelocity - angularVelocity * _wheelSeparation / 2f) / _wheelRadius;
			var rightWheelVel = (linearVelocity + angularVelocity * _wheelSeparation / 2f) / _wheelRadius;

			// Apply to motors
			SetMotorVelocity(Location.LEFT, leftWheelVel);
			SetMotorVelocity(Location.RIGHT, rightWheelVel);
		}

		public override bool Update(
			out float linearVelocity, out float angularVelocity,
			in float duration, in float compensatingRatio = 1f)
		{
			// Run PID on each motor
			foreach (var motor in _motorList.Values)
			{
				motor.Run(duration);
			}

			// Compute odometry from wheel encoders
			var leftVel = GetMotorVelocity(Location.LEFT);
			var rightVel = GetMotorVelocity(Location.RIGHT);

			_odometry.Update(leftVel, rightVel, duration);

			linearVelocity = _odometry.LinearVelocity;
			angularVelocity = _odometry.AngularVelocity;

			return true;
		}

		public override void Reset()
		{
			base.Reset();
			_odometry?.Reset();
		}
	}
}
```

### 2. Understand the Motor API

Each `Motor` wraps an `ArticulationBody` joint drive:

```csharp
// Motor is attached per-wheel via MotorControl.AttachMotor()
motor.SetTargetVelocity(angularVelocityRadPerSec);
motor.Run(fixedDeltaTime);      // Applies PID correction
motor.GetVelocity();            // Reads back from ArticulationBody
motor.Stop();                   // Zero velocity + reset PID
```

### 3. Understand the Location Enum

Motors are indexed by `Location`:

```csharp
public enum Location
{
	NONE = 0,
	LEFT = 1,
	RIGHT = 2,
	LEFT_REAR = 3,
	RIGHT_REAR = 4,
	// Add new locations if needed for your drive type
}
```

### 4. Configure PID

Each motor has its own PID controller:

```csharp
motor.SetPID(pGain, iGain, dGain);
```

PID features:
- Integral clamping (anti-windup)
- Output clamping
- Reset on mode change

### 5. Integrate Odometry

Use `Odometry` class for dead reckoning:

```csharp
_odometry = new Odometry(wheelRadius, wheelSeparation);
_odometry.Update(leftWheelVel, rightWheelVel, deltaTime);

// Read back:
_odometry.LinearVelocity;
_odometry.AngularVelocity;
_odometry.Pose;  // (x, y, heading)
```

Odometry features:
- Runge-Kutta 2nd order integration
- Optional IMU yaw fusion
- Rolling mean velocity filtering

### 6. Wire to MicomPlugin

The `MicomPlugin` creates the drive controller in its `OnStart()`:

```csharp
// In MicomPlugin.OnStart() or MicomSensor setup:
_motorControl = new MyDrive(transform);
_motorControl.SetWheelInfo(wheelRadius, wheelSeparation);
_motorControl.AttachMotor(Location.LEFT, leftWheelLink);
_motorControl.AttachMotor(Location.RIGHT, rightWheelLink);
```

The plugin's `FixedUpdate()` calls `_motorControl.Update()` and the RX handler calls `_motorControl.Drive()`.

### 7. Handle Multi-Axle (Optional)

For 4-wheel or multi-axle robots, use additional `Location` values and compute individual wheel velocities accounting for all axles:

```csharp
public override void Drive(in float linearVelocity, in float angularVelocity)
{
	// Front axle
	SetMotorVelocity(Location.LEFT, frontLeftVel);
	SetMotorVelocity(Location.RIGHT, frontRightVel);
	// Rear axle
	SetMotorVelocity(Location.LEFT_REAR, rearLeftVel);
	SetMotorVelocity(Location.RIGHT_REAR, rearRightVel);
}
```

## Checklist

- [ ] Drive controller extends `MotorControl`
- [ ] `SetWheelInfo()` configures geometry and creates `Odometry`
- [ ] `Drive()` converts twist to per-wheel velocities
- [ ] `Update()` runs PID on each motor, updates odometry, returns linear/angular velocity
- [ ] `Reset()` calls `base.Reset()` and resets odometry
- [ ] PID gains configured appropriately for the drive type
- [ ] Odometry integration uses appropriate model (differential, mecanum, etc.)
- [ ] Motors attached via `AttachMotor()` with correct `Location` mapping
- [ ] License header on new files
