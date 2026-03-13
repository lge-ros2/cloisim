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
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		#region "Compute Shader For Depth Buffer Scaling"
		private uint _depthScale = 1;
		private static ComputeShader ComputeShaderDepthBufferScaling = null;
		private ComputeShader _csDepthScaling = null;
		private int _kernelScalingIndex = -1;
		private int _threadGroupScalingX;
		private int _threadGroupScalingY;
		ComputeBuffer _computeBufferSrc = null;
		ComputeBuffer _computeBufferDst = null;
		private byte[] _computedBufferOutput;
		#endregion

		#region "Compute Shader For VCSEL prepass"
		private static ComputeShader ComputeShaderVCSELPrepass = null;
		private ComputeShader _csVcselPrepass = null;
		private int _kernelVcselIndex = -1;
		private int _threadGroupVcselX;
		private int _threadGroupVcselY;

		[SerializeField]
		private Texture2D _textureVcselMask = null;
		[SerializeField]
		private float _fovMaskHInRad = 0;
		[SerializeField]
		private float _fovMaskVInRad = 0;
		[SerializeField]
		private float _targetVerticalFovDeg = 0;

		[SerializeField]
		private int _dotMode = 2; // 0:Linear, 1:PixelSnap, 2:SoftSpot

		[SerializeField, Range(0.0001f, 10f)]
		private float _spotSigmaPx = 0.1f; // (only for dotMode=2)

		[SerializeField]
		private int _useProbDrop = 1; // 0: Threshold, 1: probability drop

		[SerializeField, Range(0f, 10f)]
		private float _irIntensity = 2f;
		[SerializeField, Range(0f, 10f)]
		private float _irFalloffK = 5f;

		[Header("Properties for VCSEL prepass")]
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

		public override bool IsURT => true;

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
			_depthScale = value;
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

			_fovMaskHInRad = fovMaskH;
			_fovMaskVInRad = fovMaskV;

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			SetupVCSELPrepass(width, height);
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
			_computeBufferSrc = null;
			_computeBufferDst = null;

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
			URTSensorManager.Instance?.Unregister(GetInstanceID());

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

			SetupDepthBufferScaling(width, height, imageDepth, (float)_camParam.clip.far);

			Debug.Log($"[DepthCamera] format={_camParam.image.format} pixelFormat={format} " +
				$"imageDepth={imageDepth} depthScale={_depthScale}");
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

			SetupURTPerCamera();

			Debug.Log($"[DepthCamera] URT initialized (shared Compute backend), " +
				$"image={_camParam.image.width}x{_camParam.image.height}, " +
				$"clip=[{_camParam.clip.near:F2}, {_camParam.clip.far:F2}]");
		}

		private void SetupDepthBufferScaling(in int width, in int height, in int imageDepth, in float clipFar)
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

			_csDepthScaling.SetFloat("_DepthMax", clipFar);
			_csDepthScaling.SetFloat("_DepthScale", (float)_depthScale);
			_csDepthScaling.SetInt("_Width", width);
			_csDepthScaling.SetInt("_Height", height);
			_csDepthScaling.SetInt("_UnitSize", imageDepth);

			_csDepthScaling.GetKernelThreadGroupSizes(_kernelScalingIndex, out var threadX, out var threadY, out var _);

			// Consider packWidth, (8bit=4, 16bit=2, 32bit=1)
			var pack = (imageDepth == 4) ? 1 : (imageDepth == 2 ? 2 : 4);
			var packedWidth = Mathf.CeilToInt(width / (float)pack);

			_threadGroupScalingX = Mathf.CeilToInt(packedWidth / (float)threadX);
			_threadGroupScalingY = Mathf.CeilToInt(height / (float)threadY);

			var pixelCount = width * height;
			var packedCount = (imageDepth == 4)
				? pixelCount
				: (imageDepth == 2 ? (pixelCount + 1) / 2 : (pixelCount + 3) / 4);

			_computeBufferSrc = new ComputeBuffer(pixelCount, sizeof(float));
			_computeBufferDst = new ComputeBuffer(packedCount, sizeof(uint));
			_computedBufferOutput = new byte[packedCount * sizeof(uint)];
		}

		private void SetupVCSELPrepass(in int width, in int height)
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

			_camSensor.allowMSAA = false;

			_csVcselPrepass.SetTexture(_kernelVcselIndex, "_VcselMaskTex", _textureVcselMask);
			_csVcselPrepass.SetInt("_Width", width);
			_csVcselPrepass.SetInt("_Height", height);

			_csVcselPrepass.SetInt("_DotMode", _dotMode);
			_csVcselPrepass.SetFloat("_SpotSigmaPx", _spotSigmaPx);

			var fovV = _camSensor.fieldOfView;
			var aspect = (float)_camParam.image.width / _camParam.image.height;
			var fovH = 2f * Mathf.Atan(Mathf.Tan(fovV * Mathf.Deg2Rad * 0.5f) * aspect) * Mathf.Rad2Deg;

			_csVcselPrepass.SetFloat("_FovCamH", fovH * Mathf.Deg2Rad);
			_csVcselPrepass.SetFloat("_FovCamV", ((_targetVerticalFovDeg > 0) ? _targetVerticalFovDeg : fovV) * Mathf.Deg2Rad);

			_csVcselPrepass.SetFloat("_FovMaskH", _fovMaskHInRad);
			_csVcselPrepass.SetFloat("_FovMaskV", _fovMaskVInRad);

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
		}

		#region "Unified Ray Tracing Setup"

		/// <summary>
		/// Set up per-camera URT resources (shader, trace scratch buffer,
		/// command buffer) using the shared URTSensorManager for context
		/// and acceleration structure.
		/// </summary>
		private void SetupURTPerCamera()
		{
			var manager = URTSensorManager.Instance;
			if (manager == null || !manager.Register(GetInstanceID()))
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

			_rtShader = manager.CreateShader(_csRayTrace);

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			// Create per-camera scratch buffer for ray trace dispatch
			_rtTraceScratchBuffer = RayTracingHelper.CreateScratchBufferForTrace(
				_rtShader, width, height, 1);

			_urtCmdBuffer = new CommandBuffer { name = "DepthCamera URT Dispatch" };
		}

		#endregion

		/// <summary>
		/// URT render path: cast rays on the GPU, then run the VCSEL and
		/// depth-scaling compute passes, and enqueue the result.
		/// All GPU dispatches are recorded into a single CommandBuffer to
		/// guarantee execution order (URT → VCSEL → Scaling).
		/// </summary>
		protected override void ExecuteRender(float realtimeNow)
		{
			var manager = URTSensorManager.Instance;
			if (manager == null || manager.AccelStruct == null || _rtShader == null)
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

			var vFovRad = _camSensor.fieldOfView * Mathf.Deg2Rad;
			var tanHalfV = Mathf.Tan(vFovRad * 0.5f);
			var tanHalfH = tanHalfV * ((float)width / height);

			// --- Resize per-camera trace scratch buffer if needed ---
			RayTracingHelper.ResizeScratchBufferForTrace(_rtShader, width, height, 1, ref _rtTraceScratchBuffer);

			// === Record ALL GPU work into a single CommandBuffer ===
			_urtCmdBuffer.Clear();

			// 1. Shared BVH: scene gather, transform update, and build (once per frame)
			manager.EnsureBVHReady(_urtCmdBuffer);

			// 2. URT ray trace dispatch
			_rtShader.SetAccelerationStructure(_urtCmdBuffer, "_AccelStruct", manager.AccelStruct);
			_rtShader.SetBufferParam(_urtCmdBuffer, PID_DepthOutput, _computeBufferSrc);

			_rtShader.SetIntParam(_urtCmdBuffer, PID_Width, (int)width);
			_rtShader.SetIntParam(_urtCmdBuffer, PID_Height, (int)height);
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_NearClip, (float)_camParam.clip.near);
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_FarClip, (float)_camParam.clip.far);

			_rtShader.SetVectorParam(_urtCmdBuffer, PID_CameraPosition,
				new Vector4(camPos.x, camPos.y, camPos.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_CameraRight,
				new Vector4(camRight.x, camRight.y, camRight.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_CameraUp,
				new Vector4(camUp.x, camUp.y, camUp.z, 0f));
			_rtShader.SetVectorParam(_urtCmdBuffer, PID_CameraForward,
				new Vector4(camForward.x, camForward.y, camForward.z, 0f));

			_rtShader.SetFloatParam(_urtCmdBuffer, PID_TanHalfFovH, tanHalfH);
			_rtShader.SetFloatParam(_urtCmdBuffer, PID_TanHalfFovV, tanHalfV);
			_rtShader.SetIntParam(_urtCmdBuffer, PID_FlipX, 0);

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
			}

			// 4. Depth buffer scaling → packed output
			//    Re-set _DepthScale every frame so late SetDepthScale() calls take effect.
			if (_csDepthScaling != null && _kernelScalingIndex > -1)
			{
				_urtCmdBuffer.SetComputeFloatParam(_csDepthScaling, "_DepthScale", (float)_depthScale);
				_urtCmdBuffer.SetComputeFloatParam(_csDepthScaling, "_DepthMax", (float)_camParam.clip.far);
				_urtCmdBuffer.SetComputeBufferParam(_csDepthScaling, _kernelScalingIndex, "_Input", _computeBufferSrc);
				_urtCmdBuffer.SetComputeBufferParam(_csDepthScaling, _kernelScalingIndex, "_Output", _computeBufferDst);
				_urtCmdBuffer.DispatchCompute(_csDepthScaling, _kernelScalingIndex, _threadGroupScalingX, _threadGroupScalingY, 1);
			}

			// === Execute all recorded GPU work atomically ===
			Graphics.ExecuteCommandBuffer(_urtCmdBuffer);

			// --- Sync readback (blocks until all GPU work completes) ---
			_computeBufferDst?.GetData(_computedBufferOutput);

			// --- Enqueue message ---
			var imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);
			imageStamped.Image = _image;

			if (_computedBufferOutput != null)
			{
				Buffer.BlockCopy(_computedBufferOutput, 0, imageStamped.Image.Data, 0,
					Math.Min(_computedBufferOutput.Length, imageStamped.Image.Data.Length));
			}

			EnqueueMessage(imageStamped);
		}
	}
}