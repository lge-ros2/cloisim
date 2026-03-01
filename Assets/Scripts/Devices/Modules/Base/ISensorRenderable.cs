/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

namespace SensorDevices
{
	/// <summary>
	/// Interface for any device that needs GPU render passes managed
	/// by the centralized SensorRenderManager. Both Camera (single render)
	/// and Lidar (multiple sub-camera renders per scan) implement this.
	/// </summary>
	public interface ISensorRenderable
	{
		/// <summary>Device name for logging.</summary>
		string DeviceName { get; }

		/// <summary>Target update period (seconds). Used by multi-fire logic.</summary>
		float UpdatePeriod { get; }

		/// <summary>
		/// True if this device uses Unified Ray Tracing (compute dispatch only,
		/// no Camera.Render). URT devices are cheap enough to render multiple
		/// times per frame when they fall behind their target rate.
		/// </summary>
		bool IsURT { get; }

		/// <summary>Check if this device needs a render pass this frame.</summary>
		bool IsReadyToRender(float realtimeNow);

		/// <summary>How overdue this device is (seconds past update period).</summary>
		float GetRenderUrgency(float realtimeNow);

		/// <summary>
		/// Execute one render unit. For cameras this renders the full frame.
		/// For lidars this renders one sub-camera and returns true when
		/// all sub-cameras for the current scan are complete.
		/// </summary>
		/// <returns>true if the device's current render cycle is complete</returns>
		bool ExecuteRenderStep(float realtimeNow);
	}
}
