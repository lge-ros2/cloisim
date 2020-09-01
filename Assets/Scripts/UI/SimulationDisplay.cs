/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Text;
using UnityEngine;

public class SimulationDisplay : MonoBehaviour
{
	private Clock clock = null;
	private string eventMessage = string.Empty;
	private StringBuilder sbTimInfo = new StringBuilder();

	[Header("fps")]
	private const float fpsUpdatePeriod = 0.5f;
	private int frameCount = 0;
	private float dT = 0.0F;
	private float fps = 0.0F;

	[Header("GUI properties")]
	private const int labelFontSize = 16;

	private const int textMargin = 7;

	private const int textHeight = 22;

	private const int textWidthFps = 80;
	private const int textWidthVersion = 50;
	private const int textWidthSimulation = 650;

	[Header("Rect")]
	private Rect rectVersion = new Rect(textMargin, textMargin, textWidthVersion, textHeight);
	private Rect rectFps = new Rect(Screen.width - textWidthFps - textMargin, textMargin, textWidthFps, textHeight);
	private Rect rectSimulationinfo = new Rect(textMargin, Screen.height - textHeight- textMargin, textWidthSimulation, textHeight);

	// Start is called before the first frame update
	void Awake()
	{
		var coreObject = GameObject.Find("Core");
		clock = DeviceHelper.GetGlobalClock();
	}

	void Update()
	{
		CalculateFPS();
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

	private string GetTimeInfoString()
	{
		var simTime = (clock == null) ? Time.time : clock.GetSimTime();
		var realTime = (clock == null) ? Time.realtimeSinceStartup : clock.GetRealTime();

		var simTs = TimeSpan.FromSeconds(simTime);
		var realTs = TimeSpan.FromSeconds(realTime);
		var diffTs1 = realTs - simTs;

		var currentSimTime = simTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var currentRealTime = realTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var diffRealSimTime = diffTs1.ToString(@"d\:hh\:mm\:ss\.fff");

		sbTimInfo.Clear();
		sbTimInfo.AppendFormat("(Time) Simulation: {0}, Real: {1}, (Diff) Real-Sim: {2}", currentSimTime, currentRealTime, diffRealSimTime);
		return sbTimInfo.ToString();
	}

	private string GetBoldText(in string value)
	{
		return ("<b>" + value + "</b>");
	}

	void OnGUI()
	{
		GUI.skin.label.fontSize = labelFontSize;

		// version info
		GUI.color = Color.green;
		GUI.Label(rectVersion, GetBoldText(Application.version));

		// fps info
		GUI.color = Color.blue;

		var originSkinLabelAlign = GUI.skin.label.alignment;
		GUI.skin.label.alignment = TextAnchor.MiddleRight;

		rectFps.x = Screen.width - textWidthFps - textMargin;
		GUI.Label(rectFps, GetBoldText("FPS [" + Mathf.Round(fps).ToString("F1") + "]"));

		GUI.skin.label.alignment = originSkinLabelAlign;

		// Simulation time info or event message
		GUI.color = Color.black;
		rectSimulationinfo.y = Screen.height - textHeight - textMargin;

		var simulationInfo = (string.IsNullOrEmpty(eventMessage)) ? GetTimeInfoString() : eventMessage;
		GUI.Label(rectSimulationinfo, GetBoldText(simulationInfo));
	}
}