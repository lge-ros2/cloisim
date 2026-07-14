/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace SensorDevices
{
	/// <summary>
	/// Encodes camera depth into color via each active DepthCamera's Sensor/DepthRange
	/// material, as a pass inside URP's Render Graph.
	///
	/// This exists because reading _CameraDepthTexture from an external CommandBuffer
	/// (e.g. from RenderPipelineManager.endCameraRendering, after the graph has already
	/// executed) is unreliable under Unity 6's Render Graph renderer: if nothing inside
	/// the graph itself consumes the depth texture resource, the graph culls the pass
	/// that publishes it, leaving the global bound to Unity's black dummy texture. By
	/// declaring a read dependency on the camera depth texture from within the graph,
	/// this pass keeps that dependency alive and guarantees the global is valid when the
	/// material's shader samples it.
	///
	/// Must be added to the Renderer asset(s) used by any camera that hosts a
	/// DepthCamera component (see Assets/Resources/RenderPipelines/*_Renderer.asset).
	/// </summary>
	public class DepthRangeRendererFeature : ScriptableRendererFeature
	{
		private class DepthRangePass : ScriptableRenderPass
		{
			private class PassData
			{
				public Material material;
			}

			public Material Material;

			public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
			{
				if (Material == null)
				{
					return;
				}

				var resourceData = frameData.Get<UniversalResourceData>();
				if (!resourceData.cameraDepthTexture.IsValid() || !resourceData.activeColorTexture.IsValid())
				{
					return;
				}

				using var builder = renderGraph.AddRasterRenderPass<PassData>("DepthCamera.DepthRange", out var passData);

				passData.material = Material;

				builder.UseTexture(resourceData.cameraDepthTexture, AccessFlags.Read);
				builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);
				builder.AllowPassCulling(false);

				builder.SetRenderFunc((PassData data, RasterGraphContext ctx) =>
				{
					Blitter.BlitTexture(ctx.cmd, Vector2.one, data.material, 0);
				});
			}
		}

		// Single instance shared by every DepthCamera using this Renderer asset. Safe only
		// because SensorRenderManager drives all camera rendering through synchronous,
		// strictly sequential Camera.Render() calls (see Camera.cs) — each camera's
		// AddRenderPasses (write) -> RecordRenderGraph (capture) -> Execute (consume) runs
		// to completion before the next camera's Render() starts, so there is no window for
		// one camera's Material write to be overwritten before it's consumed. If camera
		// rendering ever becomes async/batched (e.g. Camera.RenderRequest), this must become
		// a per-camera pass (e.g. keyed by camera instance ID) instead of one shared field.
		private DepthRangePass _pass;

		public override void Create()
		{
			_pass = new DepthRangePass
			{
				renderPassEvent = RenderPassEvent.AfterRenderingTransparents
			};
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
			var depthCamera = renderingData.cameraData.camera.GetComponent<DepthCamera>();
			if (depthCamera == null || depthCamera.DepthRangeMaterial == null)
			{
				return;
			}

			_pass.Material = depthCamera.DepthRangeMaterial;
			renderer.EnqueuePass(_pass);
		}
	}
}
