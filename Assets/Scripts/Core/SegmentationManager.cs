/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using System.Collections.Generic;

public class SegmentationManager : MonoBehaviour
{
	public enum ReplacementMode
	{
		ObjectId = 0,
		ObjectName = 1,
		LayerId = 2
	};

	public ReplacementMode Mode => _ReplaceMode;

	private static readonly ReplacementMode _ReplaceMode = ReplacementMode.ObjectName;
	private static readonly bool _disableColor = true;
	private static Material _material = null;

	private const int MAX_LABEL_INFO = 256;

	private Dictionary<string, UInt16> _labelInfo = new Dictionary<string, UInt16>();

	private List<SegmentationTag> _tagList = new List<SegmentationTag>();


	void OnEnable()
	{
		if (_material == null)
		{
			_material = Resources.Load<Material>("Materials/Segmentation");
		}

		if (_material != null)
		{
			_material.SetInt("_DisableColor", _disableColor ? 1 : 0);
		}
	}

	void OnDisable()
	{
		if (_material != null)
		{
			_material.SetInt("_DisableColor", 0);
		}
	}

	public void AddClass(in string className, in UInt16 value)
	{
		if (_labelInfo.Count > MAX_LABEL_INFO)
		{
			Debug.LogWarning(
				$"Cannot add className({className}) due to maximum count({MAX_LABEL_INFO}) reached");
			return;
		}
		_labelInfo.TryAdd(className, value);
		// Debug.Log($"AddClass: {className}, {_labelInfo[className]}");
	}

	public void GetLabelInfo()
	{
		foreach (var item in _labelInfo)
		{
			Debug.Log($"{item.Key}, {item.Value}");
		}
	}

	public static void AttachTag(in string className, GameObject target)
	{
		AttachTag(className, target.transform);
	}

	public static void AttachTag(in string className, Transform target)
	{
		var segmentationTag = target.gameObject.AddComponent<SegmentationTag>();
		segmentationTag.TagName = className;
		segmentationTag.Refresh();
	}
}