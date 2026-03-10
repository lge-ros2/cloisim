/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.EventSystems;
using System;
using TMPro;

[DefaultExecutionOrder(50)]
public partial class InfoDisplay : MonoBehaviour
{
	private string _pointInfo = "-000.0000, -000.0000, -000.0000";
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
			if (inputField.name.Equals("FPS"))
			{
				_inputFieldFPS = inputField;
				_inputFieldFPS.enabled = false;
				_inputFieldSim.readOnly = true;
			}
			else if (inputField.name.Equals("SimTime"))
			{
				_inputFieldSim = inputField;
				_inputFieldSim.readOnly = true;
			}
			else if (inputField.name.Equals("RealTime"))
			{
				_inputFieldReal = inputField;
				_inputFieldReal.readOnly = true;
			}
			else if (inputField.name.Equals("DiffTime"))
			{
				_inputFieldDiff = inputField;
				_inputFieldDiff.enabled = false;
				_inputFieldDiff.readOnly = true;
			}
			else if (inputField.name.Equals("HitPoint"))
			{
				_inputFieldHitPoint = inputField;
				_inputFieldHitPoint.readOnly = true;
			}
		}

		foreach (var tmp in GetComponentsInChildren<TMP_Text>())
		{
			if (tmp.name.Equals("Sim"))
			{
				AddClickEvent(tmp.gameObject, OnTimeLabelClicked);
				break;
			}
		}
	}

	void Update()
	{
		CalculateFPS();
	}

	void LateUpdate()
	{
		UpdateFPS();
		UpdateTime();
		UpdateHitPoint();
	}

	void UpdateTime()
	{
		var currentSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().SimTime;
		var currentRealTime = (_clock == null) ? string.Empty : _clock.ToHMS().RealTime;
		var diffRealSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().DiffTime;

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

	private void AddClickEvent(GameObject target, Action callback)
	{
		var trigger = target.GetComponent<EventTrigger>();
		if (trigger == null)
		{
			trigger = target.AddComponent<EventTrigger>();
		}

		var entry = new EventTrigger.Entry();
		entry.eventID = EventTriggerType.PointerClick;
		entry.callback.AddListener((_) => callback());
		trigger.triggers.Add(entry);
	}

	private void OnTimeLabelClicked()
	{
		if (_clock != null)
		{
			_clock.IsSecondsOnly = !_clock.IsSecondsOnly;
		}
	}

	public void SetPointInfo(in SDF.Vector3<double> point)
	{
		var ptX = System.Math.Truncate(point.X * 10000)/10000;
		var ptY = System.Math.Truncate(point.Y * 10000)/10000;
		var ptZ = System.Math.Truncate(point.Z * 10000)/10000;
		_pointInfo = String.Concat(ptX.ToString(), ", ", ptY.ToString(), ", ", ptZ.ToString());
	}

	private void UpdateHitPoint()
	{
		if (_inputFieldHitPoint != null)
		{
			_inputFieldHitPoint.text = _pointInfo;
		}
	}
}