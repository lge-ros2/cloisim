/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Profiling;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System;
using System.Threading.Tasks;
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		// ── Profiling ──
		private static readonly ProfilerMarker s_DepthImageProcessMarker = new("DepthCamera.ImageProcessing");
		private static readonly ProfilerMarker s_DepthComputeMarker = new("DepthCamera.ComputeDispatch");
		private static readonly ProfilerMarker s_DepthCopyMarker = new("DepthCamera.CopyOutput");
		private static readonly ProfilerMarker s_DepthRenderMarker = new("DepthCamera.DirectRender");

		#region "Compute Shader For Depth Buffer Scaling"
		private static ComputeShader ComputeShaderDepthBufferScaling = null;
		private ComputeShader _csDepthScaling = null;
		private int _kernelScalingIndex = -1;
		private int _kernelScalingFromTexIndex = -1;
		private int _threadGroupScalingX;
		private int _threadGroupScalingY;
		ComputeBuffer _computeBufferSrc = null;
		ComputeBuffer _computeBufferDst = null;
		private byte[] _computedBufferOutput;
		private int _computedBufferOutputUnitLength;
		private const uint OutputMaxUnitSize = 4;
		private int _imageDepth;

		private ParallelOptions _parallelOptions = null;
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

		#region "Unified Ray Tracing"

		// When Unified RT is available (hardware or compute backend), replaces
		// Camera.Render() + depth blit + DepthBufferScaling with a single dispatch.
		private bool _useURT = false;
		private UnityEngine.Rendering.UnifiedRayTracing.IRayTracingShader _urtShader;
		private GraphicsBuffer _urtScratchBuffer;
		private CommandBuffer _urtCmd;

		public override bool IsURT => _useURT;

		#endregion

		private uint _depthScale = 1;

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

			Resources.UnloadUnusedAssets();
		}

		private void ReverseDepthData(in bool reverse)
		{
			if (_depthMaterial != null)
			{
				_depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		private void FlipXDepthData(in bool flip)
		{
			if (_depthMaterial != null)
			{
				_depthMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
			}
		}

		public void SetDepthScale(in uint value)
		{
			_depthScale = value;
			if (_csDepthScaling != null)
			{
				_csDepthScaling.SetFloat("_DepthScale", (float)_depthScale);
			}
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
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(_csDepthScaling);
			_csDepthScaling = null;

			Destroy(_csVcselPrepass);
			_csVcselPrepass = null;

			_computeBufferSrc?.Release();
			_computeBufferDst?.Release();

			_computeBufferSrc = null;
			_computeBufferDst = null;

			// Clean up URT resources
			_urtScratchBuffer?.Release();
			_urtScratchBuffer = null;
			_urtCmd?.Release();
			_urtCmd = null;
			_urtShader = null;

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;

			// When URT is available, skip full RT allocation to reduce Vulkan
			// framebuffer count. The URT path writes to a ComputeBuffer directly
			// and never touches the base-class RTHandle.
			var urtManager = URTSensorManager.Instance;
			if (urtManager != null && urtManager.IsSupported)
			{
				_skipRTAllocation = true;
			}

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);
			_imageDepth = CameraData.GetImageDepth(format);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			SetupDepthBufferScaling(width, height, _imageDepth, (float)_camParam.clip.far);
		}

		protected override void SetupCamera()
		{
			// Try Unified RT path first
			InitURTDepth();

			if (!_useURT)
			{
				// Rasterization path: use DepthRange shader via base class _depthMaterial
				var depthShader = Shader.Find("Sensor/DepthRange");
				_depthMaterial = new Material(depthShader);
				_depthMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
			}

			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = true;
			_camSensor.depthTextureMode = DepthTextureMode.Depth;

			// URP-specific camera settings: only need depth, disable unnecessary features
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			ReverseDepthData(false);
			FlipXDepthData(false);
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

			// Optional: direct texture→compute kernel (avoids CPU round-trip when available)
			_kernelScalingFromTexIndex = _csDepthScaling.FindKernel("CSScaleDepthBufferFromTex");

			_csDepthScaling.SetFloat("_DepthMax", clipFar);
			_csDepthScaling.SetFloat("_DepthScale", (float)_depthScale);
			_csDepthScaling.SetInt("_Width", width);
			_csDepthScaling.SetInt("_Height", height);
			_csDepthScaling.SetInt("_UnitSize", imageDepth);

			_csDepthScaling.GetKernelThreadGroupSizes(_kernelScalingIndex, out var threadX, out var threadY, out var _);

			// Each shader thread writes one uint32 per pixel (_Output[y*Width+x]).
			// Dispatch and buffer must cover the full pixel grid.
			_threadGroupScalingX = Mathf.CeilToInt(width / (float)threadX);
			_threadGroupScalingY = Mathf.CeilToInt(height / (float)threadY);

			var pixelCount = width * height;
			_computedBufferOutputUnitLength = pixelCount;

			_computeBufferSrc = new ComputeBuffer(pixelCount, sizeof(float));
			_computeBufferDst = new ComputeBuffer(pixelCount, sizeof(uint));
			_computedBufferOutput = new byte[pixelCount * sizeof(uint)];

			// Configure parallelism for ProcessComputeOutput
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
				Debug.Log($"Check Image size of depth camera!! width={width} height={height}");
			}
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

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			// This path handles the standard readback flow.
			// When ExecuteRender uses the direct texture→compute path, this is bypassed.
			using (s_DepthImageProcessMarker.Auto())
			{
				var imageStamped = new messages.ImageStamped();
				imageStamped.Time = new messages.Time();
				imageStamped.Time.Set(capturedTime);
				imageStamped.Image = new messages.Image();
				imageStamped.Image = _image;

				_computeBufferSrc?.SetData(readbackData);

				// Apply VCSEL prepass if configured
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
				}

				if (_csDepthScaling != null && _kernelScalingIndex > -1)
				{
					using (s_DepthComputeMarker.Auto())
					{
						_csDepthScaling.SetFloat("_DepthScale", (float)_depthScale);
						_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Input", _computeBufferSrc);
						_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Output", _computeBufferDst);
						_csDepthScaling.Dispatch(_kernelScalingIndex, _threadGroupScalingX, _threadGroupScalingY, 1);
					}

					_computeBufferDst.GetData(_computedBufferOutput);

					// Each uint32 holds one depth value in its lower _imageDepth bytes.
					// Extract _imageDepth bytes per pixel from the uint32 array.
					if (_imageDepth == (int)OutputMaxUnitSize)
					{
						Buffer.BlockCopy(_computedBufferOutput, 0, imageStamped.Image.Data, 0, imageStamped.Image.Data.Length);
					}
					else
					{
						unsafe
						{
							fixed (byte* srcPtr = _computedBufferOutput)
							fixed (byte* dstPtr = imageStamped.Image.Data)
							{
								for (int i = 0; i < _computedBufferOutputUnitLength; i++)
								{
									var si = i * (int)OutputMaxUnitSize;
									var di = i * _imageDepth;
									for (int j = 0; j < _imageDepth; j++)
										dstPtr[di + j] = srcPtr[si + j];
								}
							}
						}
					}
				}

				if (OnCameraDataGenerated != null) OnCameraDataGenerated.Invoke(imageStamped);
				if (OnCameraInfoGenerated != null) OnCameraInfoGenerated.Invoke(_sensorInfo);

				_messageQueue.Enqueue(imageStamped);
			}
		}

		/// <summary>
		/// Initialize Unified Ray Tracing for depth camera if available.
		/// </summary>
		private void InitURTDepth()
		{
			var urtManager = URTSensorManager.Instance;
			if (urtManager == null || !urtManager.IsSupported) return;

			var shaderAsset = Resources.Load<ComputeShader>("Shader/URTDepthRaycast");
			if (shaderAsset == null) return;

			_urtShader = urtManager.CreateShader(shaderAsset);
			if (_urtShader == null) return;

			var width = (uint)_camParam.image.width;
			var height = (uint)_camParam.image.height;

			_urtCmd = new CommandBuffer { name = "DepthCameraURT" };

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
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_DepthScale"), (float)_depthScale);
			_urtShader.SetIntParam(cmd, Shader.PropertyToID("_UnitSize"), _imageDepth);

			var camHFov = (float)_camParam.horizontal_fov * Mathf.Rad2Deg;
			var camVFov = SensorHelper.HorizontalToVerticalFOV(camHFov, (float)width / height);
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfHFov"), Mathf.Tan(camHFov * 0.5f * Mathf.Deg2Rad));
			_urtShader.SetFloatParam(cmd, Shader.PropertyToID("_TanHalfVFov"), Mathf.Tan(camVFov * 0.5f * Mathf.Deg2Rad));
			Graphics.ExecuteCommandBuffer(cmd);

			_useURT = true;
			Debug.Log($"[DepthCamera:{DeviceName}] Unified RT enabled (backend: {urtManager.RTContext.BackendType}) — {width}x{height}");
		}

		/// <summary>
		/// Unified RT render path: single dispatch replaces
		/// Camera.Render() + depth blit + DepthBufferScaling.
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
			_urtShader.SetBufferParam(cmd, Shader.PropertyToID("_Output"), _computeBufferDst);

			_urtShader.Dispatch(cmd, _urtScratchBuffer, width, height, 1);
			Graphics.ExecuteCommandBuffer(cmd);

			AsyncGPUReadback.Request(_computeBufferDst, (computeReq) =>
			{
				if (computeReq.hasError || !computeReq.done)
				{
					Debug.LogWarning($"{name}: URT depth readback failed");
					return;
				}

				using (s_DepthCopyMarker.Auto())
				{
					ProcessComputeOutput(computeReq, capturedTime);
				}
				SignalDataReady();
			});
		}

		public override void ExecuteRender(float realtimeNow)
		{
			using (s_DepthRenderMarker.Auto())
			{
				AdvanceRenderSchedule(realtimeNow);

				if (_useURT)
				{
					ExecuteRenderURT(realtimeNow);
					return;
				}

				_universalCamData.enabled = true;

				if (_universalCamData.isActiveAndEnabled)
				{
					_camSensor.Render();
				}

				// After Camera.Render(), the depth was blitted to targetTexture
				// by OnEndCameraRendering via the DepthRange shader (_depthMaterial).
				var targetRT = _camSensor.targetTexture;
				if (targetRT == null)
				{
					_universalCamData.enabled = false;
					Debug.LogWarning($"{name}: targetTexture is null after render");
					return;
				}

				var capturedTime = GetNextSyntheticTime();

				// Direct texture→compute path if the kernel is available (avoids CPU round-trip)
				if (_csDepthScaling != null && _kernelScalingFromTexIndex >= 0)
				{
					using (s_DepthComputeMarker.Auto())
					{
						_csDepthScaling.SetFloat("_DepthScale", (float)_depthScale);
						_csDepthScaling.SetTexture(_kernelScalingFromTexIndex, "_InputTex", targetRT);
						_csDepthScaling.SetBuffer(_kernelScalingFromTexIndex, "_Output", _computeBufferDst);
						_csDepthScaling.Dispatch(_kernelScalingFromTexIndex, _threadGroupScalingX, _threadGroupScalingY, 1);
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
						SignalDataReady();
					});
				}
				else
				{
					// Standard path: async readback from targetTexture → ImageProcessing
					AsyncGPUReadback.Request(targetRT, 0, _readbackDstFormat, (req) =>
					{
						if (req.hasError)
						{
							Debug.LogError($"{name}: Failed to read GPU texture (format={_readbackDstFormat})");
							return;
						}
						if (req.done)
						{
							var readbackData = req.GetData<float>();
							ImageProcessing<float>(ref readbackData, capturedTime);
						}
						SignalDataReady();
					});
				}

				_universalCamData.enabled = false;
			}
		}

		/// <summary>
		/// Shared method to process compute shader output into an ImageStamped message.
		/// Used by both the direct texture→compute path and (when available) the URT path.
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
			SignalDataReady();
		}
	}
}
