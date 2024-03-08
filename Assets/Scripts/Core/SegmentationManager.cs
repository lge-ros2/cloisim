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

	[SerializeField]
	private static readonly bool _disableColor = true;

	private static Material _material = null; // linked to Render Objects

	[SerializeField]
	private const int MAX_LABEL_INFO = 256;

	private Dictionary<string, List<SegmentationTag>> _labelInfo = new Dictionary<string, List<SegmentationTag>>();
	private List<string> _labelClassFilters = new List<string>();

	public static void AttachTag(in string className, GameObject target)
	{
		AttachTag(className, target?.transform);
	}

	public static void AttachTag(in string className, Transform target)
	{
		var segmentationTag = target?.gameObject.GetComponentInChildren<SegmentationTag>();
		if (segmentationTag == null)
		{
			segmentationTag = target?.gameObject.AddComponent<SegmentationTag>();
		}

		segmentationTag.TagName = className;
		segmentationTag.Refresh();
	}

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

	public void SetClassFilter(in List<string> items)
	{
		_labelClassFilters.Clear();
		_labelClassFilters.AddRange(items);

		// foreach (var item in items)
		// {
		// 	Debug.Log(item);
		// }
	}

	public void AddClass(in string className, in SegmentationTag tag)
	{
		if (_labelInfo.Count > MAX_LABEL_INFO)
		{
			Debug.LogWarning(
				$"Cannot add className({className}) due to maximum count({MAX_LABEL_INFO}) reached");
			return;
		}

		if (_labelInfo.ContainsKey(className))
		{
			_labelInfo[className].Add(tag);
		}
		else
		{
			_labelInfo.TryAdd(className, new List<SegmentationTag> { tag });
		}
		// Debug.Log($"AddClass: {className}, {_labelInfo[className]}");
	}

	public void RemoveClass(in string className, in SegmentationTag tag)
	{
		if (_labelInfo.ContainsKey(className))
		{
			_labelInfo[className].Remove(tag);
		}
	}

	public void UpdateTags()
	{
		foreach (var vk in _labelInfo)
		{
			var allowedTag = _labelClassFilters.Contains(vk.Key);
			foreach (var tag in vk.Value)
			{
				tag.Hide = allowedTag ? false : true;
			}
			// Debug.Log(vk.Key + ", " + allowedTag);
		}
	}

	public Dictionary<string, List<SegmentationTag>> GetLabelInfo()
	{
		// foreach (var item in _labelInfo)
		// {
		// 	if (item.Value.Count > 0)
		// 	{
		// 		Debug.Log($"{item.Key}, {item.Value[0].ClassId}");
		// 	}
		// }
		return _labelInfo;
	}
}