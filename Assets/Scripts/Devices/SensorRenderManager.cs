/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Unity.Profiling;
using Debug = UnityEngine.Debug;

namespace SensorDevices
{
	/// <summary>
	/// Centralized render scheduler that batches all sensor camera renders
	/// into a single coroutine per frame.
	///
	/// Problem:
	///   Each Camera.Render() triggers a full HDRP pipeline pass (~2-5ms CPU):
	///   culling, shadow atlas, render graph setup, command buffer submission.
	///   With 9 cameras running independent CameraWorker coroutines, the
	///   scheduling is unpredictable and each coroutine yields between renders,
	///   preventing HDRP from reusing internal state across cameras.
	///
	/// Solution:
	///   A single render loop renders all eligible cameras in a tight sequence
	///   within the same frame, without yielding between them. This allows:
	///   - HDRP to share shadow atlas state across sequential renders
	///   - Single scheduling point with proper real-time rate limiting
	///   - Elimination of 9 separate coroutines (1 instead)
	///   - Adaptive frame budget to prevent frame time spikes
	/// </summary>
	public class SensorRenderManager : MonoBehaviour
	{
		private static SensorRenderManager _instance;
		private readonly List<ISensorRenderable> _renderables = new();
		private Coroutine _renderLoop;

		// ── Profiling markers ──
		private static readonly ProfilerMarker s_BatchLoopMarker = new("SensorRender.BatchLoop");
		private static readonly ProfilerMarker s_CollectMarker = new("SensorRender.Collect");
		private static readonly ProfilerMarker s_SortMarker = new("SensorRender.Sort");
		private static readonly ProfilerMarker s_RenderBatchMarker = new("SensorRender.RenderBatch");
		private static readonly ProfilerMarker s_SingleRenderStepMarker = new("SensorRender.RenderStep");

		// ── Adaptive budget tracking ──
		private readonly Stopwatch _batchStopwatch = new();
		private float _avgRenderStepMs = 5f; // EMA of per-render-step time
		private const float EMA_ALPHA = 0.2f;

		// ── Diagnostics ──
		private float _diagLastLogTime = 0f;
		private int _diagFrameCount = 0;
		private int _diagTotalSteps = 0;
		private float _diagTotalBatchMs = 0f;
		private float _diagMaxBatchMs = 0f;
		private const float DIAG_INTERVAL_SEC = 10f;

		/// <summary>
		/// Frame time budget for sensor renders (milliseconds).
		/// At 20 FPS (50ms/frame), 16ms leaves 34ms for physics/scripts.
		/// </summary>
		public float FrameBudgetMs { get; set; } = 16f;

		/// <summary>Current EMA of per-render-step cost in ms.</summary>
		public float AvgRenderStepMs => _avgRenderStepMs;

		/// <summary>
		/// Maximum render steps per frame (hard cap).
		/// Each camera = 1 step, each lidar sub-camera = 1 step.
		/// With 9 cameras + 2 lidars (4+6 sub-cameras), worst case = 19 steps.
		/// The adaptive budget may execute fewer than this.
		/// </summary>
		private const int MAX_RENDER_STEPS_PER_FRAME = 24;

		public static SensorRenderManager Instance
		{
			get
			{
				if (_instance == null)
				{
					var go = new GameObject("__SensorRenderManager__");
					DontDestroyOnLoad(go);
					_instance = go.AddComponent<SensorRenderManager>();
				}
				return _instance;
			}
		}

		/// <summary>
		/// Register any ISensorRenderable device (Camera or Lidar).
		/// </summary>
		public void Register(ISensorRenderable device)
		{
			if (device != null && !_renderables.Contains(device))
			{
				_renderables.Add(device);
				Debug.Log($"[SensorRenderManager] Registered: {device.DeviceName} (total: {_renderables.Count})");

				if (_renderLoop == null)
				{
					_renderLoop = StartCoroutine(BatchedRenderLoop());
				}
			}
		}

		/// <summary>
		/// Unregister a sensor device (called from OnDestroy).
		/// </summary>
		public void Unregister(ISensorRenderable device)
		{
			if (_renderables.Remove(device))
			{
				Debug.Log($"[SensorRenderManager] Unregistered: {device.DeviceName} (total: {_renderables.Count})");
			}
		}

		/// <summary>
		/// Core batched render loop. Each frame:
		///   1. Collect all cameras that are due for rendering
		///   2. Sort by urgency (most overdue first)
		///   3. Render up to MAX_CAMERAS_PER_FRAME in a tight batch
		///
		/// The tight loop (no yields between cameras) allows HDRP to
		/// reuse internal state, and urgency sorting ensures fair
		/// scheduling across all cameras.
		/// </summary>
		private IEnumerator BatchedRenderLoop()
		{
			Debug.Log($"[SensorRenderManager] Batched render loop started (budget={FrameBudgetMs}ms, hardCap={MAX_RENDER_STEPS_PER_FRAME})");

			var readyDevices = new List<(ISensorRenderable device, float urgency)>(16);
			var frameBatchStopwatch = new Stopwatch();

			while (true)
			{
				using (s_BatchLoopMarker.Auto())
				{
					var now = Time.realtimeSinceStartup;
					readyDevices.Clear();
					frameBatchStopwatch.Restart();

					// Phase 1: Collect devices that need rendering this frame
					using (s_CollectMarker.Auto())
					{
						for (int i = 0; i < _renderables.Count; i++)
						{
							var device = _renderables[i];
							if (device == null || (device is UnityEngine.Object obj && obj == null))
							{
								_renderables.RemoveAt(i--);
								continue;
							}

							if (device.IsReadyToRender(now))
							{
								readyDevices.Add((device, device.GetRenderUrgency(now)));
							}
						}
					}

					// Phase 2: Sort by urgency (most overdue first)
					using (s_SortMarker.Auto())
					{
						if (readyDevices.Count > 1)
						{
							readyDevices.Sort((a, b) => b.urgency.CompareTo(a.urgency));
						}
					}

					// Phase 3: Render with adaptive budget — stop when frame budget exhausted
					// Devices that need multiple steps per scan (e.g., LiDAR with N
					// sub-cameras) are re-processed immediately if budget remains.
					using (s_RenderBatchMarker.Auto())
					{
						var budgetRemainingMs = FrameBudgetMs;
						var steps = 0;

						for (int i = 0; i < readyDevices.Count; i++)
						{
							if (steps >= MAX_RENDER_STEPS_PER_FRAME)
								break;

							// Check if we have enough budget for another render step
							if (steps > 0 && budgetRemainingMs < _avgRenderStepMs * 0.5f)
								break;

							_batchStopwatch.Restart();

							bool completed;
							using (s_SingleRenderStepMarker.Auto())
							{
								completed = readyDevices[i].device.ExecuteRenderStep(now);
							}

							_batchStopwatch.Stop();
							var elapsedMs = (float)_batchStopwatch.Elapsed.TotalMilliseconds;
							budgetRemainingMs -= elapsedMs;
							steps++;

							// Update EMA of per-render-step cost
							_avgRenderStepMs = _avgRenderStepMs * (1f - EMA_ALPHA) + elapsedMs * EMA_ALPHA;

							// If the device needs more steps (e.g., lidar multi-subcam scan),
							// re-process it on the next iteration (budget permitting)
							if (!completed)
								i--;
						}
					}

					// ── Periodic diagnostics ──
					frameBatchStopwatch.Stop();
					var frameBatchMs = (float)frameBatchStopwatch.Elapsed.TotalMilliseconds;
					_diagFrameCount++;
					_diagTotalSteps += readyDevices.Count;
					_diagTotalBatchMs += frameBatchMs;
					if (frameBatchMs > _diagMaxBatchMs) _diagMaxBatchMs = frameBatchMs;

					if (now - _diagLastLogTime >= DIAG_INTERVAL_SEC)
					{
						var avgBatch = _diagFrameCount > 0 ? _diagTotalBatchMs / _diagFrameCount : 0;
						var avgSteps = _diagFrameCount > 0 ? (float)_diagTotalSteps / _diagFrameCount : 0;
						Debug.Log($"[SensorRenderManager] {DIAG_INTERVAL_SEC}s stats: " +
							$"frames={_diagFrameCount}, avgBatchMs={avgBatch:F2}, maxBatchMs={_diagMaxBatchMs:F2}, " +
							$"avgSteps/frame={avgSteps:F1}, avgStepMs={_avgRenderStepMs:F2}, " +
							$"registered={_renderables.Count}");
						_diagLastLogTime = now;
						_diagFrameCount = 0;
						_diagTotalSteps = 0;
						_diagTotalBatchMs = 0;
						_diagMaxBatchMs = 0;
					}
				}

				yield return null;
			}
		}

		private void OnDestroy()
		{
			if (_renderLoop != null)
			{
				StopCoroutine(_renderLoop);
				_renderLoop = null;
			}
			_renderables.Clear();
			_instance = null;
		}
	}
}
