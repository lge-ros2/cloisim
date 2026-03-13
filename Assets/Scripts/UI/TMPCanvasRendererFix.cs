/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using UnityEngine;
using TMPro;

/// <summary>
/// Workaround for a known Unity 6 bug (com.unity.ugui 2.0.0) where
/// TextMeshProUGUI.Cull() throws NullReferenceException because it uses
/// the private field 'm_canvasRenderer' directly instead of the base-class
/// property 'canvasRenderer' which has lazy initialization.
///
/// This script ensures all TextMeshProUGUI components have
/// their internal m_canvasRenderer field properly initialized.
///
/// Attach this to a persistent UI GameObject (e.g. Main Canvas),
/// or it will auto-run via [RuntimeInitializeOnLoadMethod].
/// </summary>
[DefaultExecutionOrder(-100)]
public class TMPCanvasRendererFix : MonoBehaviour
{
	private static readonly FieldInfo _canvasRendererField =
		typeof(TextMeshProUGUI).GetField(
			"m_canvasRenderer",
			BindingFlags.NonPublic | BindingFlags.Instance);

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
	private static void OnAfterSceneLoad()
	{
		FixAllInScene();
	}

	void Awake()
	{
		FixAllInScene();
	}

	void OnEnable()
	{
		FixAllInScene();
	}

	/// <summary>
	/// Finds all TextMeshProUGUI components in the scene (including inactive)
	/// and ensures their internal m_canvasRenderer field is set.
	/// </summary>
	public static void FixAllInScene()
	{
		if (_canvasRendererField == null)
		{
			return;
		}

		var tmpComponents = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();

		foreach (var tmp in tmpComponents)
		{
			EnsureCanvasRenderer(tmp);
		}
	}

	/// <summary>
	/// Ensures a single TextMeshProUGUI has its m_canvasRenderer field set.
	/// Call this after dynamically creating TMP UI objects.
	/// </summary>
	public static void EnsureCanvasRenderer(TextMeshProUGUI tmp)
	{
		if (tmp == null || _canvasRendererField == null)
			return;

		var currentValue = _canvasRendererField.GetValue(tmp) as CanvasRenderer;

		// Unity's overloaded == operator: a destroyed native object is "== null" but not "ReferenceEquals null"
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
}
