/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#if UNITY_EDITOR
using UnityEngine;

public class MotorPIDTuner : MonoBehaviour
{
	[Header("PID Gains")]
	[SerializeField] private float _pGain;
	[SerializeField] private float _iGain;
	[SerializeField] private float _dGain;

	[Header("Integral Limits")]
	[SerializeField] private float _integralMin;
	[SerializeField] private float _integralMax;

	[Header("Output Limits")]
	[SerializeField] private float _outputMin;
	[SerializeField] private float _outputMax;

	private PID _pid;

	public void Initialize(
		PID pid,
		in float pGain, in float iGain, in float dGain,
		in float integralMin, in float integralMax,
		in float outputMin, in float outputMax)
	{
		_pid = pid;
		_pGain = pGain;
		_iGain = iGain;
		_dGain = dGain;
		_integralMin = integralMin;
		_integralMax = integralMax;
		_outputMin = outputMin;
		_outputMax = outputMax;
	}

	private void OnValidate()
	{
		if (_pid != null)
		{
			_pid.Change(_pGain, _iGain, _dGain);
			_pid.SetIntegralRange(_integralMin, _integralMax);
			_pid.SetOutputRange(_outputMin, _outputMax);
		}
	}
}
#endif
