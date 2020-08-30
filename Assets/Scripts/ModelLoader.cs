/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using UnityEditor;

public class ModelLoader : MonoBehaviour
{
	[Header("Block Loading SDF")]
	public bool doNotLoad = false;

	[Header("Pause after load models")]
	public bool pauseOnStart = false;

	[Header("Clean all models before load model")]
	public bool clearAllOnStart = true;

	[Header("World File")]
	private string modelsRootName = "Models";
	private string defaultCameraName = "Main Camera";
	private string followingListName = "FollowingTargetList";

	public string worldFileName;

	private string filesRootDirectory = string.Empty;

	public List<string> modelRootDirectories = new List<string>();
	public List<string> worldRootDirectories = new List<string>();

	private GameObject modelsRoot = null;
	private Clock clock = null;
	private Camera mainCamera = null;

	private bool isResetting = false;
	private bool resetTriggered = false;

	private void CleanAllModels()
	{
		foreach (var child in modelsRoot.GetComponentsInChildren<Transform>())
		{
			// skip root gameobject
			if (child.gameObject == modelsRoot)
			{
				continue;
			}

			GameObject.Destroy(child.gameObject, 0.00001f);
		}
	}

	private void ResetTransform()
	{
		if (modelsRoot != null)
		{
			modelsRoot.transform.localRotation = Quaternion.identity;
			modelsRoot.transform.localPosition = Vector3.zero;
			modelsRoot.transform.localScale = Vector3.one;
		}
	}

	void Awake()
	{
#if UNITY_EDITOR
 		filesRootDirectory = "/usr/share/gazebo-9/";
		modelRootDirectories.Add("../sample-resources/models/");
		worldRootDirectories.Add("../sample-resources/worlds/");
		// modelRootDirectories.Add("../../lgrs_resource/assets/models/");
		// worldRootDirectories.Add("../../lgrs_resource/worlds/");
#else
		var separator = new char[] {':'};
		filesRootDirectory = Environment.GetEnvironmentVariable("CLOISIM_FILES_PATH");
		var modelPathEnv = Environment.GetEnvironmentVariable("CLOISIM_MODEL_PATH");
		var modelPaths = modelPathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		modelRootDirectories.AddRange(modelPaths);
		var worldPathEnv = Environment.GetEnvironmentVariable("CLOISIM_WORLD_PATH");
		var worldPaths = worldPathEnv.Split(separator, StringSplitOptions.RemoveEmptyEntries);
		worldRootDirectories.AddRange(worldPaths);
#endif

		mainCamera = Camera.main;
		mainCamera.depthTextureMode = DepthTextureMode.None;
		mainCamera.allowHDR = true;
		mainCamera.allowMSAA = true;

		Application.targetFrameRate = 61;

		modelsRoot = GameObject.Find(modelsRootName);

		clock = GetComponent<Clock>();

		ResetTransform();
	}

	void Start()
	{
		if (clearAllOnStart)
		{
			CleanAllModels();
		}

		var newWorldFile = GetArgument("-worldFile");
		if (!string.IsNullOrEmpty(newWorldFile))
		{
			worldFileName = newWorldFile;
		}

		StartCoroutine(LoadSdfModels());
	}

	private IEnumerator LoadSdfModels()
	{
		// Debug.Log("Hello CLOiSim World!!!!!");
		Debug.Log("World: " + worldFileName);

		// Main models loader
		if (!doNotLoad && !string.IsNullOrEmpty(worldFileName))
		{
			var sdf = new SDF.Root();
			sdf.SetWorldFileName(worldFileName);
			sdf.fileDefaultPath = filesRootDirectory;
			sdf.modelDefaultPaths.AddRange(modelRootDirectories);
			sdf.worldDefaultPath.AddRange(worldRootDirectories);

			if (sdf.DoParse())
			{
				yield return new WaitForSeconds(0.005f);

				var importer = new SDFImporter();
				importer.SetRootObject(modelsRoot);
				importer.SetMainCamera(mainCamera);
				importer.Start(sdf.World());
			}
			else
			{
				Debug.LogError("Parsing failed!!");
			}
		}

#if UNITY_EDITOR
		if (pauseOnStart)
		{
			EditorApplication.isPaused = true;
		}
#endif

		// for GUI
		var followingListObject = GameObject.Find(followingListName);
		var followingTargetList = followingListObject.GetComponent<FollowingTargetList>();
		followingTargetList.UpdateList();
	}

	void LateUpdate()
	{
		if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyUp(KeyCode.R))
		{
			resetTriggered = true;
		}

		if (resetTriggered && !isResetting)
		{
			if (Input.GetKey(KeyCode.LeftShift))
			{
				// full Reset
				isResetting = true;
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
				isResetting = false;
			}
			else
			{
				resetTriggered = false;

				StartCoroutine(ResetSimulation());
			}
		}
	}

	public bool TriggerResetService()
	{
		if (isResetting)
		{
			return false;
		}

		resetTriggered = true;
		return true;
	}

	private IEnumerator ResetSimulation()
	{
		isResetting = true;

		Debug.LogWarning("Reset positions in simulation!!!");

		foreach (var plugin in modelsRoot.GetComponentsInChildren<ModelPlugin>())
		{
			plugin.Reset();
		}

		foreach (var plugin in modelsRoot.GetComponentsInChildren<LinkPlugin>())
		{
			plugin.Reset();
		}

		foreach (var plugin in modelsRoot.GetComponentsInChildren<DevicePlugin>())
		{
			plugin.Reset();
		}

		yield return null;

		if (clock != null)
		{
			clock.ResetTime();
		}

		yield return new WaitForSeconds(0.5f);
		Debug.LogWarning("[Done] Reset positions in simulation!!!");
		isResetting = false;
	}

	/// <summary>
	/// Eg:  CLOiSim.x86_64 -worldFile gazebo.world
	/// read the "-worldFile" command line argument
	/// </summary>
	private static string GetArgument(in string name)
	{
		var args = Environment.GetCommandLineArgs();
		for (var i = 0; i < args.Length; i++)
		{
			if (args[i] == name && args.Length > i + 1)
			{
				return args[i + 1];
			}
		}
		return null;
	}
}