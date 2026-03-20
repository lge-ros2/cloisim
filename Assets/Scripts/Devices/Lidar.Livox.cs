/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.UnifiedRayTracing;
using messages = cloisim.msgs;

namespace SensorDevices
{
	/// <summary>
	/// Livox non-repetitive scan pattern support for the Lidar device.
	/// This partial class adds all Livox-specific fields and methods.
	/// </summary>
	public partial class Lidar
	{
		#region "Livox fields"

		private LivoxScanPattern _livoxPattern = null;
		private ComputeBuffer _scanPatternBuffer = null;
		private bool _isLivoxMode = false;

		private static ComputeShader ComputeShaderLivoxRayTrace = null;

		// Shader property IDs for Livox-specific parameters
		private static readonly int PID_ScanPattern = Shader.PropertyToID("_ScanPattern");
		private static readonly int PID_PatternSize = Shader.PropertyToID("_PatternSize");
		private static readonly int PID_SampleCount = Shader.PropertyToID("_SampleCount");
		private static readonly int PID_StartIndex = Shader.PropertyToID("_StartIndex");
		private static readonly int PID_DownsampleStep = Shader.PropertyToID("_DownsampleStep");
		private static readonly int PID_XYZOutput = Shader.PropertyToID("_XYZOutput");

		public bool IsLivoxMode => _isLivoxMode;

		#endregion

		#region "Livox setup"

		private static void LoadLivoxComputeShader()
		{
			if (ComputeShaderLivoxRayTrace == null)
			{
				ComputeShaderLivoxRayTrace = Resources.Load<ComputeShader>("Shader/LivoxLidarRayTrace");
			}
		}

		private static void UnloadLivoxComputeShader()
		{
			if (ComputeShaderLivoxRayTrace != null)
			{
				Resources.UnloadAsset(ComputeShaderLivoxRayTrace);
				ComputeShaderLivoxRayTrace = null;
			}
		}

		/// <summary>
		/// Configure the Lidar to operate in Livox non-repetitive scan mode.
		/// Called from LaserPlugin after reading plugin parameters.
		/// Can be called before or after the standard OnStart/SetupURT.
		/// </summary>
		public void SetupLivoxPattern(string csvPath, int samplesPerCycle, int downsample)
		{
			_livoxPattern = new LivoxScanPattern();
			if (!_livoxPattern.Load(csvPath, samplesPerCycle, downsample))
			{
				Debug.LogError("[Lidar] Failed to load Livox scan pattern");
				_livoxPattern = null;
				return;
			}

			_isLivoxMode = true;

			// Re-setup messages for Livox mode (different buffer sizes)
			SetupMessages();

			// Re-setup URT if already initialized; otherwise OnStart → SetupURT will handle it
			if (_rtShader != null)
			{
				CleanupLivoxResources();
				CleanupURTResources();
				SetupURT();
			}

			// Drain any stale readback results from the standard-mode queue
			while (_outputQueue.TryDequeue(out _)) { }

			Debug.Log($"[Lidar] Livox mode enabled — {_livoxPattern.TotalRaysPerCycle} rays/cycle, " +
				$"pattern size {_livoxPattern.PatternSize}");
		}

		/// <summary>
		/// Set up Livox-specific message layout.
		/// For PointCloud2Raw: Ranges stores XYZ triples, Intensities stores one value per point.
		/// </summary>
		private void SetupLivoxMessages()
		{
			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			_laserScan.Frame = DeviceName;
			_laserScan.Count = raysPerCycle;

			// Preserve the SDF-defined angle metadata for identification
			_laserScan.AngleMin = _horizontal.angle.min * Mathf.Deg2Rad;
			_laserScan.AngleMax = _horizontal.angle.max * Mathf.Deg2Rad;
			_laserScan.AngleStep = _horizontal.angleStep * Mathf.Deg2Rad;

			_laserScan.RangeMin = _scanRange.min;
			_laserScan.RangeMax = _scanRange.max;

			_laserScan.VerticalCount = 1;
			_laserScan.VerticalAngleMin = _vertical.angle.min * Mathf.Deg2Rad;
			_laserScan.VerticalAngleMax = _vertical.angle.max * Mathf.Deg2Rad;
			_laserScan.VerticalAngleStep = _vertical.angleStep * Mathf.Deg2Rad;

			_totalSamples = raysPerCycle;

			// Ranges stores 3 floats per point (x, y, z) for PointCloud2Raw
			_laserScan.Ranges = new double[_totalSamples * 3];
			_laserScan.Intensities = new double[_totalSamples];
			Array.Fill(_laserScan.Ranges, double.NaN);
			Array.Fill(_laserScan.Intensities, 0.0);
		}

		/// <summary>
		/// Set up URT resources for Livox mode.
		/// Uses a dedicated compute shader with a pattern buffer.
		/// </summary>
		private void SetupLivoxURT()
		{
			LoadLivoxComputeShader();

			if (!URTSensorManager.Register(GetEntityId()))
			{
				Debug.LogError("[Lidar] Failed to register with URTSensorManager (Livox)");
				return;
			}

			_csRayTrace = Instantiate(ComputeShaderLivoxRayTrace);
			if (_csRayTrace == null)
			{
				Debug.LogError("[Lidar] Failed to instantiate LivoxLidarRayTrace compute shader");
				return;
			}

			_rtShader = URTSensorManager.CreateShader(_csRayTrace);

			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			// Output buffer: 3 floats per ray (x, y, z)
			_rangeOutputBuffer?.Release();
			_rangeOutputBuffer = new ComputeBuffer((int)(raysPerCycle * 3), sizeof(float));

			// Scratch buffer for 1D dispatch (width = raysPerCycle, height = 1)
			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(
				_rtShader, raysPerCycle, 1, 1);

			// Upload scan pattern to GPU (persistent buffer for entire CSV)
			_scanPatternBuffer?.Release();
			_scanPatternBuffer = new ComputeBuffer(_livoxPattern.PatternSize, sizeof(float) * 2);
			_scanPatternBuffer.SetData(_livoxPattern.Pattern);

			_urtCmdBuffer = new CommandBuffer { name = "Livox Lidar URT Dispatch" };

			Debug.Log($"[Lidar] Livox URT initialized, rays/cycle={raysPerCycle}, " +
				$"pattern={_livoxPattern.PatternSize}, " +
				$"range=[{_scanRange.min:F2}, {_scanRange.max:F2}]");
		}

		/// <summary>
		/// Execute GPU ray trace for Livox mode.
		/// Dispatches rays from the current pattern window and outputs XYZ positions.
		/// </summary>
		private void ExecuteLivoxRender()
		{
			if (URTSensorManager.AccelStruct == null || _rtShader == null)
				return;

			var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

			var sensorTransform = this.transform;
			var sensorPos = sensorTransform.position;
			var sensorRight = sensorTransform.right;
			var sensorUp = sensorTransform.up;
			var sensorForward = sensorTransform.forward;
			var sensorWorldPose = new Pose(sensorPos, sensorTransform.rotation);

			var raysPerCycle = (uint)_livoxPattern.TotalRaysPerCycle;

			// Resize scratch buffer if needed
			RayTracingHelper.ResizeScratchBufferForTrace(
				_rtShader, raysPerCycle, 1, 1, ref _rtTraceScratchBuffer);

			// === Record GPU work ===
			_urtCmdBuffer.Clear();

			// 1. Shared BVH
			URTSensorManager.EnsureBVHReady(_urtCmdBuffer);

			// 2. Bind resources
			_rtShader.SetAccelerationStructure(_urtCmdBuffer, "_AccelStruct", URTSensorManager.AccelStruct);
			_rtShader.SetBufferParam(_urtCmdBuffer, PID_XYZOutput, _rangeOutputBuffer);
			_rtShader.SetBufferParam(_urtCmdBuffer, PID_ScanPattern, _scanPatternBuffer);

			// 3. Pattern parameters
			_rtShader.SetIntParam(_urtCmdBuffer, PID_PatternSize, _livoxPattern.PatternSize);
			_rtShader.SetIntParam(_urtCmdBuffer, PID_SampleCount, (int)raysPerCycle);
			_rtShader.SetIntParam(_urtCmdBuffer, PID_StartIndex, _livoxPattern.CurrentStartIndex);
			_rtShader.SetIntParam(_urtCmdBuffer, PID_DownsampleStep, _livoxPattern.Downsample);

			// 4. Range parameters
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_RangeMin, _scanRange.min);
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_RangeMax, _scanRange.max);
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_RangeLinearResolution, _resolution.linear);

			// 5. Sensor pose
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_SensorPosition,
				new Vector4(sensorPos.x, sensorPos.y, sensorPos.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_SensorRight,
				new Vector4(sensorRight.x, sensorRight.y, sensorRight.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_SensorUp,
				new Vector4(sensorUp.x, sensorUp.y, sensorUp.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_SensorForward,
				new Vector4(sensorForward.x, sensorForward.y, sensorForward.z, 0f));

			// 6. Dispatch (1D: raysPerCycle × 1 × 1)
			_rtShader.Dispatch(_urtCmdBuffer, _rtTraceScratchBuffer, raysPerCycle, 1, 1);

			// === Execute ===
			Graphics.ExecuteCommandBuffer(_urtCmdBuffer);

			// Advance pattern for next frame
			_livoxPattern.AdvanceCycle();

			// --- Async readback ---
			AsyncGPUReadback.Request(_rangeOutputBuffer, (req) =>
			{
				if (req.hasError || !req.done)
				{
					Debug.LogWarning("[Lidar] Livox async GPU readback failed");
					return;
				}

				var src = req.GetData<float>();
				var xyzData = new float[src.Length];
				src.CopyTo(xyzData);

				_outputQueue.Enqueue((capturedTime, sensorWorldPose, xyzData));
				_dataAvailable.Set();
			});
		}

		/// <summary>
		/// Process Livox readback data into LaserScanStamped message.
		/// XYZ triples are copied directly into the Ranges array for PointCloud2Raw output.
		/// </summary>
		private messages.LaserScanStamped ProcessLivoxData(
			double capturedTime, Pose sensorWorldPose, float[] xyzData)
		{
			var laserScanStamped = new messages.LaserScanStamped();
			laserScanStamped.Time = new messages.Time();
			laserScanStamped.Time.Set(capturedTime);

			laserScanStamped.Scan = _laserScan;

			var laserScan = laserScanStamped.Scan;
			laserScan.WorldPose.Position.Set(sensorWorldPose.position);
			laserScan.WorldPose.Orientation.Set(sensorWorldPose.rotation);

			// Copy XYZ triples directly into Ranges array
			var ranges = laserScan.Ranges;
			var count = Math.Min(ranges.Length, xyzData.Length);
			for (var i = 0; i < count; i++)
			{
				ranges[i] = xyzData[i];
			}

			// Intensities: no intensity model for Livox patterns currently,
			// fill with 0.0 (already initialized in SetupLivoxMessages)

			return laserScanStamped;
		}

		/// <summary>Clean up Livox-specific GPU resources.</summary>
		private void CleanupLivoxResources()
		{
			_scanPatternBuffer?.Release();
			_scanPatternBuffer = null;
		}

		/// <summary>Clean up shared URT resources (used before re-initialization).</summary>
		private void CleanupURTResources()
		{
			_urtCmdBuffer?.Release();
			_urtCmdBuffer = null;

			_rtTraceScratchBuffer?.Dispose();
			_rtTraceScratchBuffer = null;

			_rangeOutputBuffer?.Release();
			_rangeOutputBuffer = null;

			if (_csRayTrace != null)
			{
				Destroy(_csRayTrace);
				_csRayTrace = null;
			}

			_rtShader = null;

			URTSensorManager.Unregister(GetEntityId());
		}

		#endregion
	}
}
