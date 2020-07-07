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
	private List<string> modelRootDirectories;
	private List<string> worldRootDirectories;

	private GameObject modelsRoot = null;

	private bool isResetting = false;
	private bool resetTriggered = false;

	ModelLoader()
	{
		modelRootDirectories = new List<string>();
		worldRootDirectories= new List<string>();
	}

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
		Application.targetFrameRate = 60;

		modelsRoot = GameObject.Find(modelsRootName);

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
				importer.SetMainCamera(defaultCameraName);
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
		if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.R))
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
				isResetting = true;
				StartCoroutine(ResetSimulation());
			}
		}
	}

	public string TriggerResetService(in string command)
	{
		// Debug.Log(command);

		if (command.Equals("reset"))
		{
			if (isResetting)
			{
				return SimulationService.FAIL;
			}

			resetTriggered = true;

			return SimulationService.SUCCESS;
		}

		return SimulationService.FAIL;
	}

	private IEnumerator ResetSimulation()
	{
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

		yield return new WaitForSeconds(1);

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