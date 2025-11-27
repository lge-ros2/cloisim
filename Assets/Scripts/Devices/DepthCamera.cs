/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using Unity.Collections;
using Unity.Jobs;
using System.Threading.Tasks;
using System;
using messages = cloisim.msgs;

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		#region "For Compute Shader"

		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader _computeShader = null;
		private int _kernelIndex = -1;
		private const int ThreadGroupsX = 32;
		private const int ThreadGroupsY = 32;
		private int _threadGroupX;
		private int _threadGroupY;

		private ParallelOptions _parallelOptions = null;

		#endregion

		private uint _depthScale = 1;
		private int _imageDepth;
		private const int BatchSize = 64;

		private DepthData.CamBuffer _depthCamBuffer;
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
			if (_depthMaterial != null)
			{
				_depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
		}

		public void FlipXDepthData(in bool flip)
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

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(_computeShader);
			_computeShader = null;

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R8G8B8A8_UNorm;
			_readbackDstFormat = GraphicsFormat.R8G8B8A8_UNorm;

			var width = _camParam.image.width;
			var height = _camParam.image.height;
			var format = CameraData.GetPixelFormat(_camParam.image.format);

			_imageDepth = CameraData.GetImageDepth(format);

			_depthCamBuffer = new DepthData.CamBuffer(width, height);
			_computedBufferOutputUnitLength = width * height;
			_computedBufferOutput = new byte[_computedBufferOutputUnitLength * OutputMaxUnitSize];

			_threadGroupX = Mathf.RoundToInt(width / ThreadGroupsX);
			_threadGroupY = Mathf.RoundToInt(height / ThreadGroupsY);

			_textureForCapture = new Texture2D(width, height, TextureFormat.R8, false);
			_textureForCapture.filterMode = FilterMode.Point;

			_computeShader = Instantiate(ComputeShaderDepthBuffer);

			if (_computeShader != null)
			{
				_kernelIndex = _computeShader.FindKernel("CSScaleDepthBuffer");

				_computeShader.SetFloat("_DepthMax", (float)_camParam.clip.far);
				_computeShader.SetInt("_Width", width);
				_computeShader.SetInt("_UnitSize", _imageDepth);
				_computeShader.SetFloat("_DepthScale", (float)_depthScale);
			}
		}

		protected override void SetupCamera()
		{
			// Debug.Log("Depth Setup Camera");
			var depthShader = Shader.Find("Sensor/Depth");
			_depthMaterial = new Material(depthShader);

			if (_camParam.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				_camParam.image.format = "RGB_FLOAT32";
			}

			_camSensor.clearFlags = CameraClearFlags.Depth;
			_camSensor.allowHDR = false;
			_camSensor.allowMSAA = false;
			_camSensor.depthTextureMode = DepthTextureMode.Depth;

			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

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

		protected override void ImageProcessing(ref NativeArray<byte> readbackData, in double capturedTime)
		{
			var imageStamped = new messages.ImageStamped();
			imageStamped.Time = new messages.Time();
			imageStamped.Time.Set(capturedTime);

			imageStamped.Image = new messages.Image();
			imageStamped.Image = _image;

			_depthCamBuffer.Allocate();
			_depthCamBuffer.raw = readbackData;

			if (_depthCamBuffer.depth.IsCreated && _computeShader != null)
			{
				var jobHandleDepthCamBuffer = _depthCamBuffer.Schedule(_depthCamBuffer.Length(), BatchSize);
				jobHandleDepthCamBuffer.Complete();

				var computeBufferSrc = new ComputeBuffer(_depthCamBuffer.depth.Length, sizeof(float));
				_computeShader.SetBuffer(_kernelIndex, "_Input", computeBufferSrc);
				computeBufferSrc.SetData(_depthCamBuffer.depth);

				var computeBufferDst = new ComputeBuffer(_computedBufferOutput.Length, sizeof(byte));
				_computeShader.SetBuffer(_kernelIndex, "_Output", computeBufferDst);
				_computeShader.Dispatch(_kernelIndex, _threadGroupX + 1, _threadGroupY + 1, 1);
				computeBufferDst.GetData(_computedBufferOutput);

				computeBufferSrc.Release();
				computeBufferDst.Release();

				if (_parallelOptions != null)
				{
					var computeGroupSize = _computedBufferOutputUnitLength / _parallelOptions.MaxDegreeOfParallelism;
					Parallel.For(0, _parallelOptions.MaxDegreeOfParallelism, _parallelOptions, groupIndex =>
					{
						for (var i = 0; i < computeGroupSize ; i++)
						{
							var bufferIndex = computeGroupSize * groupIndex + i;
							var dataIndex = bufferIndex * _imageDepth;
							var outputGroupIndex = bufferIndex * (int)OutputMaxUnitSize;

							for (var j = 0; j < _imageDepth; j++)
							{
								var outputIndex = outputGroupIndex + j;
								imageStamped.Image.Data[dataIndex + j] = _computedBufferOutput[outputIndex];
							}
						}
					});
				}
			}
			_depthCamBuffer.Deallocate();

			_messageQueue.Enqueue(imageStamped);
		}
	}
}