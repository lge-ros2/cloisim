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
	private bool isError = false;

	public void SetSkip(in bool value)
	{
		isSkip = value;
	}

	public void SetWarningOnce()
	{
		isWarning = true;
	}

	public void SetErrorOnce()
	{
		isError = true;
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

		Print(value);
	}

	public override void WriteLine(string value)
	{
		if (isSkip || value == null)
		{
			return;
		}

		base.WriteLine(value);

		Print(value);
	}

	private void Print(in string value)
	{
		if (isWarning)
		{
			Debug.LogWarning(value);
			isWarning = false;
		}
		else if (isError)
		{
			Debug.LogError(value);
			isError = false;
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