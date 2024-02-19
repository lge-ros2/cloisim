/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
// using UnityEngine.Rendering.Universal;
// using UnityEngine.Experimental.Rendering;
// using Unity.Collections;
using System.Collections.Generic;

public class SegmentationManager : MonoBehaviour
{
	private enum ReplacementMode
	{
		ObjectId = 0,
		ObjectName = 1,
		LayerId = 2
	};

	private static readonly ReplacementMode ReplaceMode = ReplacementMode.ObjectName;
	private static Material SegmentationMaterial = null;


	void OnEnable()
	{
		if (SegmentationMaterial == null)
		{
			SegmentationMaterial = Resources.Load<Material>("Materials/Segmentation");
		}

		if (SegmentationMaterial != null)
		{
			SegmentationMaterial.SetInt("_OutputMode", (int)ReplaceMode);
		}
	}

	void OnDisable()
	{
		if (SegmentationMaterial != null)
		{
			SegmentationMaterial.SetInt("_OutputMode", (int)-1);
		}
	}



	public void OnSceneChanged()
	{
		var renderersWorld = Main.WorldRoot.GetComponentsInChildren<Renderer>();
		var renderersProps = Main.PropsRoot.GetComponentsInChildren<Renderer>();

		var combineRenderers = new List<Renderer>();
		combineRenderers.AddRange(renderersWorld);
		combineRenderers.AddRange(renderersProps);

		var mpb = new MaterialPropertyBlock();
		foreach (var renderer in combineRenderers.ToArray())
		{
			var go = renderer.gameObject;

			var id = go.GetInstanceID();
			var layer = go.layer;
			var name = go.name;

			mpb.SetColor("_SegmentationIdColor", ColorEncoding.EncodeIDAsColor(id));
			mpb.SetColor("_SegmentationNameColor", ColorEncoding.EncodeNameAsColor(name));
			mpb.SetColor("_SegmentationLayerColor", ColorEncoding.EncodeLayerAsColor(layer));
			// Debug.Log(id + " " + name + " " + mpb.GetColor("_SegmentationIdColor"));
			// Debug.Log(id + " " + name + " " + mpb.GetColor("_SegmentationNameColor"));
			// Debug.Log(id + " " + name + " " + mpb.GetColor("_SegmentationLayerColor"));

			renderer.SetPropertyBlock(mpb);
		}
	}
}