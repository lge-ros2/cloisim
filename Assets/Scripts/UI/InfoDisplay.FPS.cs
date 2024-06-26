/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class InfoDisplay : MonoBehaviour
{
	private const float fpsUpdatePeriod = 1f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

	private void CalculateFPS()
	{
		frameCount++;
		dT += Time.deltaTime;
		if (dT > fpsUpdatePeriod)
		{
			fps = Mathf.Round(frameCount / dT);
			dT -= fpsUpdatePeriod;
			frameCount = 0;
		}
	}

	private void UpdateFPS()
	{
		if (_inputFieldFPS != null)
		{
			_inputFieldFPS.text = fps.ToString();
		}
	}
}