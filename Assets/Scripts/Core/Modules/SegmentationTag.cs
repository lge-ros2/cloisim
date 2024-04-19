/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System;

public class SegmentationTag : MonoBehaviour
{
	[SerializeField]
	private bool _hide = false;

	[SerializeField]
	private string _className = string.Empty;

	[SerializeField]
	private UInt16 _classId = 0;

	[SerializeField]
	private string _tagName = string.Empty;

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

	public bool Hide
	{
		get => _hide;
		set {
			_hide = value;
			HideLabelForMaterialPropertyBlock(_hide);
		}
	}

	void OnDestroy() {
		// Debug.Log($"Destroy segmentation tag {this.name}");
		Main.SegmentationManager.RemoveClass(_className, this);
	}

	public void Refresh()
	{
		var mpb = new MaterialPropertyBlock();

		// Debug.Log(Main.SegmentationManager.Mode);
		var color = new Color();
		switch (Main.SegmentationManager.Mode)
		{
			case SegmentationManager.ReplacementMode.ObjectId:
				color = ColorEncoding.EncodeIDAsColor(TagId);
				break;

			case SegmentationManager.ReplacementMode.ObjectName:
				color = ColorEncoding.EncodeNameAsColor(TagName);
				break;

			case SegmentationManager.ReplacementMode.LayerId:
				color = ColorEncoding.EncodeLayerAsColor(TagId);
				break;

			default:
				color = Color.black;
				break;
		}

		var grayscale = color.grayscale;

		_classId = (UInt16)(grayscale * UInt16.MaxValue);

		var classValue = ColorEncoding.Encode16BitsToGR(_classId);

		mpb.SetColor("_SegmentationColor", color);
		mpb.SetColor("_SegmentationClassId", classValue);

		// Debug.Log(TagName + ": mode=" + Main.SegmentationManager.Mode +
		// 			" color=" + color +
		// 			" calssId=" + classValue.r + " "  + classValue.g);
		// Debug.Log($"{TagName} : {grayscale} > {_classId}");

		AllocateMaterialPropertyBlock(mpb);

		UpdateClass();
	}

	private void UpdateClass()
	{
		switch (Main.SegmentationManager.Mode)
		{
			case SegmentationManager.ReplacementMode.ObjectId:
				_className = TagId.ToString();
				break;

			case SegmentationManager.ReplacementMode.ObjectName:
				_className = TagName;
				break;

			case SegmentationManager.ReplacementMode.LayerId:
				_className = TagLayer.ToString();
				break;

			default:
				return;
		}

		Main.SegmentationManager.AddClass(_className, this);
	}

	private void AllocateMaterialPropertyBlock(in MaterialPropertyBlock mpb)
	{
		var renderers = GetComponentsInChildren<Renderer>();
		// Debug.Log($"{this.name} {renderers.Length}");
		foreach (var renderer in renderers)
		{
			renderer.SetPropertyBlock(mpb);
		}

		var terrains = GetComponentsInChildren<Terrain>();
		foreach (var terrain in terrains)
		{
			terrain.SetSplatMaterialPropertyBlock(mpb);
		}
	}

	/// <summary>
	/// Hides the label in material property block.
	/// </summary>
	/// <param name="value">if set to <c>true</c>, hide this segmentation.</param>
	private void HideLabelForMaterialPropertyBlock(in bool value)
	{
		var mpb = new MaterialPropertyBlock();
		var renderers = GetComponentsInChildren<Renderer>();
		foreach (var renderer in renderers)
		{
			renderer.GetPropertyBlock(mpb);
			mpb.SetInt("_Hide", value? 1 : 0);
			renderer.SetPropertyBlock(mpb);
		}
	}
}