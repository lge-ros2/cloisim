/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

/// <summary>
/// Interface for sensor devices that can be rendered by SensorRenderManager.
/// Scheduling (nextRenderTime, urgency) is owned by SensorRenderManager.
/// Sensors only provide timing parameters and execution logic.
/// </summary>
public interface ISensorRenderable
{
	/// <summary>
	/// Whether this sensor uses Unified Ray Tracing (cheap compute dispatch).
	/// </summary>
	bool IsURT { get; }

	/// <summary>
	/// The desired render period in seconds (1 / updateRate).
	/// Used by SensorRenderManager for scheduling.
	/// </summary>
	float RenderPeriod { get; }

	/// <summary>
	/// Whether the sensor is ready to accept render commands
	/// (e.g. has been initialized, has a valid render target).
	/// </summary>
	bool CanRender { get; }

	/// <summary>
	/// Execute one render step. Returns true when the render is complete
	/// (single-step devices always return true).
	/// </summary>
	bool ExecuteRenderStep(float realtimeNow);
}
