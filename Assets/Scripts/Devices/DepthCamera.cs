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

namespace SensorDevices
{
	[RequireComponent(typeof(UnityEngine.Camera))]
	public class DepthCamera : Camera
	{
		#region "For Compute Shader"

		private static ComputeShader ComputeShaderDepthBuffer = null;
		private ComputeShader computeShader = null;
		private int _kernelIndex = -1;
		private const int ThreadGroupsX = 32;
		private const int ThreadGroupsY = 32;
		private int _threadGroupX;
		private int _threadGroupY;

		#endregion

		private Material depthMaterial = null;

		private uint depthScale = 1;
		private int _imageDepth;

		private const int BatchSize = 64;
		private DepthData.CamBuffer _depthCamBuffer;
		private byte[] _computedBufferOutput;
		private const uint OutputUnitSize = 4;
		private int _computedBufferOutputUnitLength;
		private Texture2D _textureForCapture;


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
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_ReverseData", (reverse) ? 1 : 0);
			}
			else
				Debug.Log("is null");
		}

		public void FlipXDepthData(in bool flip)
		{
			if (depthMaterial != null)
			{
				depthMaterial.SetInt("_FlipX", (flip) ? 1 : 0);
			}
		}

		public void SetDepthScale(in uint value)
		{
			depthScale = value;

			if (computeShader != null)
			{
				computeShader.SetFloat("_DepthScale", (float)depthScale);
			}
		}

		new void OnDestroy()
		{
			// Debug.Log("OnDestroy(Depth Camera)");
			Destroy(computeShader);
			computeShader = null;

			base.OnDestroy();
		}

		protected override void SetupTexture()
		{
			computeShader = Instantiate(ComputeShaderDepthBuffer);
			_kernelIndex = computeShader.FindKernel("CSScaleDepthBuffer");

			var depthShader = Shader.Find("Sensor/Depth");
			depthMaterial = new Material(depthShader);

			if (camParameter.depth_camera_output.Equals("points"))
			{
				Debug.Log("Enable Point Cloud data mode - NOT SUPPORT YET!");
				camParameter.image.format = "RGB_FLOAT32";
			}

			camSensor.clearFlags = CameraClearFlags.Depth;
			camSensor.allowHDR = false;
			camSensor.depthTextureMode = DepthTextureMode.Depth;
			_universalCamData.requiresColorOption = CameraOverrideOption.Off;
			_universalCamData.requiresDepthOption = CameraOverrideOption.On;
			_universalCamData.requiresColorTexture = false;
			_universalCamData.requiresDepthTexture = true;
			_universalCamData.renderShadows = false;

			_targetRTname = "CameraDepthTexture";
			_targetColorFormat = GraphicsFormat.R8G8B8A8_UNorm;
			_readbackDstFormat = GraphicsFormat.R8G8B8A8_UNorm;

			var cb = new CommandBuffer();
			cb.name = "CommandBufferForDepthShading";
			var tempTextureId = Shader.PropertyToID("_RenderImageCameraDepthTexture");
			cb.GetTemporaryRT(tempTextureId, -1, -1);
			cb.Blit(tempTextureId, BuiltinRenderTextureType.CameraTarget, depthMaterial);
			cb.ReleaseTemporaryRT(tempTextureId);
			camSensor.AddCommandBuffer(CameraEvent.AfterEverything, cb);
			cb.Release();

			var width = camParameter.image.width;
			var height = camParameter.image.height;
			var format = CameraData.GetPixelFormat(camParameter.image.format);
			GraphicsFormat graphicFormat;
			switch (format)
			{
				case CameraData.PixelFormat.L_INT8:
					graphicFormat = GraphicsFormat.R8_UNorm;
					break;
				case CameraData.PixelFormat.R_FLOAT32:
					graphicFormat = GraphicsFormat.R16G16_UNorm;
					break;
				case CameraData.PixelFormat.L_INT16:
				default:
					graphicFormat = GraphicsFormat.R16_UNorm;
					break;
			}

			_depthCamBuffer = new DepthData.CamBuffer(width, height);
			_computedBufferOutputUnitLength = width * height;
			_computedBufferOutput = new byte[_computedBufferOutputUnitLength * OutputUnitSize];

			if (computeShader != null)
			{
				_imageDepth = CameraData.GetImageDepth(format);
				computeShader.SetFloat("_DepthMax", (float)camParameter.clip.far);
				computeShader.SetInt("_Width", width);
				computeShader.SetInt("_UnitSize", _imageDepth);
			}

			ReverseDepthData(false);
			FlipXDepthData(false);

			_threadGroupX = width / ThreadGroupsX;
			_threadGroupY = height / ThreadGroupsY;

			_textureForCapture = new Texture2D(width, height, graphicFormat, 0, TextureCreationFlags.None);
			_textureForCapture.filterMode = FilterMode.Point;
		}

		protected override void ImageProcessing(ref NativeArray<byte> readbackData)
		{
			_depthCamBuffer.Allocate();
			_depthCamBuffer.raw = readbackData;

			if (_depthCamBuffer.depth.IsCreated && computeShader != null)
			{
				var jobHandleDepthCamBuffer = _depthCamBuffer.Schedule(_depthCamBuffer.Length(), BatchSize);
				jobHandleDepthCamBuffer.Complete();

				var computeBufferSrc = new ComputeBuffer(_depthCamBuffer.depth.Length, sizeof(float));
				computeShader.SetBuffer(_kernelIndex, "_Input", computeBufferSrc);
				computeBufferSrc.SetData(_depthCamBuffer.depth);

				var computeBufferDst = new ComputeBuffer(_computedBufferOutput.Length, sizeof(byte));
				computeShader.SetBuffer(_kernelIndex, "_Output", computeBufferDst);
				computeShader.Dispatch(_kernelIndex, _threadGroupX, _threadGroupY, 1);
				computeBufferDst.GetData(_computedBufferOutput);

				computeBufferSrc.Release();
				computeBufferDst.Release();

				Parallel.For(0, _computedBufferOutputUnitLength, (int i) =>
				{
					for (int j = 0; j < _imageDepth; j++)
						imageStamped.Image.Data[i * _imageDepth + j] = _computedBufferOutput[i * OutputUnitSize + j];
				});

				// Debug.LogFormat("{0:X} {1:X} {2:X} {3:X} => {4}, {5}, {6}, {7}", image.Data[0], image.Data[1], image.Data[2], image.Data[3], System.BitConverter.ToInt16(image.Data, 0), System.BitConverter.ToUInt16(image.Data, 2), System.BitConverter.ToInt32(image.Data, 0), System.BitConverter.ToSingle(image.Data, 0));

				if (camParameter.save_enabled && _startCameraWork)
				{
					var saveName = name + "_" + Time.time;
					SaveRawImageData(camParameter.save_path, saveName);
				}
			}
			_depthCamBuffer.Deallocate();
		}

		private void SaveRawImageData(in string path, in string name)
		{
			_textureForCapture.SetPixelData(imageStamped.Image.Data, 0);
			_textureForCapture.Apply();
			var bytes = _textureForCapture.EncodeToJPG();
			var fileName = string.Format("{0}/{1}.jpg", path, name);
			System.IO.File.WriteAllBytes(fileName, bytes);
			// Debug.LogFormat("{0}|{1} captured", camParameter.save_path, saveName);
		}
	}
}