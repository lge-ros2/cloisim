/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CLOiSim.EditorTools
{
	[InitializeOnLoad]
	public static class AutoOpenMainSceneOnStartup
	{
		private const string MainScenePath = "Assets/Scenes/MainScene.unity";
		private const string StartupSceneLoadKey = "CLOiSim.AutoOpenMainSceneOnStartup.Loaded";
		private const double MinimumStartupDelaySeconds = 1.0d;
		private static readonly double _startupTime;

		static AutoOpenMainSceneOnStartup()
		{
			if (Application.isBatchMode || SessionState.GetBool(StartupSceneLoadKey, false))
			{
				return;
			}

			_startupTime = EditorApplication.timeSinceStartup;
			EditorApplication.update += TryOpenMainScene;
		}

		private static void TryOpenMainScene()
		{
			if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
			{
				return;
			}

			if (EditorApplication.timeSinceStartup - _startupTime < MinimumStartupDelaySeconds)
			{
				return;
			}

			if (Resources.FindObjectsOfTypeAll<SceneView>().Length == 0)
			{
				return;
			}

			EditorApplication.update -= TryOpenMainScene;

			var activeScene = EditorSceneManager.GetActiveScene();
			if (activeScene.path == MainScenePath || activeScene.isDirty)
			{
				SessionState.SetBool(StartupSceneLoadKey, true);
				return;
			}

			SessionState.SetBool(StartupSceneLoadKey, true);
			EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
		}
	}
}
#endif