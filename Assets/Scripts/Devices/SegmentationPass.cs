/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace SensorDevices
{
	/// <summary>
	/// URP ScriptableRenderPass that renders all objects with a segmentation override
	/// material to a DEDICATED RenderTexture. Uses Compatibility Mode Execute() path.
	///
	/// Replaces the former HDRP CustomPass. The dedicated RT has its own depth
	/// attachment (D24) so depth testing works correctly for occlusion without
	/// depending on the pipeline's internal depth buffer.
	///
	/// Each object's MaterialPropertyBlock (_SegmentationValue, _Hide) set by
	/// SegmentationTag is preserved — only the shader is replaced, producing
	/// discrete class ID values without lighting, shadows, or any gradation.
	///
	/// NOTE: The primary URP segmentation path uses SegmentationRenderer.asset
	/// with SetRenderer(1) + RenderObjects feature. This pass provides an
	/// alternative for scenarios needing manual per-object control.
	/// </summary>
	public class SegmentationPass : ScriptableRenderPass
	{
		private Material _segMaterial;
		private LayerMask _layerMask;
		private RenderTexture _segmentationRT;
		private MaterialPropertyBlock _tmpMpb = new MaterialPropertyBlock();
		private UnityEngine.Camera _targetCamera;
		private Renderer[] _renderers;

		/// <summary>
		/// The captured segmentation output. Valid after Camera.Render() returns.
		/// </summary>
		public RenderTexture segmentationRT => _segmentationRT;

		public SegmentationPass()
		{
			renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
		}

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

#pragma warning disable CS0618, CS0672 // Compatibility Mode: Execute is obsolete but required when RenderGraph is disabled
		public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
		{
			if (_segMaterial == null) return;

			var cam = renderingData.cameraData.camera;

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

			var cmd = CommandBufferPool.Get("SegmentationPass");

			// Clear dedicated RT — explicit depth clear to 1.0 for ZTest LEqual
			cmd.SetRenderTarget(_segmentationRT);
			cmd.ClearRenderTarget(true, true, Color.black, 1.0f);

			// Set custom VP matrix for the segmentation shader
			var gpuProj = GL.GetGPUProjectionMatrix(cam.projectionMatrix, true);
			var vpMatrix = gpuProj * cam.worldToCameraMatrix;
			cmd.SetGlobalMatrix("_SegViewProjMatrix", vpMatrix);

			// Per-object rendering with override material
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
						// Pass class ID via global vector
						cmd.SetGlobalVector("_SegParams", new Vector4(segVal, 0, 0, 0));
						cmd.DrawRenderer(r, _segMaterial, sub, 0);
					}
				}
			}

			context.ExecuteCommandBuffer(cmd);
			CommandBufferPool.Release(cmd);
		}
#pragma warning restore CS0618

		public void Cleanup()
		{
			if (_segmentationRT != null)
			{
				_segmentationRT.Release();
				_segmentationRT = null;
			}
		}
	}

	/// <summary>
	/// URP ScriptableRendererFeature that injects the SegmentationPass.
	/// The primary URP segmentation path uses the built-in RenderObjects feature
	/// via SegmentationRenderer.asset + SetRenderer(1). This feature is an
	/// alternative for manual per-object segmentation control.
	/// </summary>
	public class SegmentationPassFeature : ScriptableRendererFeature
	{
		private SegmentationPass _pass;

		public SegmentationPass Pass => _pass;

		public override void Create()
		{
			_pass = new SegmentationPass();
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
