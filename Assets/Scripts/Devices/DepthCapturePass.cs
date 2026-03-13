/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace SensorDevices
{
	/// <summary>
	/// URP ScriptableRenderPass that captures linearized depth via the DepthRange shader.
	/// Replaces the former HDRP CustomPass. Uses the Render Graph UnsafePass API
	/// (Unity 6 URP 17.x).
	///
	/// The pass runs AfterRenderingOpaques, reads the camera color texture via the
	/// DepthRange shader (which linearizes depth), and writes to a dedicated
	/// RenderTexture that persists after Camera.Render() returns.
	/// </summary>
	public class DepthCapturePass : ScriptableRenderPass
	{
		private Material _depthMaterial;
		private RenderTexture _capturedDepthRT;
		private RTHandle _capturedDepthRTHandle;
		private UnityEngine.Camera _targetCamera;

		/// <summary>
		/// The captured linearized depth. Valid after Camera.Render() returns.
		/// </summary>
		public RenderTexture capturedDepthRT => _capturedDepthRT;

		public DepthCapturePass()
		{
			renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
		}

		public void SetDepthMaterial(Material mat)
		{
			_depthMaterial = mat;
		}

		/// <summary>
		/// Restrict this pass to a specific camera. Without this guard,
		/// the GUI/scene camera can trigger Execute and overwrite
		/// _capturedDepthRT with its own depth, producing spurious frames.
		/// </summary>
		public void SetTargetCamera(UnityEngine.Camera cam)
		{
			_targetCamera = cam;
		}

		private class PassData
		{
			internal TextureHandle source;
			internal TextureHandle destination;
			internal Material material;
		}

		private void EnsureCaptureRT(int w, int h)
		{
			if (_capturedDepthRT == null || _capturedDepthRT.width != w || _capturedDepthRT.height != h)
			{
				if (_capturedDepthRTHandle != null) _capturedDepthRTHandle.Release();
				if (_capturedDepthRT != null) _capturedDepthRT.Release();

				_capturedDepthRT = new RenderTexture(w, h, 0, GraphicsFormat.R32_SFloat)
				{
					name = "CapturedDepth",
					filterMode = FilterMode.Point,
				};
				_capturedDepthRT.Create();
				_capturedDepthRTHandle = RTHandles.Alloc(_capturedDepthRT);
			}
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
		{
			if (_depthMaterial == null) return;

			var cameraData = frameData.Get<UniversalCameraData>();
			var cam = cameraData.camera;

			if (_targetCamera != null && cam != _targetCamera) return;

			EnsureCaptureRT(cam.pixelWidth, cam.pixelHeight);

			var resourceData = frameData.Get<UniversalResourceData>();

			using (var builder = renderGraph.AddUnsafePass<PassData>("DepthCapturePass", out var passData))
			{
				passData.source = resourceData.activeColorTexture;
				passData.destination = renderGraph.ImportTexture(_capturedDepthRTHandle);
				passData.material = _depthMaterial;

				builder.UseTexture(passData.source);
				builder.UseTexture(passData.destination, AccessFlags.WriteAll);
				builder.AllowPassCulling(false);

				builder.SetRenderFunc(static (PassData data, UnsafeGraphContext context) =>
				{
					var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
					Blitter.BlitCameraTexture(cmd, data.source, data.destination, data.material, 0);
				});
			}
		}

		public void Cleanup()
		{
			if (_capturedDepthRTHandle != null)
			{
				_capturedDepthRTHandle.Release();
				_capturedDepthRTHandle = null;
			}
			if (_capturedDepthRT != null)
			{
				_capturedDepthRT.Release();
				_capturedDepthRT = null;
			}
		}
	}
}
