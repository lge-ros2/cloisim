/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.Text;
using TMPro;

[DefaultExecutionOrder(50)]
public partial class InfoDisplay : MonoBehaviour
{
	private StringBuilder _pointInfo = new StringBuilder(30);
	private Clock _clock = null;
	private TMP_InputField _inputFieldSim = null;
	private TMP_InputField _inputFieldReal = null;
	private TMP_InputField _inputFieldDiff = null;

	private TMP_InputField _inputFieldFPS = null;

	private TMP_InputField _inputFieldHitPoint = null;

	void Awake()
	{
		_clock = DeviceHelper.GetGlobalClock();

		foreach (var inputField in GetComponentsInChildren<TMP_InputField>())
		{
			if (inputField.name.CompareTo("FPS") == 0)
			{
				_inputFieldFPS = inputField;
				_inputFieldFPS.enabled = false;
			}
			else if (inputField.name.CompareTo("SimTime") == 0)
			{
				_inputFieldSim = inputField;
			}
			else if (inputField.name.CompareTo("RealTime") == 0)
			{
				_inputFieldReal = inputField;
			}
			else if (inputField.name.CompareTo("DiffTime") == 0)
			{
				_inputFieldDiff = inputField;
			}
			else if (inputField.name.CompareTo("HitPoint") == 0)
			{
				_inputFieldHitPoint = inputField;
			}
		}

		_pointInfo.Append("000.0000, 000.0000, 000.0000");
	}

	void LateUpdate()
	{
		UpdateFPS();
		UpdateTime();
		UpdateHitPoint();
	}

	void UpdateTime()
	{
		var currentSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().SimTime.ToString();
		var currentRealTime = (_clock == null) ? string.Empty : _clock.ToHMS().RealTime.ToString();
		var diffRealSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().DiffTime.ToString();

		if (_inputFieldSim != null)
		{
			_inputFieldSim.text = currentSimTime;
		}

		if (_inputFieldReal != null)
		{
			_inputFieldReal.text = currentRealTime;
		}

		if (_inputFieldDiff != null)
		{
			_inputFieldDiff.text = diffRealSimTime;
		}
	}

	public void SetPointInfo(in Vector3 point)
	{
		_pointInfo.Clear();
		_pointInfo.Append(System.Math.Truncate(point.x * 10000)/10000);
		_pointInfo.Append(", ");
		_pointInfo.Append(System.Math.Truncate(point.y * 10000)/10000);
		_pointInfo.Append(", ");
		_pointInfo.Append(System.Math.Truncate(point.z * 10000)/10000);
	}

	private void UpdateHitPoint()
	{
		if (_inputFieldHitPoint != null)
		{
			_inputFieldHitPoint.text = _pointInfo.ToString();
		}
	}
}