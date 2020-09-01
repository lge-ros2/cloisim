/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;
using TMPro;

public class SimulationDisplay : MonoBehaviour
{
	private StringBuilder sbToPrint = new StringBuilder();
	private TextMeshProUGUI uiText = null;
	private Clock clock = null;

	private string eventMessage = string.Empty;

	private const float fpsUpdatePeriod = 0.5f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

	// Start is called before the first frame update
	void Awake()
	{
		uiText = GetComponent<TextMeshProUGUI>();

		var coreObject = GameObject.Find("Core");
		clock = coreObject.GetComponent<Clock>();
	}

	void Update()
	{
		CalculateFPS();
	}

	void LateUpdate()
	{
		if (sbToPrint != null)
		{
			sbToPrint.Clear();
			sbToPrint.Append("Version : ");
			sbToPrint.Append(Application.version);

			sbToPrint.Append(", FPS : ");
			sbToPrint.Append(Mathf.Round(fps));

			if (string.IsNullOrEmpty(eventMessage))
			{
				InsertTimeInfo();
			}
			else
			{
				InsertEventMessage();
			}

			uiText.text = sbToPrint.ToString();
		}
	}

	public void ClearEventMessage()
	{
		SetEventMessage(string.Empty);
	}

	public void SetEventMessage(in string value)
	{
		eventMessage = value;
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

	private void InsertEventMessage()
	{
		InsertNewLine();
		sbToPrint.Append(eventMessage);
	}

	private void InsertNewLine()
	{
		sbToPrint.Append("\n");
	}

	private void InsertTimeInfo()
	{
		var simTime = (clock == null) ? Time.time : clock.GetSimTime();
		var realTime = (clock == null) ? Time.realtimeSinceStartup : clock.GetRealTime();

		var simTs = TimeSpan.FromSeconds(simTime);
		var realTs = TimeSpan.FromSeconds(realTime);
		var diffTs1 = realTs - simTs;

		var currentSimTime = simTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var currentRealTime = realTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var diffRealSimTime = diffTs1.ToString(@"d\:hh\:mm\:ss\.fff");

		InsertNewLine();

		sbToPrint.Append("(Time) Simulation: ");
		sbToPrint.Append(currentSimTime);

		sbToPrint.Append(", Real: ");
		sbToPrint.Append(currentRealTime);

		sbToPrint.Append(", (DiffTime) Real-Sim: ");
		sbToPrint.Append(diffRealSimTime);
	}
}