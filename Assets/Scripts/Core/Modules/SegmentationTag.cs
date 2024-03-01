/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;

public class SegmentationTag : MonoBehaviour
{
	private string _tagName = string.Empty;

	private UInt16 _classId = 0;

	public string TagName
	{
		set => _tagName = value;
		get => (string.IsNullOrEmpty(_tagName) ? this.name : _tagName);
	}

	public int TagId
	{
		get => this.gameObject.GetInstanceID();
	}

	public int TagLayer
	{
		get => this.gameObject.layer;
	}

	public UInt16 ClassId
	{
		get => _classId;
	}

	public void Refresh()
	{
		var mpb = new MaterialPropertyBlock();
		// mpb.SetColor("_SegmentationIdColor", ColorEncoding.EncodeIDAsColor(TagId));
		// mpb.SetColor("_SegmentationNameColor", ColorEncoding.EncodeNameAsColor(TagName));
		// mpb.SetColor("_SegmentationLayerColor", ColorEncoding.EncodeLayerAsColor(TagLayer));

		// Debug.Log(Main.SegmentationManager.Mode);
		var color = new Color();
		switch (Main.SegmentationManager.Mode)
		{
			case SegmentationManager.ReplacementMode.ObjectId:
				color = ColorEncoding.EncodeIDAsColor(TagId);
				// grayscale = mpb.GetColor("_SegmentationIdColor").grayscale;
				// gray = color.grayscale;
				break;

			case SegmentationManager.ReplacementMode.ObjectName:
				color = ColorEncoding.EncodeNameAsColor(TagName);
				// grayscale = mpb.GetColor("_SegmentationNameColor").grayscale;
				break;

			case SegmentationManager.ReplacementMode.LayerId:
				color = ColorEncoding.EncodeLayerAsColor(TagId);
				// grayscale = mpb.GetColor("_SegmentationLayerColor").grayscale;
				break;

			default:
				color = Color.black;
				// grayscale = 0F;
				break;
		}

		var grayscale = color.grayscale;

		_classId = (UInt16)(grayscale * UInt16.MaxValue);

		mpb.SetColor("_SegmentationColor", color);
		mpb.SetColor("_SegmentationClassId", ColorEncoding.Encode16BitsToGR(_classId));

		var calssIdColor = mpb.GetColor("_SegmentationClassId");

		// Debug.Log(TagName + ": mode=" + Main.SegmentationManager.Mode +
		// 			" color=" + color +
		// 			" calssId=" + calssIdColor.r + " "  + calssIdColor.g);
		// Debug.Log($"{TagName} : {grayscale} > {_classId}");

		var renderers = GetComponentsInChildren<Renderer>();
		foreach (var renderer in renderers)
		{
			renderer.SetPropertyBlock(mpb);
		}

		UpdateClass();
	}

	private void UpdateClass()
	{
		switch (Main.SegmentationManager.Mode)
		{
			case SegmentationManager.ReplacementMode.ObjectId:
				Main.SegmentationManager.AddClass(TagId.ToString(), _classId);
				break;

			case SegmentationManager.ReplacementMode.ObjectName:
				Main.SegmentationManager.AddClass(TagName, _classId);
				break;

			case SegmentationManager.ReplacementMode.LayerId:
				Main.SegmentationManager.AddClass(TagLayer.ToString(), _classId);
				break;

			default:
				break;
		}
	}
}