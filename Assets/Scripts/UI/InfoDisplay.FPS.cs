/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class InfoDisplay : MonoBehaviour
{
	private const float _fpsUpdatePeriod = 1f;
	private int _frameCount = 0;
	private float _deltaTime = 0.0F;
	private float _fps = 0.0F;

	public float FPS()
	{
		return _fps;
	}

	private void CalculateFPS()
	{
		_frameCount++;
		_deltaTime += Time.deltaTime;
		if (_deltaTime > _fpsUpdatePeriod)
		{
			_fps = Mathf.Round(_frameCount / _deltaTime);
			_deltaTime -= _fpsUpdatePeriod;
			_frameCount = 0;
		}
	}

	private void UpdateFPS()
	{
		if (_inputFieldFPS != null)
		{
			_inputFieldFPS.text = _fps.ToString();
		}
	}
}