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

	private int frameCount = 0;
	private float dt = 0.0F;
	private float fps = 0.0F;

	private const float fpsUpdatePeriod = 0.5F;

	// Start is called before the first frame update
	void Awake()
	{
		uiText = GetComponent<TextMeshProUGUI>();

		var coreObject = GameObject.Find("Core");
		clock = coreObject.GetComponent<Clock>();
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
		var simTime = (clock == null) ? Time.time : clock.GetSimTime();
		var realTime = (clock == null) ? Time.realtimeSinceStartup : clock.GetRealTime();

		var simTs = TimeSpan.FromSeconds(simTime);
		var realTs = TimeSpan.FromSeconds(realTime);
		var diffTs1 = realTs - simTs;

		var currentSimTime = simTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var currentRealTime = realTs.ToString(@"d\:hh\:mm\:ss\.fff");
		var diffRealSimTime = diffTs1.ToString(@"d\:hh\:mm\:ss\.fff");

		if (sbToPrint != null)
		{
			sbToPrint.Clear();
			sbToPrint.Append("Version : ");
			sbToPrint.Append(Application.version);

			sbToPrint.Append(", FPS : ");
			sbToPrint.Append(Mathf.Round(fps));

			sbToPrint.Append("\n");

			sbToPrint.Append("(Time) Simulation: ");
			sbToPrint.Append(currentSimTime);

			sbToPrint.Append(", Real: ");
			sbToPrint.Append(currentRealTime);

			sbToPrint.Append(", (DiffTime) Real-Sim: ");
			sbToPrint.Append(diffRealSimTime);


			uiText.text = sbToPrint.ToString();
		}
	}
}