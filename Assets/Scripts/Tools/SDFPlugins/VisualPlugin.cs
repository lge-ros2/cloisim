/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VisualPlugin : MonoBehaviour
{
	public bool isCastingShadow = true;

	private void SetShadowMode()
	{
		var receiveShadows = isCastingShadow;
		var shadowCastingMode = (isCastingShadow) ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

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
