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

	// Teardown guard: writing TMP_InputField.text marks the field dirty and
	// queues a Canvas geometry rebuild (CanvasUpdateRegistry → FillMesh →
	// Mesh.Clear). If the rebuild runs after teardown has freed the underlying
	// native Mesh, Mesh.Clear dereferences freed memory and SIGSEGVs. Once this
	// component is disabled/destroyed or the app is quitting we stop touching
	// the fields entirely.
	private bool _teardown = false;

	// Last value pushed to each field — only write on change so unchanged
	// frames queue no rebuild at all, shrinking the window for a freed-Mesh
	// rebuild and avoiding wasted per-frame canvas work.
	private string _lastSim, _lastReal, _lastDiff, _lastFps, _lastHit;

	void OnEnable()
	{
		_teardown = false;
	}

	void OnDisable()
	{
		_teardown = true;
	}

	void OnApplicationQuit()
	{
		_teardown = true;
	}

	/// <summary>
	/// Write to a read-only display field only when its value changed and
	/// teardown has not begun. SetTextWithoutNotify avoids onValueChanged churn.
	/// </summary>
	private void SetFieldText(TMP_InputField field, ref string cache, string value)
	{
		if (_teardown || field == null || string.Equals(cache, value))
		{
			return;
		}

		cache = value;
		field.SetTextWithoutNotify(value);
	}

	void Awake()
	{
		_clock = DeviceHelper.GetGlobalClock();

		foreach (var inputField in GetComponentsInChildren<TMP_InputField>())
		{
			if (inputField.name.Equals("FPS"))
			{
				_inputFieldFPS = inputField;
				_inputFieldFPS.readOnly = true;
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
		if (_teardown)
		{
			return;
		}

		UpdateFPS();
		UpdateTime();
		UpdateHitPoint();
	}

	void UpdateTime()
	{
		var currentSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().SimTime;
		var currentRealTime = (_clock == null) ? string.Empty : _clock.ToHMS().RealTime;
		var diffRealSimTime = (_clock == null) ? string.Empty : _clock.ToHMS().DiffTime;

		SetFieldText(_inputFieldSim, ref _lastSim, currentSimTime);
		SetFieldText(_inputFieldReal, ref _lastReal, currentRealTime);
		SetFieldText(_inputFieldDiff, ref _lastDiff, diffRealSimTime);
	}

	private void AddClickEvent(GameObject target, Action callback)
	{
		var trigger = target.GetComponent<EventTrigger>();
		if (trigger == null)
		{
			trigger = target.AddComponent<EventTrigger>();
		}

		var entry = new EventTrigger.Entry
		{
			eventID = EventTriggerType.PointerClick
		};
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

	public void SetPointInfo(in SDFormat.Math.Vector3d point)
	{
		var ptX = Math.Truncate(point.X * 10000)/10000;
		var ptY = Math.Truncate(point.Y * 10000)/10000;
		var ptZ = Math.Truncate(point.Z * 10000)/10000;
		_pointInfo = string.Concat(ptX.ToString(), ", ", ptY.ToString(), ", ", ptZ.ToString());
	}

	private void UpdateHitPoint()
	{
		SetFieldText(_inputFieldHitPoint, ref _lastHit, _pointInfo);
	}
}