/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
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
			// Debug.Log($"path={vcselPatternPath} mask={_textureVcselMask.name} fovMaskH={_fovMaskHInRad} fovMaskV={_fovMaskVInRad}");

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

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R32_SFloat;
			_readbackDstFormat = GraphicsFormat.R32_SFloat;

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);
			var imageDepth = CameraData.GetImageDepth(format);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			SetupDepthBufferScaling(width, height, imageDepth, (float)_camParam.clip.far);
		}

		protected override void SetupCamera()
		{
			// Debug.Log("Depth Setup Camera");
			var depthShader = Shader.Find("Sensor/DepthRange");
			_depthMaterial = new Material(depthShader);
			_depthMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = true;
			_camSensor.depthTextureMode = DepthTextureMode.Depth;

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

			_csVcselPrepass.SetInt("_DotMode", _dotMode); // 0:Linear, 1:PixelSnap, 2:SoftSpot
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

			// Debug.Log($"VCSEL mask applied '{_textureVcselMask.name}' " +
			// 		$"({_textureVcselMask.width}×{_textureVcselMask.height}), threshold={_vcselThreshold:F2} " +
			// 		$"intensity={_irIntensity} falloffK={_irFalloffK} " +
			// 		$"useProbDrop={_useProbDrop} bowMode={_bowMode} bowAmp={_bowAmp}");
		}

		protected override void ImageProcessing<T>(ref NativeArray<T> readbackData, in double capturedTime) where T : struct
		{
			var imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);

			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			_computeBufferSrc?.SetData(readbackData);

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
				_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Input", _computeBufferSrc);
				_csDepthScaling.SetBuffer(_kernelScalingIndex, "_Output", _computeBufferDst);
				_csDepthScaling.Dispatch(_kernelScalingIndex, _threadGroupScalingX, _threadGroupScalingY, 1);
				_computeBufferDst.GetData(_computedBufferOutput);

				Buffer.BlockCopy(_computedBufferOutput, 0, imageStamped.Image.Data, 0, imageStamped.Image.Data.Length);
			}

			_messageQueue.Enqueue(imageStamped);
		}
	}
}