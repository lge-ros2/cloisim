/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using messages = cloisim.msgs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class SegmentationCamera : Camera
	{
		// ── Profiling ──
		private static readonly ProfilerMarker s_SegRenderMarker = new("SegmentationCamera.Render");

		#region "Unified Ray Tracing"

		// ── Unified Ray Tracing ──
		// When Unified RT is available (hardware or compute backend), replaces
		// Camera.Render() with a single dispatch.
		// Instance IDs in the accel struct carry the segmentation class ID.
		private bool _useURT = false;
		private UnityEngine.Rendering.UnifiedRayTracing.IRayTracingShader _urtShader;
		private GraphicsBuffer _urtScratchBuffer;
		private CommandBuffer _urtCmd;
		private RenderTexture _urtOutputRT;

		public override bool IsURT => _useURT;

		#endregion

		protected override void SetupTexture()
		{
			_targetRTname = "SegmentationTexture";

			var pixelFormat = CameraData.GetPixelFormat(_camParam.image.format);
			if (pixelFormat != CameraData.PixelFormat.L_INT16)
			{
				Debug.Log("Only support INT16 format");
			}

			// for Unsigned 16-bit
			_targetColorFormat = GraphicsFormat.R8G8B8A8_UNorm;
			_readbackDstFormat = GraphicsFormat.R8G8_UNorm;

			// When URT is available, skip full RT allocation to reduce Vulkan
			// framebuffer count. The URT path writes to its own _urtOutputRT
			// and never touches the base-class RTHandle.
			var urtManager = URTSensorManager.Instance;
			if (urtManager != null && urtManager.IsSupported)
			{
				_skipRTAllocation = true;
			}

			// Point filtering is critical for segmentation — bilinear filtering
			// would interpolate between class IDs at object edges, creating
			// gradation artifacts that corrupt the discrete label data.
			_rtFilterMode = FilterMode.Point;

			_textureForCapture = new Texture2D(_camParam.image.width, _camParam.image.height, TextureFormat.R16, false, true);
			_textureForCapture.filterMode = FilterMode.Point;
		}

		protected override void SetupCamera()
		{
			// Refer to SegmentationRenderer (Universal Renderer Data)
			_universalCamData.SetRenderer(1);
			_universalCamData.renderPostProcessing = true;
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = false;
			_universalCamData.dithering = true;
			_universalCamData.stopNaN = true;
			_universalCamData.allowHDROutput = false;
			_universalCamData.allowXRRendering = false;
			_universalCamData.antialiasing = AntialiasingMode.FastApproximateAntialiasing;

			// Try Unified RT path first
			InitURTSegmentation();
		}

		/// <summary>
		/// Initialize Unified Ray Tracing for segmentation camera if available.
		/// Instance IDs in the acceleration structure carry segmentation class IDs.
		/// </summary>
		private void InitURTSegmentation()
		{
			var urtManager = URTSensorManager.Instance;
			if (urtManager == null || !urtManager.IsSupported) return;

			var shaderAsset = Resources.Load<ComputeShader>("Shader/URTSegmentationRaycast");
			if (shaderAsset == null) return;

			_urtShader = urtManager.CreateShader(shaderAsset);
			if (_urtShader == null) return;

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			_urtCmd = new CommandBuffer { name = "SegmentationCameraURT" };

			// Pre-allocate scratch buffer
			var scratchSize = _urtShader.GetTraceScratchBufferRequiredSizeInBytes(width, height, 1);
			if (scratchSize > 0)
			{
				_urtScratchBuffer = new GraphicsBuffer(
					UnityEngine.Rendering.UnifiedRayTracing.RayTracingHelper.ScratchBufferTarget,
					(int)((scratchSize + 3) / 4), 4);
			}

			// Set static params
			var cmd = _urtCmd;
			cmd.Clear();
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_Width"), (int)width);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_Height"), (int)height);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_NearClip"), (float)_camParam.clip.near);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_FarClip"), (float)_camParam.clip.far);

			var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = SensorHelper.HorizontalToVerticalFOV(camHFov, (float)width / height);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfHFov"), Mathf.Tan(camHFov * 0.5f * Mathf.Deg2Rad));
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfVFov"), Mathf.Tan(camVFov * 0.5f * Mathf.Deg2Rad));
			Graphics.ExecuteCommandBuffer(cmd);

			_urtOutputRT = new RenderTexture((int)width, (int)height, 0, GraphicsFormat.R8G8B8A8_UNorm)
			{
				name = "URTSegmentationOutput",
				filterMode = FilterMode.Point,
				enableRandomWrite = true,
			};
			_urtOutputRT.Create();

			_useURT = true;
			Debug.Log($"[SegmentationCamera:{DeviceName}] Unified RT enabled (backend: {urtManager.RTContext.BackendType}) — {width}x{height}");
		}

		/// <summary>
		/// Unified RT render path: single dispatch replaces
		/// Camera.Render() + per-object segmentation rendering.
		/// </summary>
		private void ExecuteRenderURT(float realtimeNow)
		{
			var urtManager = URTSensorManager.Instance;
			if (urtManager?.AccelStruct == null) return;

			var capturedTime = GetNextSyntheticTime();
			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			var cmd = _urtCmd;
			cmd.Clear();

			var pos = _camSensor.transform.position;
			_urtShader.SetVectorParam(cmd, Shader.PropertyToID("_CameraOrigin"), new Vector4(pos.x, pos.y, pos.z, 0));
			_urtShader.SetMatrixParam(cmd, Shader.PropertyToID("_CameraToWorld"), _camSensor.transform.localToWorldMatrix);
			_urtShader.SetAccelerationStructure(cmd, "_AccelStruct", urtManager.AccelStruct);
			_urtShader.SetTextureParam(cmd, Shader.PropertyToID("_OutputTex"), _urtOutputRT);

			_urtShader.Dispatch(cmd, _urtScratchBuffer, width, height, 1);
			Graphics.ExecuteCommandBuffer(cmd);

			AsyncGPUReadback.Request(_urtOutputRT, 0, _readbackDstFormat, (req) =>
			{
				if (req.hasError)
				{
					Debug.LogError($"{name}: URT segmentation readback failed");
				}
				else if (req.done)
				{
					var readbackData = req.GetData<byte>();
					ImageProcessing<byte>(ref readbackData, capturedTime);
				}
			});
		}

		/// <summary>
		/// Override readback to handle URT path and match the segmentation
		/// camera's render target. URP rasterization path renders via the
		/// dedicated SegmentationRenderer and reads back from targetTexture.
		/// </summary>
		public override void ExecuteRender(float realtimeNow)
		{
			using (s_SegRenderMarker.Auto())
			{
				AdvanceRenderSchedule(realtimeNow);

				if (_useURT)
				{
					ExecuteRenderURT(realtimeNow);
					return;
				}

				// URP rasterization path: segmentation handled by dedicated renderer (index 1)
				_universalCamData.enabled = true;

				if (_universalCamData.isActiveAndEnabled)
				{
					_camSensor.Render();

					var capturedTime = GetNextSyntheticTime();

					AsyncGPUReadback.Request(_camSensor.targetTexture, 0, _readbackDstFormat, (req) =>
					{
						if (req.hasError)
						{
							Debug.LogError($"{name}: Failed to read segmentation GPU texture (format={_readbackDstFormat})");
						}
						else if (req.done)
						{
							var readbackData = req.GetData<byte>();
							ImageProcessing<byte>(ref readbackData, capturedTime);
						}
					});
				}

				_universalCamData.enabled = false;
			}
		}

		protected override void InitializeMessages()
		{
			base.InitializeMessages();
		}

		new void OnDestroy()
		{
			// Clean up URT resources
			_urtScratchBuffer?.Release();
			_urtScratchBuffer = null;
			_urtCmd?.Release();
			_urtCmd = null;
			_urtShader = null;

			if (_urtOutputRT != null)
			{
				_urtOutputRT.Release();
				_urtOutputRT = null;
			}

			base.OnDestroy();
		}

		void LateUpdate()
		{
			if (_startCameraWork &&
				_camParam.save_enabled &&
				_messageQueue.TryPeek(out var msg))
			{
				var imageStampedMsg = ((messages.Segmentation)msg).ImageStamped;
				var saveName = $"{DeviceName}_{imageStampedMsg.Time.Sec}.{imageStampedMsg.Time.Nsec}";
				_textureForCapture.SaveRawImage(imageStampedMsg.Image.Data, _camParam.save_path, saveName);
			}
		}

		public System.Action<messages.Segmentation> OnSegmentationDataGenerated;

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			var segmentation = new messages.Segmentation();
			segmentation.ImageStamped = new messages.ImageStamped();
			segmentation.ImageStamped.Time = new messages.Time();
			segmentation.ImageStamped.Time.Set(capturedTime);

			segmentation.ImageStamped.Image = new messages.Image();
			segmentation.ImageStamped.Image = _image;

			var image = segmentation.ImageStamped.Image;
			var sizeOfT = UnsafeUtility.SizeOf<T>();
			var byteView = readbackData.Reinterpret<byte>(sizeOfT);

			CopyReadbackToImage(byteView, image.Data);

			// update labels
			var labelInfo = Main.SegmentationManager.GetLabelInfo();
			segmentation.ClassMaps.Clear();
			foreach (var kv in labelInfo)
			{
				if (kv.Value.Count > 0 && !kv.Value[0].Hide)
				{
					var visionClass = new messages.VisionClass()
					{
						ClassName = kv.Key,
						ClassId = kv.Value[0].ClassId
					};
					segmentation.ClassMaps.Add(visionClass);
				}
			}

			if (OnSegmentationDataGenerated != null) OnSegmentationDataGenerated.Invoke(segmentation);

			// Also invoke parent Camera events so CameraPlugin publishes the image natively
			if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(segmentation.ImageStamped);
			if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

			EnqueueMessage(segmentation);
		}
	}
}