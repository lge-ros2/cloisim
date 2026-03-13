/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using UnityEditor;
using UnityEngine;
using TMPro;

/// <summary>
/// Editor-mode workaround for Unity 6 (com.unity.ugui 2.0.0) bug where
/// TextMeshProUGUI.Cull() throws NullReferenceException because its private
/// 'm_canvasRenderer' field is null while still registered with a RectMask2D.
///
/// This runs on editor update and after domain reload to ensure the field is
/// populated, preventing the error from spamming in the Editor console.
/// </summary>
[InitializeOnLoad]
public static class TMPCanvasRendererFixEditor
{
	private static readonly FieldInfo _canvasRendererField =
		typeof(TextMeshProUGUI).GetField(
			"m_canvasRenderer",
			BindingFlags.NonPublic | BindingFlags.Instance);

	private static bool _applied = false;

	static TMPCanvasRendererFixEditor()
	{
		if (_canvasRendererField == null)
			return;

		EditorApplication.delayCall += ApplyFix;
		EditorApplication.hierarchyChanged += OnHierarchyChanged;
	}

	private static void OnHierarchyChanged()
	{
		_applied = false;
		EditorApplication.delayCall += ApplyFix;
	}

	private static void ApplyFix()
	{
		if (_applied || EditorApplication.isPlayingOrWillChangePlaymode)
			return;

		if (_canvasRendererField == null)
			return;

		var tmpComponents = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();

		foreach (var tmp in tmpComponents)
		{
			if (tmp == null)
				continue;

			var currentValue = _canvasRendererField.GetValue(tmp) as CanvasRenderer;

			if (currentValue == null)
			{
				var cr = tmp.GetComponent<CanvasRenderer>();
				if (cr == null)
				{
					cr = tmp.gameObject.AddComponent<CanvasRenderer>();
				}
				_canvasRendererField.SetValue(tmp, cr);
			}
		}

		_applied = true;
	}
}
