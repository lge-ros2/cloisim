/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

/// <summary>
/// Attach to any GameObject that has a MeshRenderer.
/// Automatically notifies <see cref="URTSensorManager"/> when the
/// renderer is enabled or disabled, so the BVH is rebuilt without
/// requiring a per-frame FindObjectsByType scan.
///
/// For dynamically spawned objects, add this component alongside
/// the MeshRenderer (e.g., in ObjectSpawning or the SDF import pipeline).
/// The periodic fallback timer in URTSensorManager still catches
/// objects that don't carry this component.
/// </summary>
public class URTSceneChangeNotifier : MonoBehaviour
{
	private void OnEnable()
	{
		URTSensorManager.MarkSceneDirty();
	}

	private void OnDisable()
	{
		URTSensorManager.MarkSceneDirty();
	}
}
