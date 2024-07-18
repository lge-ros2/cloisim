/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.IO;

public class DebugLogWriter : TextWriter
{
	private bool isError = false;
	private bool showOnDisplay = false;

	public DebugLogWriter(in bool errorLog = false)
	{
		//Debug.Log("Initialized!!!");
		isError = errorLog;
	}

	public override void Write(string value)
	{
		if (value != null)
		{
			base.Write(value);
			Print(value);
		}
	}

	public override void WriteLine(string value)
	{
		if (value != null)
		{
			base.Write(value + "\n");
			Print(value);
		}
	}

	public void SetShowOnDisplayOnce()
	{
		showOnDisplay = true;
	}

	private void Print(in string value)
	{
		if (isError)
		{
			Debug.LogWarning(value);
			if (showOnDisplay)
			{
				Main.Display?.SetErrorMessage(value);
				showOnDisplay = false;
			}
		}
		else
		{
			Debug.Log(value);
			if (showOnDisplay)
			{
				Main.Display?.SetEventMessage(value);
				showOnDisplay = false;
			}
		}
	}

	public override System.Text.Encoding Encoding
	{
		get { return System.Text.Encoding.UTF8; }
	}
}