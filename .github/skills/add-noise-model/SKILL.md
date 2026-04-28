---
name: add-noise-model
description: "Add a custom noise model for sensor data simulation. Use when: implementing a non-Gaussian noise type, adding distance-dependent noise, creating a new sensor-specific noise profile."
---

# Add a New Noise Model

Procedure for creating a custom noise model and integrating it with sensor devices.

## When to Use

- Implementing a new noise type beyond Gaussian (e.g., salt-and-pepper, Perlin, structured)
- Adding distance-dependent or range-binned noise for lidar/depth sensors
- Creating a sensor-specific noise profile from real-world calibration data

## Architecture

```
Noise (facade)
  └── NoiseModel (abstract base)
        ├── GaussianNoiseModel (standard + dynamic bias)
        └── CustomNoiseModel (extends Gaussian, range-binned, XML-parameterized)
```

- `Noise` is the public API used by devices — handles threading via `Parallel.For`
- `NoiseModel` is the abstract base with bias sampling, clamping, quantization
- `GaussianNoiseModel` implements standard Gaussian with dynamic bias correlation
- `CustomNoiseModel` extends Gaussian with range-binned noise from XML parameters

## Procedure

### 1. Create the Noise Model Class

Create `Assets/Scripts/Devices/Modules/NoiseModel/MyNoiseModel.cs`:

```csharp
/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;

public class MyNoiseModel : NoiseModel
{
	// Custom parameters
	private readonly double _myParam;

	public MyNoiseModel(in SDFormat.Noise parameter)
		: base(parameter)
	{
		// Extract parameters from SDFormat.Noise or custom fields
		_myParam = parameter.StdDev; // or a custom parameter
	}

	public override T Generate<T>(T data, float deltaTime)
	{
		var value = Convert.ToDouble(data);

		// Apply your noise model
		var noise = ComputeNoise(value, deltaTime);
		var output = value + bias + noise;

		// Apply quantization if enabled
		if (_quantized)
		{
			if (Math.Abs(_parameter.Precision - 0d) > Epsilon)
			{
				output = Math.Round(output / _parameter.Precision) * _parameter.Precision;
			}
		}

		// Apply clamping
		return (T)Convert.ChangeType(Clamp(output), typeof(T));
	}

	private double ComputeNoise(double value, float deltaTime)
	{
		// Your noise algorithm here
		// Use RandomNumberGenerator for RNG:
		var random = RandomNumberGenerator.GetNormal(0, _myParam);
		return random;
	}
}
```

### 2. Register in Noise Facade

Edit `Assets/Scripts/Devices/Modules/Noise.cs` — add a case in the constructor:

```csharp
public Noise(in SDFormat.Noise noise)
{
	switch (noise.Type)
	{
		case SDFormat.NoiseType.Gaussian:
		case SDFormat.NoiseType.GaussianQuantized:
			_noiseModel = new GaussianNoiseModel(noise);
			if (noise.Type == SDFormat.NoiseType.GaussianQuantized)
				_noiseModel.SetQuantization(true);
			break;

		case SDFormat.NoiseType.MyType:  // Add new case
			_noiseModel = new MyNoiseModel(noise);
			break;

		default:
			_noiseModel = null;
			break;
	}
}
```

If using a custom noise type string, add it to the `SDFormat.NoiseType` enum in the SDFormat package.

### 3. Use in a Device

```csharp
// In the sensor device class:
private Noise _noise;

public void SetupNoise(in SDFormat.Noise noise)
{
	_noise = new Noise(noise);
	_noise.SetClampMin(0);        // optional: clamp minimum
	_noise.SetClampMax(maxRange); // optional: clamp maximum
}

// Apply to single value:
protected override void GenerateMessage()
{
	float value = ReadSensorValue();
	_noise.Apply<float>(ref value, Time.fixedDeltaTime);
	_msg.Value = value;
}

// Apply to array (parallelized automatically):
protected override void GenerateMessage()
{
	float[] data = ReadSensorArray();
	_noise.Apply<float>(data, Time.fixedDeltaTime);
}
```

## NoiseModel Base Class API

```csharp
// Available in base class:
protected readonly SDFormat.Noise _parameter;  // Original SDF noise parameters
protected double bias;                          // Sampled bias (from BiasMean/BiasStdDev)
protected bool _quantized;                      // Whether to quantize output
protected double clampMin, clampMax;            // Output clamping bounds

// Methods:
protected double Clamp(in double value);        // Apply clamping
protected double Expm1(in double value);        // exp(x) - 1 helper

// Static RNG:
RandomNumberGenerator.GetNormal(mean, stddev);  // Box-Muller Gaussian
RandomNumberGenerator.GetNormal();              // Standard normal (0, 1)
RandomNumberGenerator.GetUniform();             // Uniform [0, 1)
```

## Dynamic Bias (from GaussianNoiseModel)

If your model needs time-correlated bias drift:

```csharp
// Available if extending GaussianNoiseModel:
protected double ComputeDynamicBias(in float deltaTime)
{
	// Uses _parameter.DynamicBiasStdDev and DynamicBiasCorrelationTime
	// Returns time-varying bias using Ornstein-Uhlenbeck process
}
```

## GPU Noise (Shader-Based)

For camera sensors, noise is applied on the GPU via `AddGaussianNoise.shader`:

```csharp
// In a camera device:
_noiseMaterial = new Material(Shader.Find("Sensor/Camera/GaussianNoise"));
_noiseMaterial.SetFloat("_Mean", mean);
_noiseMaterial.SetFloat("_StdDev", stddev);

// Applied via CommandBuffer blit in the camera's render pipeline
```

## Threading

The `Noise` class uses `Parallel.For` with adaptive parallelism:

```csharp
// Thread count: max(1, ProcessorCount / 4)
Parallel.For(0, data.Length, _parallelOptions, i =>
{
    data[i] = _noiseModel.Generate(data[i], deltaTime);
});
```

Each `Generate()` call must be **thread-safe** — avoid shared mutable state. `RandomNumberGenerator` uses `ThreadLocal<Random>` internally.

## Checklist

- [ ] Noise model class extends `NoiseModel` (or `GaussianNoiseModel`)
- [ ] `Generate<T>()` is thread-safe
- [ ] Registered in `Noise` constructor switch
- [ ] Uses `RandomNumberGenerator` for RNG (thread-safe)
- [ ] Applies `Clamp()` on output
- [ ] Handles quantization if `_quantized` is true
- [ ] SDFormat noise type enum extended (if new type)
- [ ] License header on file
- [ ] Tabs for indentation, Allman braces
