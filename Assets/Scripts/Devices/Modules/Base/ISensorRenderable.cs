/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

/// <summary>
/// Interface for sensor devices that can be rendered by SensorRenderManager.
/// Provides scheduling and execution hooks for batched rendering.
/// </summary>
public interface ISensorRenderable
{
	/// <summary>
	/// Whether this sensor uses Unified Ray Tracing (cheap compute dispatch).
	/// </summary>
	bool IsURT { get; }

	/// <summary>
	/// Check if this sensor should render this frame.
	/// </summary>
	bool IsReadyToRender(float realtimeNow);

	/// <summary>
	/// How overdue this sensor is for rendering (seconds past its
	/// scheduled time). Used to prioritize the most starved sensors.
	/// </summary>
	float GetRenderUrgency(float realtimeNow);

	/// <summary>
	/// Execute one render step. Returns true when the render is complete
	/// (single-step devices always return true).
	/// </summary>
	bool ExecuteRenderStep(float realtimeNow);
}
