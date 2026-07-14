/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using System;
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		// Rasterization-based depth acquisition (not URT). The scene depth is encoded
		// into color by DepthRangeRendererFeature, an in-Render-Graph pass that consumes
		// this material (see DepthRangeMaterial below).
		public override bool IsURT => false;

		// Force renderScale=1.0 for this camera's Render() call regardless of the
		// shared URP asset's setting (see Camera.RenderScaleOverride). A <1.0
		// renderScale makes URP render the depth-encoded color target at reduced
		// resolution and bilinearly upscale it; interpolating across a silhouette
		// edge blends near/far depth values into physically meaningless
		// in-between distances, which show up as flying-pixel noise once the
		// depth image is converted to a point cloud.
		protected override float? RenderScaleOverride => 1.0f;

		// Exposed so DepthRangeRendererFeature can look up this camera's depth-encoding
		// material and enqueue a pass for it.
		public Material DepthRangeMaterial => _depthMaterial;

		private ComputeBuffer _computeBufferSrc = null;
		private ComputeBuffer _computeBufferDst = null;

		// Reusable CPU-side buffer for the packed depth readback (avoids per-frame alloc).
		private byte[] _packedOutput = null;

		// Depth packing scale (e.g. 1000 => millimeter units for 16U). Stored so a late
		// SetDepthScale() can reconfigure the scaling compute shader.
		private float _depthScale = 1000f;

		#region "Compute Shader For Depth Buffer Scaling"
		private static ComputeShader ComputeShaderDepthBufferScaling = null;
		private ComputeShader _csDepthScaling = null;
		private int _kernelScalingIndex = -1;
		private int _threadGroupScalingX;
		private int _threadGroupScalingY;
		#endregion

		#region "Compute Shader For VCSEL prepass"
		private static ComputeShader ComputeShaderVCSELPrepass = null;
		private ComputeShader _csVcselPrepass = null;
		private int _kernelVcselIndex = -1;
		private int _threadGroupVcselX;
		private int _threadGroupVcselY;

		// Screen-space bunch center post-process (DotMode=3)
		private ComputeBuffer _computeBufferBunchFlag = null;
		private int _kernelClearBunchIndex = -1;
		private int _kernelMarkBunchIndex = -1;
		private int _kernelFilterBunchIndex = -1;
		private int _threadGroupBunchClear;
		private int _threadGroupBunchX;
		private int _threadGroupBunchY;

		[Header("Properties for VCSEL prepass")]
		[SerializeField]
		private Texture2D _textureVcselMask = null;
		[SerializeField]
		private float _targetVerticalFovRad = 0;

		[SerializeField]
		private int _dotMode = 3; // 0:Linear, 1:PixelSnap, 2:SoftSpot, 3:BunchCenter

		[SerializeField, Range(0.0001f, 10f)]
		private float _spotSigmaPx = 0.1f; // (only for dotMode=2)

		[SerializeField]
		private int _useProbDrop = 1; // 0: Threshold, 1: probability drop

		[SerializeField, Range(0f, 10f)]
		private float _irIntensity = 2f;
		[SerializeField, Range(0f, 10f)]
		private float _irFalloffK = 5f;

		[Tooltip("Luminance threshold 0-1. Pixels darker than this are masked out.")]
		[SerializeField, Range(0f, 1f)]
		private float _vcselThreshold = 0.95f;

		[SerializeField]
		private int _bowMode = 3; // 1: Quadratic, 2: Sine, 3: up/down symmetric arch (bow)

		[SerializeField, Range(0.01f, 5.0f)]
		private float _bowAmp = 2f;
		#endregion

		public static void LoadComputeShader()
		{
			if (ComputeShaderDepthBufferScaling == null)
			{
				ComputeShaderDepthBufferScaling = Resources.Load<ComputeShader>("Shader/DepthBufferScaling");
			}

			if (ComputeShaderVCSELPrepass == null)
			{
				ComputeShaderVCSELPrepass = Resources.Load<ComputeShader>("Shader/VCSELPrepass");
			}
		}

		public static void UnloadComputeShader()
		{
			if (ComputeShaderDepthBufferScaling != null)
			{
				Resources.UnloadAsset(ComputeShaderDepthBufferScaling);
				ComputeShaderDepthBufferScaling = null;
			}

			if (ComputeShaderVCSELPrepass != null)
			{
				Resources.UnloadAsset(ComputeShaderVCSELPrepass);
				ComputeShaderVCSELPrepass = null;
			}
		}

		/// <summary>
		/// Resolve the SDF-configured pixel format for this depth camera, falling back
		/// to a single-channel float depth format for anything that is not a valid
		/// single-channel depth encoding.
		/// </summary>
		private CameraData.PixelFormat GetDepthPixelFormat()
		{
			var format = CameraData.GetPixelFormat(_camParam.ImageFormat);
			switch (format)
			{
				case CameraData.PixelFormat.L_INT8:
				case CameraData.PixelFormat.L_INT16:
				case CameraData.PixelFormat.R_FLOAT16:
				case CameraData.PixelFormat.R_FLOAT32:
					return format;
				default:
					return CameraData.PixelFormat.R_FLOAT32;
			}
		}

		/// <summary>
		/// Number of pixels packed into a single uint32 output element for the given
		/// per-pixel byte depth (8bit=4, 16bit=2, 32bit=1). Shared by SetupTexture()
		/// (buffer sizing) and SetupDepthBufferScaling() (thread group sizing) so the
		/// two never disagree on the packing scheme.
		/// </summary>
		private static int GetPackFactor(in int imageDepth)
		{
			return (imageDepth == 4) ? 1 : (imageDepth == 2 ? 2 : 4);
		}

		public void SetDepthScale(in uint value)
		{
			_depthScale = value;

			var width = (int)_camParam.ImageWidth;
			var height = (int)_camParam.ImageHeight;
			var format = GetDepthPixelFormat();
			var imageDepth = CameraData.GetImageDepth(format);

			SetupDepthBufferScaling(width, height, imageDepth, (float)_camParam.FarClip, (float)value);
		}

		public void SetTofPattern(in string vcselPatternPath, in float fovMaskH, in float fovMaskV)
		{
			_textureVcselMask = MeshLoader.GetTexture(vcselPatternPath);
			if (_textureVcselMask == null)
			{
				Debug.LogError($"[DepthCamera] Failed to load VCSEL mask texture: '{vcselPatternPath}'");
				return;
			}

			_textureVcselMask.name = System.IO.Path.GetFileNameWithoutExtension(vcselPatternPath);
			_textureVcselMask.filterMode = FilterMode.Point;
			_textureVcselMask.wrapMode = TextureWrapMode.Clamp;

			var width = (int)_camParam.ImageWidth;
			var height = (int)_camParam.ImageHeight;
			SetupVCSELPrepass(width, height, fovMaskH, fovMaskV);
		}

		public void SetTofVerticalFov(in float targetVerticalFov)
		{
			_targetVerticalFovRad = targetVerticalFov;
		}

		// Base Camera.OnDestroy() already stops render scheduling, unregisters from
		// SensorRenderManager, and drains in-flight readbacks once; this hook reuses
		// that same drain result instead of repeating it.
		protected override void ReleaseExtraGpuResources(bool quiesced)
		{
			var csDepthScaling = _csDepthScaling;
			_csDepthScaling = null;
			var csVcselPrepass = _csVcselPrepass;
			_csVcselPrepass = null;
			var computeBufferSrc = _computeBufferSrc;
			_computeBufferSrc = null;
			var computeBufferDst = _computeBufferDst;
			_computeBufferDst = null;
			var computeBufferBunchFlag = _computeBufferBunchFlag;
			_computeBufferBunchFlag = null;
			var depthMaterial = _depthMaterial;
			_depthMaterial = null;

			if (quiesced)
			{
				Destroy(csDepthScaling);
				Destroy(csVcselPrepass);
				computeBufferSrc?.Release();
				computeBufferDst?.Release();
				computeBufferBunchFlag?.Release();
				if (depthMaterial != null)
					Destroy(depthMaterial);
			}
			else
			{
				// GPU did not quiesce: an in-flight readback may still reference these
				// buffers. Freeing them now is a use-after-free (SIGSEGV) — defer until
				// the GPU actually catches up.
				URTSensorManager.DeferDispose(() =>
				{
					Destroy(csDepthScaling);
					Destroy(csVcselPrepass);
					computeBufferSrc?.Release();
					computeBufferDst?.Release();
					computeBufferBunchFlag?.Release();
					if (depthMaterial != null)
						Destroy(depthMaterial);
				});
			}
		}

		/// <summary>
		/// Size _image.PixelFormatType/Step/Data from the resolved depth pixel format.
		/// </summary>
		protected override void SetupMessages()
		{
			base.SetupMessages();

			var format = GetDepthPixelFormat();
			_image.PixelFormatType = (messages.PixelFormatType)format;
			_image.Step = _image.Width * (uint)CameraData.GetImageStep(format);
			_image.Data = new byte[_image.Height * _image.Step];
		}

		protected override void SetupTexture()
		{
			// Rasterized depth is rendered to a full-size R32_SFloat color target
			// (Sensor/DepthRange writes planarZ/farClip into the red channel).
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;
			_skipRTAllocation = false; // need a real render target for rasterization

			var width = (int)_camParam.ImageWidth;
			var height = (int)_camParam.ImageHeight;
			var format = GetDepthPixelFormat();
			var imageDepth = CameraData.GetImageDepth(format);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false)
			{
				filterMode = FilterMode.Point
			};

			var pixelCount = width * height;
			var pack = GetPackFactor(imageDepth);
			var packedCount = (pixelCount + pack - 1) / pack;

			_computeBufferSrc?.Release();
			_computeBufferSrc = new ComputeBuffer(pixelCount, sizeof(float));
			_computeBufferDst?.Release();
			_computeBufferDst = new ComputeBuffer(packedCount, sizeof(uint));
			_computeBufferBunchFlag?.Release();
			_computeBufferBunchFlag = new ComputeBuffer(pixelCount, sizeof(int));

			_packedOutput = new byte[packedCount * sizeof(uint)];
		}

		protected override void SetupCamera()
		{
			var depthCameraOutput = SDFormat.Extensions.GetElementValue(_camParam.Element?.FindElement("depth_camera"), "output", string.Empty);
			if (depthCameraOutput.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.ImageFormat = "RGB_FLOAT32";
			}

			// Rasterization: render the scene depth and convert to a linear range image
			// via the Sensor/DepthRange material, applied in-graph by DepthRangeRendererFeature.
			var depthShader = Shader.Find("Sensor/DepthRange");
			if (depthShader == null)
			{
				Debug.LogError("[DepthCamera] Shader 'Sensor/DepthRange' not found. Ensure Assets/Resources/Shader/DepthRange.shader exists.");
			}
			else
			{
				_depthMaterial = new Material(depthShader) { hideFlags = HideFlags.DontUnloadUnusedAsset };
			}

			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = false;

			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			// Correct fieldOfView from the real image aspect (full-size RT).
			var imageAspect = (float)_camParam.ImageWidth / _camParam.ImageHeight;
			var camHFov = (float)_camParam.HorizontalFov * Mathf.Rad2Deg;
			_camSensor.fieldOfView = SensorHelper.HorizontalToVerticalFOV(camHFov, imageAspect);

			Debug.Log($"[DepthCamera] Rasterization depth path initialized, " +
				$"image={_camParam.ImageWidth}x{_camParam.ImageHeight}, " +
				$"clip=[{_camParam.NearClip:F2}, {_camParam.FarClip:F2}] vFov={_camSensor.fieldOfView:F1}");
		}

		private void SetupDepthBufferScaling(in int width, in int height, in int imageDepth, in float clipFar, in float depthScale)
		{
			if (_csDepthScaling != null)
			{
				Destroy(_csDepthScaling);
			}
			_csDepthScaling = Instantiate(ComputeShaderDepthBufferScaling);

			if (_csDepthScaling == null)
			{
				Debug.LogError("Failed to create Depth buffer scaling compute shader");
				return;
			}

			_kernelScalingIndex = _csDepthScaling.FindKernel("CSScaleDepthBuffer");
			if (_kernelScalingIndex < 0)
			{
				Debug.LogWarning("[DepthCamera] failed to find 'CSScaleDepthBuffer' kernel");
				return;
			}

			_csDepthScaling.SetFloat("_DepthMax", clipFar);
			_csDepthScaling.SetFloat("_DepthScale", depthScale);
			_csDepthScaling.SetInt("_Width", width);
			_csDepthScaling.SetInt("_Height", height);
			_csDepthScaling.SetInt("_UnitSize", imageDepth);

			_csDepthScaling.GetKernelThreadGroupSizes(_kernelScalingIndex, out var threadX, out var threadY, out var _);

			var pack = GetPackFactor(imageDepth);
			var packedWidth = Mathf.CeilToInt(width / (float)pack);

			_threadGroupScalingX = Mathf.CeilToInt(packedWidth / (float)threadX);
			_threadGroupScalingY = Mathf.CeilToInt(height / (float)threadY);
		}

		private void SetupVCSELPrepass(in int width, in int height, in float fovMaskH, in float fovMaskV)
		{
			if (_csVcselPrepass == null)
				_csVcselPrepass = Instantiate(ComputeShaderVCSELPrepass);

			if (_csVcselPrepass == null)
			{
				Debug.LogError("Failed to create VCSEL prepass compute shader");
				return;
			}

			_kernelVcselIndex = _csVcselPrepass.FindKernel("CSApplyVcselDepthMask");
			if (_kernelVcselIndex < 0)
			{
				Debug.LogWarning("Failed to find 'CSApplyVcselDepthMask' kernel");
				return;
			}

			_csVcselPrepass.SetTexture(_kernelVcselIndex, "_VcselMaskTex", _textureVcselMask);
			_csVcselPrepass.SetInt("_Width", width);
			_csVcselPrepass.SetInt("_Height", height);

			_csVcselPrepass.SetInt("_DotMode", _dotMode);
			_csVcselPrepass.SetFloat("_SpotSigmaPx", _spotSigmaPx);

			var hFovRad = (float)_camParam.HorizontalFov;
			var vFovRad = _camSensor.fieldOfView * Mathf.Deg2Rad;
			_csVcselPrepass.SetFloat("_FovCamH", hFovRad);
			_csVcselPrepass.SetFloat("_FovCamV", (_targetVerticalFovRad > 0) ? _targetVerticalFovRad : vFovRad);

			_csVcselPrepass.SetFloat("_FovMaskH", fovMaskH);
			_csVcselPrepass.SetFloat("_FovMaskV", fovMaskV);

			_csVcselPrepass.SetInt("_BowMode", _bowMode);
			_csVcselPrepass.SetFloat("_BowCenter", 0.5f);
			_csVcselPrepass.SetFloat("_BowAmp", _bowAmp);

			_csVcselPrepass.SetFloat("_MaskThreshold", _vcselThreshold);
			_csVcselPrepass.SetFloat("_IRIntensity", _irIntensity);
			_csVcselPrepass.SetFloat("_IR_FalloffK", _irFalloffK);
			_csVcselPrepass.SetInt("_UseProbDrop", _useProbDrop);
			// _DepthBuffer holds normalized planarZ/farClip, not meters; the IR falloff
			// term needs physical distance, so convert back using farClip.
			_csVcselPrepass.SetFloat("_DepthMax", (float)_camParam.FarClip);

			_csVcselPrepass.GetKernelThreadGroupSizes(_kernelVcselIndex, out var threadX, out var threadY, out _);

			_threadGroupVcselX = Mathf.CeilToInt(width / (float)threadX);
			_threadGroupVcselY = Mathf.CeilToInt(height / (float)threadY);

			// Screen-space bunch center kernels (for DotMode=3)
			_kernelClearBunchIndex = _csVcselPrepass.FindKernel("CSClearBunchFlags");
			_kernelMarkBunchIndex = _csVcselPrepass.FindKernel("CSMarkBunchCenters");
			_kernelFilterBunchIndex = _csVcselPrepass.FindKernel("CSApplyBunchFilter");

			if (_kernelClearBunchIndex >= 0)
			{
				_csVcselPrepass.GetKernelThreadGroupSizes(_kernelClearBunchIndex, out var clearThreadX, out _, out _);
				_threadGroupBunchClear = Mathf.CeilToInt(width * height / (float)clearThreadX);
			}

			if (_kernelMarkBunchIndex >= 0)
			{
				_csVcselPrepass.GetKernelThreadGroupSizes(_kernelMarkBunchIndex, out var markThreadX, out var markThreadY, out _);
				_threadGroupBunchX = Mathf.CeilToInt(width / (float)markThreadX);
				_threadGroupBunchY = Mathf.CeilToInt(height / (float)markThreadY);
			}
		}

		/// <summary>
		/// Raster path: the base Camera renders depth and async-reads back the
		/// R32_SFloat range image (planarZ/farClip). Here we upload it to the GPU
		/// and run the VCSEL and depth-scaling compute passes, then enqueue.
		/// Runs on the AsyncGPUReadback callback (main thread).
		/// </summary>
		protected override void ImageProcessing<T>(ref Unity.Collections.NativeArray<T> readbackData, in double capturedTime)
		{
			_timeMsg.Set(capturedTime);

			if (_computeBufferSrc == null)
				return;

			_computeBufferSrc.SetData(readbackData);

			// VCSEL prepass (operates on _computeBufferSrc in-place)
			if (_csVcselPrepass != null && _kernelVcselIndex > -1)
			{
				_csVcselPrepass.SetInt("_BowMode", _bowMode);
				_csVcselPrepass.SetFloat("_BowAmp", _bowAmp);
				_csVcselPrepass.SetFloat("_IRIntensity", _irIntensity);
				_csVcselPrepass.SetFloat("_IR_FalloffK", _irFalloffK);
				_csVcselPrepass.SetInt("_DotMode", _dotMode);
				_csVcselPrepass.SetFloat("_SpotSigmaPx", _spotSigmaPx);

				_csVcselPrepass.SetBuffer(_kernelVcselIndex, "_DepthBuffer", _computeBufferSrc);
				_csVcselPrepass.Dispatch(_kernelVcselIndex, _threadGroupVcselX, _threadGroupVcselY, 1);

				// Screen-space bunch center post-process (DotMode=3)
				if (_dotMode == 3 && _kernelClearBunchIndex >= 0 && _kernelMarkBunchIndex >= 0 && _kernelFilterBunchIndex >= 0)
				{
					_csVcselPrepass.SetBuffer(_kernelClearBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_csVcselPrepass.Dispatch(_kernelClearBunchIndex, _threadGroupBunchClear, 1, 1);

					_csVcselPrepass.SetBuffer(_kernelMarkBunchIndex, "_DepthBuffer", _computeBufferSrc);
					_csVcselPrepass.SetBuffer(_kernelMarkBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_csVcselPrepass.Dispatch(_kernelMarkBunchIndex, _threadGroupBunchX, _threadGroupBunchY, 1);

					_csVcselPrepass.SetBuffer(_kernelFilterBunchIndex, "_DepthBuffer", _computeBufferSrc);
					_csVcselPrepass.SetBuffer(_kernelFilterBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_csVcselPrepass.Dispatch(_kernelFilterBunchIndex, _threadGroupBunchX, _threadGroupBunchY, 1);
				}
			}

			// Depth buffer scaling -> packed output
			if (_csDepthScaling != null && _kernelScalingIndex > -1)
			{
				_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Input", _computeBufferSrc);
				_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Output", _computeBufferDst);
				_csDepthScaling.Dispatch(_kernelScalingIndex, _threadGroupScalingX, _threadGroupScalingY, 1);

				_computeBufferDst.GetData(_packedOutput);

				if (_image.Data != null)
				{
					var copyLen = Math.Min(_packedOutput.Length, _image.Data.Length);
					Buffer.BlockCopy(_packedOutput, 0, _image.Data, 0, copyLen);
				}
			}

			EnqueueMessage(_image);
		}
	}
}
