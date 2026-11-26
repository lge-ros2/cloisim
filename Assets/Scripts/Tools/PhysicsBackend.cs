/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public static class PhysicsBackend
{
	public static void LogCurrent()
	{
		var current = Physics.GetCurrentIntegrationInfo();
		var message = $"Physics backend: {current.name} (id={current.id}) - {current.description}";

		if (current.isFallback)
		{
			Debug.LogError($"{message} - fallback (null) backend is active, physics is disabled. " +
				"Check that the selected backend plugin is installed and loaded.");
			return;
		}

		Debug.Log(message);
	}
}
