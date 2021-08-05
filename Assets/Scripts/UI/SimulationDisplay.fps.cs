/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Text;
using UnityEngine;

public partial class SimulationDisplay : MonoBehaviour
{
	private StringBuilder _fpsString = new StringBuilder(9);

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
		if (_fpsString != null)
		{
			_fpsString.Clear();
			_fpsString.Append("FPS [");
			_fpsString.Append(Mathf.Round(fps).ToString());
			_fpsString.Append("]");
		}
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
		rectFps.y = Screen.height - textHeight - bottomMargin;
		style.fontStyle = FontStyle.Normal;
		style.normal.textColor = new Color(0.05f, 0.05f, 0.9f, 1);
		if (_fpsString != null)
		{
			DrawLabelWithShadow(rectFps, _fpsString.ToString());
		}
	}
}