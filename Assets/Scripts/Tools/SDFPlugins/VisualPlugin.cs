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
		foreach (var renderer in GetComponentsInChildren<Renderer>())
		{
			bool receiveShadows = false;
			UnityEngine.Rendering.ShadowCastingMode shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

			if (isCastingShadow == true)
			{
				receiveShadows = true;
				shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
			}

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
