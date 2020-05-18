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
	private TextMeshProUGUI uiText = null;
	private StringBuilder sbToPrint;

	private int frameCount = 0;
	private float dt = 0.0F;
	private float fps = 0.0F;

	private const float fpsUpdatePeriod = 0.5F;

	// Start is called before the first frame update
	void Awake()
	{
		sbToPrint = new StringBuilder();
		uiText = GetComponent<TextMeshProUGUI>();
	}

	void Update()
	{
		frameCount++;
		dt += Time.unscaledDeltaTime;
		if (dt > fpsUpdatePeriod)
		{
			fps = frameCount / dt;
			dt -= fpsUpdatePeriod;
			frameCount = 0;
		}
	}

	void LateUpdate()
	{
		var simTs = TimeSpan.FromSeconds(Time.time);
		// var simFixedTs = TimeSpan.FromSeconds(Time.fixedTime);
		var realTs = TimeSpan.FromSeconds(Time.realtimeSinceStartup);
		var diffTs1 = realTs - simTs;
		// var diffTs2 = realTs - simFixedTs;
		// var timeScale = Time.timeScale;

		var currentSimTime = simTs.ToString(@"d\:hh\:mm\:ss\.fff");
		// var currentFixedTime = simFixedTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var currentRealTime = realTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var diffRealSimTime = diffTs1.ToString(@"d\:hh\:mm\:ss\.fff");
		// var diffRealFixedTime = diffTs2.ToString(@"d\:hh\:mm\:ss\.fff");

		if (sbToPrint != null)
		{
			sbToPrint.Clear();
			sbToPrint.Append("Version : ");
			sbToPrint.Append(Application.version);

			// sbToPrint.Append(", Real-Fixed: ");
			// sbToPrint.Append(diffRealFixedTime);

			// sbToPrint.Append(", (Time)Scale : ");
			// sbToPrint.Append(timeScale.ToString("F1"));

			sbToPrint.Append(", FPS : ");
			sbToPrint.Append(Mathf.Round(fps));

			sbToPrint.Append("\n");

			sbToPrint.Append("(Time) Simulation: ");
			sbToPrint.Append(currentSimTime);

			// sbToPrint.Append(", Fixed: ");
			// sbToPrint.Append(currentFixedTime);

			sbToPrint.Append(", Real: ");
			sbToPrint.Append(currentRealTime);

			sbToPrint.Append(", (DiffTime) Real-Sim: ");
			sbToPrint.Append(diffRealSimTime);

			uiText.text = sbToPrint.ToString();
		}
	}
}