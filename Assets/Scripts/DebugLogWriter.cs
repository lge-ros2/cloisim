/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class DebugLogWriter : System.IO.TextWriter
{
	private bool isSkip = false;
	private bool isWarning = false;

	public void SetSkip(in bool value) {
	{
		isSkip = value;
	}
}
	public void SetWarning(in bool value)
	{
		isWarning = value;
	}

	public DebugLogWriter()
	{
		//Debug.Log("Initialized!!!");
	}

	public override void Write(string value)
	{
		if (isSkip || value == null)
		{
			return;
		}

		base.Write(value);

		if (isWarning)
		{
			Debug.LogWarning(value);
		}
		else
		{
			Debug.Log(value);
		}
	}

	public override void WriteLine(string value)
	{
		if (isSkip || value == null)
			return;

		base.WriteLine(value);

		if (isWarning)
		{
			Debug.LogWarning(value);
		}
		else
		{
			Debug.Log(value);
		}
	}

	public override System.Text.Encoding Encoding
	{
		get { return System.Text.Encoding.UTF8; }
	}
}