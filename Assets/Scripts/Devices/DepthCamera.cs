/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading.Tasks;
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{		// ── Profiling ──
		private static readonly ProfilerMarker s_DepthImageProcessMarker = new("DepthCamera.ImageProcessing");
		private static readonly ProfilerMarker s_DepthComputeMarker = new("DepthCamera.ComputeDispatch");
		private static readonly ProfilerMarker s_DepthCopyMarker = new("DepthCamera.CopyOutput");
		private static readonly ProfilerMarker s_DepthRenderMarker = new("DepthCamera.DirectRender");

		#region "For Compute Shader"

		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader _computeShader = null;
		private int _kernelIndex = -1;
		private int _kernelIndexFromTex = -1;
		private int _threadGroupX;
		private int _threadGroupY;
		ComputeBuffer _computeBufferSrc = null;
		ComputeBuffer _computeBufferDst = null;

		private ParallelOptions _parallelOptions = null;

		#endregion

		private uint _depthScale = 1000;
		private int _imageDepth;

		private byte[] _computedBufferOutput;
		private const uint OutputMaxUnitSize = 4;
		private int _computedBufferOutputUnitLength;

		public static void LoadComputeShader()
		{
			if (ComputeShaderDepthBuffer == null)
			{
				ComputeShaderDepthBuffer = Resources.Load<ComputeShader>("Shader/DepthBufferScaling");
			}
		}

		public static void UnloadComputeShader()
		{
			if (ComputeShaderDepthBuffer != null)
			{
				Resources.UnloadAsset(ComputeShaderDepthBuffer);
				Resources.UnloadUnusedAssets();
				ComputeShaderDepthBuffer = null;
			}
		}

		public void ReverseDepthData(in bool reverse)
		{
			if (_depthBlitMaterial != null)
			{
				_depthBlitMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		public void FlipXDepthData(in bool flip)
		{
			if (_depthBlitMaterial != null)
			{
				_depthBlitMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
			}
		}

		public void SetDepthScale(in uint value)
		{
			_depthScale = value;
			if (_computeShader != null)
			{
				_computeShader.SetFloat("_DepthScale", (float)_depthScale);
			}
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(_computeShader);
			_computeShader = null;

			_computeBufferSrc?.Release();
			_computeBufferDst?.Release();

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;
			_targetDepthBits = DepthBits.Depth32;

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);

			_imageDepth = CameraData.GetImageDepth(format);

			_computedBufferOutputUnitLength = width * height;
			_computedBufferOutput = new byte[_computedBufferOutputUnitLength * OutputMaxUnitSize];

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			_computeShader = Instantiate(ComputeShaderDepthBuffer);

			if (_computeShader != null)
			{
				_kernelIndex = _computeShader.FindKernel("CSScaleDepthBuffer");
				_kernelIndexFromTex = _computeShader.FindKernel("CSScaleDepthBufferFromTex");

				_computeShader.SetFloat("_DepthMax", (float)_camParam.clip.far);
				_computeShader.SetInt("_Width", width);
				_computeShader.SetInt("_UnitSize", _imageDepth);
				_computeShader.SetFloat("_DepthScale", (float)_depthScale);

				_computeShader.GetKernelThreadGroupSizes(_kernelIndex, out var threadX, out var threadY, out var _);

				_threadGroupX = Mathf.CeilToInt(width / (float)threadX);
				_threadGroupY = Mathf.CeilToInt(height / (float)threadY);
			}

			_computeBufferSrc = new ComputeBuffer(_computedBufferOutput.Length / sizeof(float), sizeof(float));
			_computeBufferDst = new ComputeBuffer(_computedBufferOutputUnitLength, sizeof(uint));
		}

		// Store depth material separately — don't set _depthMaterial (base class)
		// to prevent OnEndCameraRendering from blitting with stale _CameraDepthTexture.
		// Instead, use an HDRP Custom Pass that runs during the pipeline when
		// _CameraDepthTexture is still valid.
		private Material _depthBlitMaterial = null;
		private CustomPassVolume _customPassVolume = null;
		private DepthCapturePass _depthCapturePass = null;

		protected override void SetupCamera()
		{
			// Debug.Log("Depth Setup Camera");
			// Use the HDRP-native fullscreen shader that handles Tex2DArray depth
			// via LoadCameraDepth() / LOAD_TEXTURE2D_X — required for Unity 6000.x
			// where the depth buffer is a Tex2DArray, not a simple Tex2D.
			var depthShader = Shader.Find("FullScreen/DepthCapture");
			_depthBlitMaterial = new Material(depthShader);
			// Don't set _depthMaterial — keep it null so OnEndCameraRendering
			// skips the depth blit (which would use the stale _CameraDepthTexture).

			// Add HDRP Custom Pass to capture depth during the render pipeline.
			// In Unity 6000.x Render Graph, _CameraDepthTexture is only valid
			// during pipeline execution, not in OnEndCameraRendering callbacks.
			_customPassVolume = gameObject.AddComponent<CustomPassVolume>();
			_customPassVolume.targetCamera = _camSensor;
			_customPassVolume.injectionPoint = CustomPassInjectionPoint.AfterOpaqueDepthAndNormal;
			_customPassVolume.isGlobal = false;

			var depthPass = _customPassVolume.AddPassOfType<DepthCapturePass>();
			if (depthPass is DepthCapturePass dcp)
			{
				dcp.SetDepthMaterial(_depthBlitMaterial);
				dcp.SetTargetCamera(_camSensor);
				_depthCapturePass = dcp;
			}

			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = false;
			_camSensor.depthTextureMode = DepthTextureMode.Depth;
			if (_hdCamData != null)
			{
				// Depth camera only needs geometric depth — disable all visual effects
				// that don't affect _CameraDepthTexture. TransparentObjects stays enabled
				// so transparent environment objects produce depth values.
				var overrideMask = _hdCamData.renderingPathCustomFrameSettingsOverrideMask;
				overrideMask.mask[(uint)FrameSettingsField.Postprocess] = true;
				overrideMask.mask[(uint)FrameSettingsField.ShadowMaps] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSAO] = true;
				overrideMask.mask[(uint)FrameSettingsField.SSR] = true;
				overrideMask.mask[(uint)FrameSettingsField.Volumetrics] = true;
				overrideMask.mask[(uint)FrameSettingsField.ReprojectionForVolumetrics] = true;
				overrideMask.mask[(uint)FrameSettingsField.AtmosphericScattering] = true;
				overrideMask.mask[(uint)FrameSettingsField.ContactShadows] = true;
				overrideMask.mask[(uint)FrameSettingsField.ScreenSpaceShadows] = true;
				overrideMask.mask[(uint)FrameSettingsField.SubsurfaceScattering] = true;
				overrideMask.mask[(uint)FrameSettingsField.Refraction] = true;
				overrideMask.mask[(uint)FrameSettingsField.Decals] = true;
				overrideMask.mask[(uint)FrameSettingsField.TransparentObjects] = true;
				_hdCamData.renderingPathCustomFrameSettingsOverrideMask = overrideMask;

				var frameSettings = _hdCamData.renderingPathCustomFrameSettings;
				frameSettings.SetEnabled(FrameSettingsField.TransparentObjects, true);
				frameSettings.SetEnabled(FrameSettingsField.Postprocess, false);
				frameSettings.SetEnabled(FrameSettingsField.ShadowMaps, false);
				frameSettings.SetEnabled(FrameSettingsField.SSAO, false);
				frameSettings.SetEnabled(FrameSettingsField.SSR, false);
				frameSettings.SetEnabled(FrameSettingsField.Volumetrics, false);
				frameSettings.SetEnabled(FrameSettingsField.ReprojectionForVolumetrics, false);
				frameSettings.SetEnabled(FrameSettingsField.AtmosphericScattering, false);
				frameSettings.SetEnabled(FrameSettingsField.ContactShadows, false);
				frameSettings.SetEnabled(FrameSettingsField.ScreenSpaceShadows, false);
				frameSettings.SetEnabled(FrameSettingsField.SubsurfaceScattering, false);
				frameSettings.SetEnabled(FrameSettingsField.Refraction, false);
				frameSettings.SetEnabled(FrameSettingsField.Decals, false);
				_hdCamData.renderingPathCustomFrameSettings = frameSettings;
			}
// 			_hdCamData.renderShadows = false;

			ReverseDepthData(false);
			FlipXDepthData(false);

			int MaxParallelism = 8;
			do {
				if (_computedBufferOutputUnitLength % MaxParallelism == 0)
				{
					_parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = MaxParallelism };
					break;
				}
			} while (MaxParallelism-- > 0);

			if (_parallelOptions == null)
			{
				Debug.Log($"Check Image size of depth camera!! width={_camParam.image.width} height={_camParam.image.height}");
			}
		}

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			// This path is only used if the direct texture kernel isn't available.
			// Normally ExecuteRender() bypasses this entirely via CSScaleDepthBufferFromTex.
			using (s_DepthImageProcessMarker.Auto())
			{
				if (_computeShader != null)
				{
					using (s_DepthComputeMarker.Auto())
					{
						_computeShader.SetFloat("_DepthScale", (float)_depthScale);
						_computeShader.SetBuffer(_kernelIndex, "_Input", _computeBufferSrc);
						_computeBufferSrc.SetData(readbackData);

						_computeShader.SetBuffer(_kernelIndex, "_Output", _computeBufferDst);
						_computeShader.Dispatch(_kernelIndex, _threadGroupX, _threadGroupY, 1);
					}

					var asyncCapturedTime = capturedTime;
					AsyncGPUReadback.Request(_computeBufferDst, (computeReq) =>
					{
						if (computeReq.hasError || !computeReq.done)
						{
							Debug.LogWarning($"{name}: Depth compute readback failed");
							return;
						}

						using (s_DepthCopyMarker.Auto())
						{
							ProcessComputeOutput(computeReq, asyncCapturedTime);
						}
					});
				}
				else
				{
					var imageStamped = new messages.ImageStamped();
					imageStamped.Time = new messages.Time();
					imageStamped.Time.Set(capturedTime);
					imageStamped.Image = new messages.Image();
					imageStamped.Image = _image;

					if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(imageStamped);
					if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

					_messageQueue.Enqueue(imageStamped);
				}
			}
		}

		/// <summary>
		/// Override ExecuteRender to use direct GPU texture → compute → readback path.
		/// In HDRP Render Graph (Unity 6000.x), _CameraDepthTexture is released after
		/// the render graph completes, making it unavailable in OnEndCameraRendering.
		/// Solution: use an HDRP Custom Pass (DepthCapturePass) that captures depth
		/// during the pipeline at AfterOpaqueDepthAndNormal, writing linearized depth
		/// to a dedicated RT that persists after Camera.Render() returns.
		/// </summary>
		public override void ExecuteRender(float realtimeNow)
		{
			using (s_DepthRenderMarker.Auto())
			{
				_lastCaptureRealtime = realtimeNow;

				_camSensor.Render();

				// Depth blit is handled by the HDRP Custom Pass (DepthCapturePass)
				// which runs during Camera.Render() at AfterOpaqueDepthAndNormal.
				// The captured depth lives in _depthCapturePass.capturedDepthRT
				// (a separate RT that HDRP won't overwrite with color output).
				var depthRT = _depthCapturePass?.capturedDepthRT;
				if (depthRT == null)
				{
					Debug.LogWarning($"{name}: DepthCapturePass has no capturedDepthRT");
					return;
				}

				var capturedTime = DeviceHelper.GetGlobalClock().SimTime;

				if (_computeShader != null && _kernelIndexFromTex >= 0)
				{
					// Direct texture→compute path: no CPU round-trip
					using (s_DepthComputeMarker.Auto())
					{
						_computeShader.SetFloat("_DepthScale", (float)_depthScale);
						_computeShader.SetTexture(_kernelIndexFromTex, "_InputTex", depthRT);
						_computeShader.SetBuffer(_kernelIndexFromTex, "_Output", _computeBufferDst);
						_computeShader.Dispatch(_kernelIndexFromTex, _threadGroupX, _threadGroupY, 1);
					}

					AsyncGPUReadback.Request(_computeBufferDst, (computeReq) =>
					{
						if (computeReq.hasError || !computeReq.done)
						{
							Debug.LogWarning($"{name}: Depth compute readback failed");
							return;
						}

						using (s_DepthCopyMarker.Auto())
						{
							ProcessComputeOutput(computeReq, capturedTime);
						}
					});
				}
				else
				{
					// Fallback to base class flow (double readback) if texture kernel unavailable
					AsyncGPUReadback.Request(depthRT, 0, _readbackDstFormat, (req) =>
					{
						if (req.hasError) return;
						if (req.done)
						{
							var readbackData = req.GetData<float>();
							ImageProcessing<float>(ref readbackData, capturedTime);
						}
					});
				}
			}
		}

		/// <summary>
		/// Shared method to process compute shader output into an ImageStamped message.
		/// </summary>
		private void ProcessComputeOutput(AsyncGPUReadbackRequest computeReq, double capturedTime)
		{
			var imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);
			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			var computeData = computeReq.GetData<byte>();

			if (_parallelOptions != null)
			{
				unsafe
				{
					var srcPtr = (byte*)computeData.GetUnsafeReadOnlyPtr();
					var computeGroupSize = _computedBufferOutputUnitLength / _parallelOptions.MaxDegreeOfParallelism;
					Parallel.For(0, _parallelOptions.MaxDegreeOfParallelism, _parallelOptions, groupIndex =>
					{
						for (var i = 0; i < computeGroupSize; i++)
						{
							var bufferIndex = computeGroupSize * groupIndex + i;
							var dataIndex = bufferIndex * _imageDepth;
							var outputGroupIndex = bufferIndex * (int)OutputMaxUnitSize;

							for (var j = 0; j < _imageDepth; j++)
							{
								imageStamped.Image.Data[dataIndex + j] = srcPtr[outputGroupIndex + j];
							}
						}
					});
				}
			}

			if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(imageStamped);
			if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

			_messageQueue.Enqueue(imageStamped);
		}
	}
}