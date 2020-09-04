/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Rendering;

public class VisualPlugin : MonoBehaviour
{
	public bool isCastingShadow = true;

	private void SetShadowMode()
	{
		var receiveShadows = isCastingShadow;
		var shadowCastingMode = (isCastingShadow) ? ShadowCastingMode.On : ShadowCastingMode.Off;

		foreach (var renderer in GetComponentsInChildren<Renderer>())
		{
			renderer.shadowCastingMode = shadowCastingMode;
			renderer.receiveShadows = receiveShadows;
		}
	}

	void Awake()
	{
		tag = "Visual";
	}

	// Start is called before the first frame update
	void Start()
	{
		SetShadowMode();
	}
}
