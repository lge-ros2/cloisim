/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class InfoDisplay : MonoBehaviour
{
	private const float _fpsUpdatePeriod = 1f;
	private const float _smoothedFpsFactor = 0.1f;
	private int _frameCount = 0;
	private float _elapsedTime = 0.0F;
	private float _uiFps = 0.0F;
	private float _serviceFps = 0.0F;
	private float _smoothedInstantFps = 0.0F;

	public float FPS()
	{
		return _serviceFps;
	}

	public float SmoothedFPS()
	{
		return _uiFps;
	}

	private void CalculateFPS()
	{
		var unscaledDeltaTime = Time.unscaledDeltaTime;
		if (unscaledDeltaTime <= 0f)
		{
			return;
		}

		var instantFps = 1f / unscaledDeltaTime;
		if (_smoothedInstantFps <= 0f)
		{
			_smoothedInstantFps = instantFps;
		}
		else
		{
			_smoothedInstantFps = Mathf.Lerp(_smoothedInstantFps, instantFps, _smoothedFpsFactor);
		}

		_uiFps = Mathf.Round(_smoothedInstantFps);
		_frameCount++;
		_elapsedTime += unscaledDeltaTime;
		if (_elapsedTime >= _fpsUpdatePeriod)
		{
			_serviceFps = Mathf.Round(_frameCount / _elapsedTime);
			_elapsedTime = 0f;
			_frameCount = 0;
		}
	}

	private void UpdateFPS()
	{
		if (_inputFieldFPS != null)
		{
			_inputFieldFPS.text = _uiFps.ToString();
		}
	}
}