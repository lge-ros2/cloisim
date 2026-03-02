/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

namespace SensorDevices
{
	/// <summary>
	/// HDRP Custom Pass that renders all objects with a segmentation override material
	/// to a DEDICATED RenderTexture (not ctx.cameraColorBuffer).
	///
	/// This follows the same pattern as DepthCapturePass: a separate RT that persists
	/// after Camera.Render() returns, allowing SegmentationCamera to do readback
	/// from this RT instead of the camera's target texture.
	///
	/// The dedicated RT has its own depth attachment (D24) so depth testing works
	/// correctly for occlusion without depending on HDRP's internal depth buffer.
	///
	/// Each object's MaterialPropertyBlock (_SegmentationValue, _Hide) set by
	/// SegmentationTag is preserved — only the shader is replaced, producing
	/// discrete class ID values without lighting, shadows, or any gradation.
	/// </summary>
	public class SegmentationPass : CustomPass
	{
		private Material _segMaterial;
		private LayerMask _layerMask;
		private RenderTexture _segmentationRT;
		private MaterialPropertyBlock _tmpMpb = new MaterialPropertyBlock();
		private UnityEngine.Camera _targetCamera;

		/// <summary>
		/// The captured segmentation output. Valid after Camera.Render() returns.
		/// </summary>
		public RenderTexture segmentationRT => _segmentationRT;

		public void SetSegmentationMaterial(Material mat)
		{
			_segMaterial = mat;
		}

		public void SetLayerMask(LayerMask mask)
		{
			_layerMask = mask;
		}

		/// <summary>
		/// Restrict this pass to a specific camera to prevent the GUI camera
		/// from overwriting segmentationRT with unrelated data.
		/// </summary>
		public void SetTargetCamera(UnityEngine.Camera cam)
		{
			_targetCamera = cam;
		}

		/// <summary>
		/// Ensures our layer mask is included in the camera's culling.
		/// Without this, objects on our target layers may be culled before
		/// they reach Execute. Matches HDRP's DrawRenderersCustomPass pattern.
		/// </summary>
		protected override void AggregateCullingParameters(ref ScriptableCullingParameters cullingParameters, HDCamera hdCamera)
		{
			cullingParameters.cullingMask |= (uint)(int)_layerMask;
		}

		protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
		{
			// RT is allocated lazily in Execute when camera resolution is known
		}

		protected override void Execute(CustomPassContext ctx)
		{
			if (_segMaterial == null) return;

			var cam = ctx.hdCamera.camera;

			if (_targetCamera != null && cam != _targetCamera) return;
			var w = cam.pixelWidth;
			var h = cam.pixelHeight;

			if (_segmentationRT == null || _segmentationRT.width != w || _segmentationRT.height != h)
			{
				if (_segmentationRT != null) _segmentationRT.Release();
				_segmentationRT = new RenderTexture(w, h, 24, GraphicsFormat.R8G8B8A8_UNorm)
				{
					name = "SegmentationRT",
					filterMode = FilterMode.Point,
				};
				_segmentationRT.Create();
			}

			// Clear dedicated RT — explicit depth clear to 1.0 for ZTest LEqual
			CoreUtils.SetRenderTarget(ctx.cmd, _segmentationRT);
			ctx.cmd.ClearRenderTarget(true, true, Color.black, 1.0f);

			// Set custom VP matrix for the segmentation shader
			var gpuProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
			var vpMatrix = gpuProj * cam.worldToCameraMatrix;
			ctx.cmd.SetGlobalMatrix("_SegViewProjMatrix", vpMatrix);

			// Use cmd.DrawRenderer per object — renderer list APIs
			// (CustomPassUtils.DrawRenderers, SRC.DrawRenderers)
			// don't produce output on dedicated RTs in HDRP render graph mode.
			if (_renderers == null || Time.frameCount % 60 == 0)
			{
				_renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
			}

			foreach (var r in _renderers)
			{
				if (r != null && r.enabled && r.gameObject.activeInHierarchy
					&& ((1 << r.gameObject.layer) & (int)_layerMask) != 0)
				{
					for (int sub = 0; sub < r.sharedMaterials.Length; sub++)
					{
						r.GetPropertyBlock(_tmpMpb, sub);
						var segVal = _tmpMpb.GetInt("_SegmentationValue");
						var hide = _tmpMpb.GetInt("_Hide");
						// Skip hidden objects to save draw calls
						if (hide != 0 || segVal == 0) continue;
						// Pass class ID via global vector (cmd.DrawRenderer with
						// override material does NOT preserve per-renderer MPBs).
						// Using _SegParams name to avoid collision with Properties block.
						ctx.cmd.SetGlobalVector("_SegParams", new Vector4(segVal, 0, 0, 0));
						ctx.cmd.DrawRenderer(r, _segMaterial, sub, 0);
					}
				}
			}
		}

		private Renderer[] _renderers;

		protected override void Cleanup()
		{
			if (_segmentationRT != null)
			{
				_segmentationRT.Release();
				_segmentationRT = null;
			}
		}
	}
}
