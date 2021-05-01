	/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;

public partial class SimulationDisplay : MonoBehaviour
{
	private string _fpsString = string.Empty;

	[Header("fps")]
	private const float fpsUpdatePeriod = 0.5f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

	void Update()
	{
		CalculateFPS();
	}

	void LateUpdate()
	{
		_fpsString = "FPS [" + GetBoldText(Mathf.Round(fps).ToString("F1")) + "]";
	}

	private void CalculateFPS()
	{
		frameCount++;
		dT += Time.unscaledDeltaTime;
		if (dT > fpsUpdatePeriod)
		{
			fps = frameCount / dT;
			dT -= fpsUpdatePeriod;
			frameCount = 0;
		}
	}

	private void DrawFPSText()
	{
		_rectFPS.y = Screen.height - textHeight - topMargin;
		DrawShadow(_rectFPS, _fpsString);
		GUI.skin.label.normal.textColor = Color.blue;
		GUI.Label(_rectFPS, _fpsString);
	}
}