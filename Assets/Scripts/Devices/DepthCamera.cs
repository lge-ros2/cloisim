/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.UnifiedRayTracing;
using UnityEngine.Experimental.Rendering;
using System;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		public override bool IsURT => true;

		private ComputeBuffer _computeBufferSrc = null;
		private ComputeBuffer _computeBufferDst = null;

		private float _tanHalfFovH;
		private float _tanHalfFovV;

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
		private float _targetVerticalFovDeg = 0;

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

		[Tooltip("Luminance threshold 0–1. Pixels darker than this are masked out.")]
		[SerializeField, Range(0f, 1f)]
		private float _vcselThreshold = 0.95f;

		[SerializeField]
		private int _bowMode = 3; // 1: Quadratic, 2: Sine, 3: up/down symmetric arch (bow)

		[SerializeField, Range(0.01f, 5.0f)]
		private float _bowAmp = 2f;
		#endregion

		#region "Unified Ray Tracing (per-camera resources)"
		private static ComputeShader ComputeShaderRayTrace = null;
		private ComputeShader _csRayTrace = null;

		private IRayTracingShader _rtShader = null;
		private GraphicsBuffer _rtTraceScratchBuffer = null;
		private CommandBuffer _urtCmdBuffer = null;

		// Cached shader property IDs for the URT shader
		private static readonly int PID_DepthOutput = Shader.PropertyToID("_DepthOutput");
		private static readonly int PID_Width = Shader.PropertyToID("_Width");
		private static readonly int PID_Height = Shader.PropertyToID("_Height");
		private static readonly int PID_NearClip = Shader.PropertyToID("_NearClip");
		private static readonly int PID_FarClip = Shader.PropertyToID("_FarClip");
		private static readonly int PID_CameraPosition = Shader.PropertyToID("_CameraPosition");
		private static readonly int PID_CameraRight = Shader.PropertyToID("_CameraRight");
		private static readonly int PID_CameraUp = Shader.PropertyToID("_CameraUp");
		private static readonly int PID_CameraForward = Shader.PropertyToID("_CameraForward");
		private static readonly int PID_TanHalfFovH = Shader.PropertyToID("_TanHalfFovH");
		private static readonly int PID_TanHalfFovV = Shader.PropertyToID("_TanHalfFovV");
		private static readonly int PID_FlipX = Shader.PropertyToID("_FlipX");
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

			if (ComputeShaderRayTrace == null)
			{
				ComputeShaderRayTrace = Resources.Load<ComputeShader>("Shader/DepthCameraRayTrace");
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

			if (ComputeShaderRayTrace != null)
			{
				Resources.UnloadAsset(ComputeShaderRayTrace);
				ComputeShaderRayTrace = null;
			}

			Resources.UnloadUnusedAssets();
		}

		public void SetDepthScale(in uint value)
		{
			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);
			var imageDepth = CameraData.GetImageDepth(format);

			Debug.Log($"[DepthCamera] format={_camParam.image.format} pixelFormat={format} imageDepth={imageDepth} depthScale={value}");
			SetupDepthBufferScaling(width, height, imageDepth, (float)_camParam.clip.far, (float)value);
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

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			SetupVCSELPrepass(width, height, fovMaskH, fovMaskV);
		}

		public void SetTofVerticalFov(in float targetVerticalFov)
		{
			_targetVerticalFovDeg = targetVerticalFov * Mathf.Rad2Deg;
		}

		new void OnDestroy()
		{
			// Clean up compute shaders
			Destroy(_csDepthScaling);
			_csDepthScaling = null;

			Destroy(_csVcselPrepass);
			_csVcselPrepass = null;

			_computeBufferSrc?.Release();
			_computeBufferDst?.Release();
			_computeBufferBunchFlag?.Release();
			_computeBufferSrc = null;
			_computeBufferDst = null;
			_computeBufferBunchFlag = null;

			// Clean up per-camera URT resources
			_urtCmdBuffer?.Release();
			_urtCmdBuffer = null;

			_rtTraceScratchBuffer?.Dispose();
			_rtTraceScratchBuffer = null;

			if (_csRayTrace != null)
			{
				Destroy(_csRayTrace);
				_csRayTrace = null;
			}

			// Unregister from shared URT manager
			URTSensorManager.Unregister(GetEntityId());

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;
			_skipRTAllocation = true; // URT doesn't need a rasterization render target

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);
			var imageDepth = CameraData.GetImageDepth(format);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			var pixelCount = width * height;
			var packedCount = (imageDepth == 4)
				? pixelCount
				: (imageDepth == 2 ? (pixelCount + 1) / 2 : (pixelCount + 3) / 4);

			_computeBufferSrc?.Release();
			_computeBufferSrc = new ComputeBuffer(pixelCount, sizeof(float));
			_computeBufferDst?.Release();
			_computeBufferDst = new ComputeBuffer(packedCount, sizeof(uint));

			_computeBufferBunchFlag?.Release();
			_computeBufferBunchFlag = new ComputeBuffer(pixelCount, sizeof(int));
		}

		protected override void SetupCamera()
		{
			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			// No rasterization-based depth rendering needed — URT replaces it.
			// Disable URP camera features that are unnecessary for ray tracing.
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.Off;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = false;
			_universalCamData.renderShadows = false;

			// Correct fieldOfView for URT cameras: SetupDefaultCamera computed
			// vFov using _camSensor.aspect which is 1.0 from the 1×1 dummy RT,
			// making fieldOfView = hFov. Recompute using the real image aspect
			// so that ExecuteRender's tanHalfV/tanHalfH are correct.
			var imageAspect = (float)_camParam.image.width / _camParam.image.height;
			var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			_camSensor.fieldOfView = SensorHelper.HorizontalToVerticalFOV(camHFov, imageAspect);

			var vFovRad = _camSensor.fieldOfView * Mathf.Deg2Rad;
			_tanHalfFovV = Mathf.Tan(vFovRad * 0.5f);
			_tanHalfFovH = _tanHalfFovV * imageAspect;

			SetupURTPerCamera();

			Debug.Log($"[DepthCamera] URT initialized (shared Compute backend), " +
				$"image={_camParam.image.width}x{_camParam.image.height}, " +
				$"clip=[{_camParam.clip.near:F2}, {_camParam.clip.far:F2}] vFov={_camSensor.fieldOfView:F1}°");
		}

		private void SetupDepthBufferScaling(in int width, in int height, in int imageDepth, in float clipFar, in float depthScale)
		{
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

			// Debug.Log($"[DepthCamera] Setting up depth buffer scaling compute shader with clipFar={clipFar} and depthScale={depthScale}, imageDepth={imageDepth}");
			_csDepthScaling.SetFloat("_DepthMax", clipFar);
			_csDepthScaling.SetFloat("_DepthScale", depthScale);
			_csDepthScaling.SetInt("_Width", width);
			_csDepthScaling.SetInt("_Height", height);
			_csDepthScaling.SetInt("_UnitSize", imageDepth);

			_csDepthScaling.GetKernelThreadGroupSizes(_kernelScalingIndex, out var threadX, out var threadY, out var _);

			// Consider packWidth, (8bit=4, 16bit=2, 32bit=1)
			var pack = (imageDepth == 4) ? 1 : (imageDepth == 2 ? 2 : 4);
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

			var hFovDeg = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			var vFovDeg = _camSensor.fieldOfView;
			_csVcselPrepass.SetFloat("_FovCamH", hFovDeg * Mathf.Deg2Rad);
			_csVcselPrepass.SetFloat("_FovCamV", ((_targetVerticalFovDeg > 0) ? _targetVerticalFovDeg : vFovDeg) * Mathf.Deg2Rad);

			_csVcselPrepass.SetFloat("_FovMaskH", fovMaskH);
			_csVcselPrepass.SetFloat("_FovMaskV", fovMaskV);

			_csVcselPrepass.SetInt("_BowMode", _bowMode);
			_csVcselPrepass.SetFloat("_BowCenter", 0.5f);
			_csVcselPrepass.SetFloat("_BowAmp", _bowAmp);

			_csVcselPrepass.SetFloat("_MaskThreshold", _vcselThreshold);
			_csVcselPrepass.SetFloat("_IRIntensity", _irIntensity);
			_csVcselPrepass.SetFloat("_IR_FalloffK", _irFalloffK);
			_csVcselPrepass.SetInt("_UseProbDrop", _useProbDrop);

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
				_threadGroupBunchClear = Mathf.CeilToInt((width * height) / (float)clearThreadX);
			}

			if (_kernelMarkBunchIndex >= 0)
			{
				_csVcselPrepass.GetKernelThreadGroupSizes(_kernelMarkBunchIndex, out var markThreadX, out var markThreadY, out _);
				_threadGroupBunchX = Mathf.CeilToInt(width / (float)markThreadX);
				_threadGroupBunchY = Mathf.CeilToInt(height / (float)markThreadY);
			}

			Debug.Log($"[DepthCamera] VCSEL prepass set up with fovCamH={hFovDeg:F1}°, fovCamV={vFovDeg:F1}°, fovMaskH={fovMaskH:F2}, fovMaskV={fovMaskV:F2}");
		}

		#region "Unified Ray Tracing Setup"

		/// <summary>
		/// Set up per-camera URT resources (shader, trace scratch buffer,
		/// command buffer) using the shared URTSensorManager for context
		/// and acceleration structure.
		/// </summary>
		private void SetupURTPerCamera()
		{
			if (!URTSensorManager.Register(GetEntityId()))
			{
				Debug.LogError("[DepthCamera] Failed to register with URTSensorManager");
				return;
			}

			// Instantiate the URT compute shader and wrap it via shared context
			_csRayTrace = Instantiate(ComputeShaderRayTrace);
			if (_csRayTrace == null)
			{
				Debug.LogError("[DepthCamera] Failed to instantiate DepthCameraRayTrace compute shader");
				return;
			}

			_rtShader = URTSensorManager.CreateShader(_csRayTrace);

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			// Create per-camera scratch buffer for ray trace dispatch
			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(_rtShader, width, height, 1);

			_urtCmdBuffer = new CommandBuffer { name = "DepthCamera URT Dispatch" };
		}

		/// <summary>Bind acceleration structure and output buffer to the shader.</summary>
		private void BindShaderResources(CommandBuffer cmd)
		{
			_rtShader.SetAccelerationStructure(cmd, "_AccelStruct", URTSensorManager.AccelStruct);
			_rtShader.SetBufferParam(cmd, PID_DepthOutput, _computeBufferSrc);
		}

		/// <summary>Static camera configuration — image size, clip planes, FOV, flip.</summary>
		private void SetCameraConfigParams(CommandBuffer cmd, uint width, uint height)
		{
			_rtShader.SetIntParam(cmd, PID_Width, (int)width);
			_rtShader.SetIntParam(cmd, PID_Height, (int)height);
			_rtShader.SetFloatParam(cmd, PID_NearClip, (float)_camParam.clip.near);
			_rtShader.SetFloatParam(cmd, PID_FarClip, (float)_camParam.clip.far);

			_rtShader.SetFloatParam(cmd, PID_TanHalfFovH, _tanHalfFovH);
			_rtShader.SetFloatParam(cmd, PID_TanHalfFovV, _tanHalfFovV);
			_rtShader.SetIntParam(cmd, PID_FlipX, 0);
		}

		/// <summary>Dynamic camera pose — changes every frame.</summary>
		private void SetCameraPoseParams(CommandBuffer cmd, Vector3 position, Vector3 right, Vector3 up, Vector3 forward)
		{
			_rtShader.SetVectorParam(cmd, PID_CameraPosition, new Vector4(position.x, position.y, position.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_CameraRight, new Vector4(right.x, right.y, right.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_CameraUp, new Vector4(up.x, up.y, up.z, 0f));
			_rtShader.SetVectorParam(cmd, PID_CameraForward, new Vector4(forward.x, forward.y, forward.z, 0f));
		}

		#endregion

		/// <summary>
		/// URT render path: cast rays on the GPU, then run the VCSEL and
		/// depth-scaling compute passes, and enqueue the result.
		/// All GPU dispatches are recorded into a single CommandBuffer to
		/// guarantee execution order (URT → VCSEL → Scaling).
		/// Uses AsyncGPUReadback to avoid blocking the main thread.
		/// </summary>
		protected override void ExecuteRender(float realtimeNow)
		{
			var manager = URTSensorManager.Instance;
			if (manager == null || URTSensorManager.AccelStruct == null || _rtShader == null)
				return;

			var capturedTime = (Clock != null) ? Clock.SimTime : Time.timeAsDouble;

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			// --- Camera parameters ---
			var camTransform = _camSensor.transform;
			var camPos = camTransform.position;
			var camRight = camTransform.right;
			var camUp = camTransform.up;
			var camForward = camTransform.forward;

			// --- Resize per-camera trace scratch buffer if needed ---
			RayTracingHelper.ResizeScratchBufferForTrace(_rtShader, width, height, 1, ref _rtTraceScratchBuffer);

			// === Record ALL GPU work into a single CommandBuffer ===
			_urtCmdBuffer.Clear();

			// 1. Shared BVH: scene gather, transform update, and build (once per frame)
			URTSensorManager.EnsureBVHReady(_urtCmdBuffer);

			// 2. URT ray trace dispatch
			BindShaderResources(_urtCmdBuffer);
			SetCameraConfigParams(_urtCmdBuffer, width, height);
			SetCameraPoseParams(_urtCmdBuffer, camPos, camRight, camUp, camForward);

			_rtShader.Dispatch(_urtCmdBuffer, _rtTraceScratchBuffer, width, height, 1);

			// 3. VCSEL prepass (optional, operates on _computeBufferSrc in-place)
			if (_csVcselPrepass != null && _kernelVcselIndex > -1)
			{
				_urtCmdBuffer.SetComputeIntParam(_csVcselPrepass, "_BowMode", _bowMode);
				_urtCmdBuffer.SetComputeFloatParam(_csVcselPrepass, "_BowAmp", _bowAmp);
				_urtCmdBuffer.SetComputeFloatParam(_csVcselPrepass, "_IRIntensity", _irIntensity);
				_urtCmdBuffer.SetComputeFloatParam(_csVcselPrepass, "_IR_FalloffK", _irFalloffK);
				_urtCmdBuffer.SetComputeIntParam(_csVcselPrepass, "_DotMode", _dotMode);
				_urtCmdBuffer.SetComputeFloatParam(_csVcselPrepass, "_SpotSigmaPx", _spotSigmaPx);

				_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelVcselIndex, "_DepthBuffer", _computeBufferSrc);
				_urtCmdBuffer.DispatchCompute(_csVcselPrepass, _kernelVcselIndex, _threadGroupVcselX, _threadGroupVcselY, 1);

				// 3b. Screen-space bunch center post-process (DotMode=3)
				if (_dotMode == 3 && _kernelClearBunchIndex >= 0 && _kernelMarkBunchIndex >= 0 && _kernelFilterBunchIndex >= 0)
				{
					// Clear flags
					_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelClearBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_urtCmdBuffer.DispatchCompute(_csVcselPrepass, _kernelClearBunchIndex, _threadGroupBunchClear, 1, 1);

					// Mark centers
					_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelMarkBunchIndex, "_DepthBuffer", _computeBufferSrc);
					_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelMarkBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_urtCmdBuffer.DispatchCompute(_csVcselPrepass, _kernelMarkBunchIndex, _threadGroupBunchX, _threadGroupBunchY, 1);

					// Filter non-centers
					_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelFilterBunchIndex, "_DepthBuffer", _computeBufferSrc);
					_urtCmdBuffer.SetComputeBufferParam(_csVcselPrepass, _kernelFilterBunchIndex, "_BunchFlag", _computeBufferBunchFlag);
					_urtCmdBuffer.DispatchCompute(_csVcselPrepass, _kernelFilterBunchIndex, _threadGroupBunchX, _threadGroupBunchY, 1);
				}
			}

			// 4. Depth buffer scaling → packed output
			//    Re-set _DepthScale every frame so late SetDepthScale() calls take effect.
			if (_csDepthScaling != null && _kernelScalingIndex > -1)
			{
				_urtCmdBuffer.SetComputeBufferParam(_csDepthScaling, _kernelScalingIndex, "_Input", _computeBufferSrc);
				_urtCmdBuffer.SetComputeBufferParam(_csDepthScaling, _kernelScalingIndex, "_Output", _computeBufferDst);
				_urtCmdBuffer.DispatchCompute(_csDepthScaling, _kernelScalingIndex, _threadGroupScalingX, _threadGroupScalingY, 1);
			}

			// === Execute all recorded GPU work atomically ===
			Graphics.ExecuteCommandBuffer(_urtCmdBuffer);

			// --- Async readback (non-blocking) replaces synchronous GetData ---
			AsyncGPUReadback.Request(_computeBufferDst, (req) =>
			{
				if (req.hasError || !req.done)
				{
					Debug.LogWarning($"[DepthCamera] {name}: async GPU readback failed");
					return;
				}

				var src = req.GetData<byte>();

				_timeMsg.Set(capturedTime);
				_imageStamped.Image = _image;

				if (_imageStamped.Image.Data != null && src.Length > 0)
				{
					var copyLen = Math.Min(src.Length, _imageStamped.Image.Data.Length);
					Unity.Collections.NativeArray<byte>.Copy(src, 0, _imageStamped.Image.Data, 0, copyLen);
				}

				EnqueueMessage(_imageStamped);
			});
		}
	}
}