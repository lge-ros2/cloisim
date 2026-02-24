/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SensorDevices
{
	/// <summary>
	/// HDRP Custom Pass that captures _CameraDepthTexture during the render pipeline.
	/// In Unity 6000.x Render Graph, _CameraDepthTexture is only valid during the
	/// pipeline execution — it becomes stale after Camera.Render() returns.
	/// This pass runs at AfterOpaqueDepthAndNormal, reads _CameraDepthTexture via
	/// the DepthRange shader (which linearizes depth), and writes the result to a
	/// dedicated RenderTexture that persists after the pipeline completes.
	/// </summary>
	public class DepthCapturePass : CustomPass
	{
		private Material _depthMaterial;
		private RenderTexture _capturedDepthRT;

		/// <summary>
		/// The captured linearized depth. Valid after Camera.Render() returns.
		/// </summary>
		public RenderTexture capturedDepthRT => _capturedDepthRT;

		public void SetDepthMaterial(Material mat)
		{
			_depthMaterial = mat;
		}

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			// RT is allocated lazily in Execute when camera resolution is known
		}

		protected override void Execute(CustomPassContext ctx)
		{
			if (_depthMaterial == null) return;

			var cam = ctx.hdCamera.camera;
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

			// Render fullscreen depth to our dedicated RT.
			// The shader uses LoadCameraDepth() which correctly handles
			// HDRP's Tex2DArray depth buffer via LOAD_TEXTURE2D_X.
			CoreUtils.SetRenderTarget(ctx.cmd, _capturedDepthRT);
			CoreUtils.DrawFullScreen(ctx.cmd, _depthMaterial, shaderPassId: 0);
		}

		protected override void Cleanup()
		{
			if (_capturedDepthRT != null)
			{
				_capturedDepthRT.Release();
				_capturedDepthRT = null;
			}
		}
	}
}
