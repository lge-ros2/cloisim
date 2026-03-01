/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SensorDevices
{
	/// <summary>
	/// URP ScriptableRenderPass that captures linearized depth via the DepthRange shader.
	/// Replaces the former HDRP CustomPass. Uses Compatibility Mode Execute() path
	/// (m_EnableRenderGraph: 0) which is supported in Unity 6 URP 17.x.
	///
	/// The pass runs AfterRenderingOpaques, reads _CameraDepthTexture via the
	/// DepthRange shader (which linearizes depth), and writes to a dedicated
	/// RenderTexture that persists after Camera.Render() returns.
	/// </summary>
	public class DepthCapturePass : ScriptableRenderPass
	{
		private Material _depthMaterial;
		private RenderTexture _capturedDepthRT;
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

#pragma warning disable CS0618, CS0672 // Compatibility Mode: Execute is obsolete but required when RenderGraph is disabled
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (_depthMaterial == null) return;

			var cam = renderingData.cameraData.camera;

			// Only capture depth for the intended sensor camera.
			if (_targetCamera != null && cam != _targetCamera) return;

			var w = cam.pixelWidth;
			var h = cam.pixelHeight;

			// Lazy-allocate (or resize) the capture RT
			if (_capturedDepthRT == null || _capturedDepthRT.width != w || _capturedDepthRT.height != h)
			{
				if (_capturedDepthRT != null) _capturedDepthRT.Release();
				_capturedDepthRT = new RenderTexture(w, h, 0, GraphicsFormat.R32_SFloat)
				{
					name = "CapturedDepth",
					filterMode = FilterMode.Point,
				};
				_capturedDepthRT.Create();
			}

			var cmd = CommandBufferPool.Get("DepthCapturePass");

			// Blit camera depth through the DepthRange material to our dedicated RT.
			cmd.Blit(cam.targetTexture, _capturedDepthRT, _depthMaterial);

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
#pragma warning restore CS0618

		public void Cleanup()
		{
			if (_capturedDepthRT != null)
			{
				_capturedDepthRT.Release();
				_capturedDepthRT = null;
			}
		}
	}

	/// <summary>
	/// URP ScriptableRendererFeature that injects the DepthCapturePass into the render pipeline.
	/// Add this feature to the URP Renderer asset to enable depth capture for LivoxLidar.
	/// </summary>
	public class DepthCaptureFeature : ScriptableRendererFeature
	{
		private DepthCapturePass _pass;

		public DepthCapturePass Pass => _pass;

		public override void Create()
		{
			_pass = new DepthCapturePass();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			if (_pass != null)
			{
				renderer.EnqueuePass(_pass);
			}
		}

		protected override void Dispose(bool disposing)
		{
			_pass?.Cleanup();
		}
	}
}
