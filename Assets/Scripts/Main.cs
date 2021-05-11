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

public class Main: MonoBehaviour
{
	[Header("Block Loading SDF")]
	public bool doNotLoad = false;

	[Header("Clean all models and lights before load model")]
	public bool clearAllOnStart = true;

	[Header("World File")]
	public string worldFileName;

	public List<string> modelRootDirectories = new List<string>();
	public List<string> worldRootDirectories = new List<string>();
	public List<string> fileRootDirectories = new List<string>();

	private GameObject modelsRoot = null;
	private GameObject lightsRoot = null;
	private FollowingTargetList followingList = null;
	private SimulationDisplay simulationDisplay = null;
	private RuntimeGizmos.TransformGizmo transformGizmo = null;
	private Clock clock = null;

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

	private void CleanAllLights()
	{
		foreach (var child in lightsRoot.GetComponentsInChildren<Transform>())
		{
			// skip root gameobject
			if (child.gameObject == lightsRoot)
			{
				continue;
			}

			GameObject.Destroy(child.gameObject, 0.00001f);
		}
	}

	private void CleanAllResources()
	{
		CleanAllLights();
		CleanAllModels();
	}

	private void ResetRootModelsTransform()
	{
		if (modelsRoot != null)
		{
			modelsRoot.transform.localRotation = Quaternion.identity;
			modelsRoot.transform.localPosition = Vector3.zero;
			modelsRoot.transform.localScale = Vector3.one;
		}
	}

	private void GetResourcesPaths()
	{
		var separator = new char[] { ':' };

		var filePathEnv = Environment.GetEnvironmentVariable("CLOISIM_FILES_PATH");
		var filePaths = filePathEnv?.Split(separator, StringSplitOptions.RemoveEmptyEntries);

		if (filePaths == null)
		{
			Debug.LogWarning("CLOISIM_FILES_PATH is null. It will use default path. \n" + String.Join(", ", fileRootDirectories));
		}
		else
		{
			fileRootDirectories.Clear();
			fileRootDirectories.AddRange(filePaths);
			Debug.Log("Files Directory Paths: " + String.Join(", ", fileRootDirectories));
		}

		var modelPathEnv = Environment.GetEnvironmentVariable("CLOISIM_MODEL_PATH");
		var modelPaths = modelPathEnv?.Split(separator, StringSplitOptions.RemoveEmptyEntries);

		if (modelPaths == null)
		{
			Debug.LogWarning("CLOISIM_MODEL_PATH is null. It will use default path. \n" + String.Join(", ", modelRootDirectories));
		}
		else
		{
			modelRootDirectories.Clear();
			modelRootDirectories.AddRange(modelPaths);
			Debug.Log("Models Directory Paths: " + String.Join(", ", modelRootDirectories));
		}

		var worldPathEnv = Environment.GetEnvironmentVariable("CLOISIM_WORLD_PATH");
		var worldPaths = worldPathEnv?.Split(separator, StringSplitOptions.RemoveEmptyEntries);

		if (worldPaths == null)
		{
			Debug.LogWarning("CLOISIM_WORLD_PATH is null. It will use default path. \n" + String.Join(", ", worldRootDirectories));
		}
		else
		{
			worldRootDirectories.Clear();
			worldRootDirectories.AddRange(worldPaths);
			Debug.Log("World Directory Paths: " + String.Join(", ", worldRootDirectories));
		}
	}

	void Awake()
	{
		GetResourcesPaths();

		// Load Library for Assimp
#if UNITY_EDITOR
		var assimpLibraryPath = "./Assets/Plugins/AssimpNet.4.1.0/runtimes/linux-x64/native";
#else
		var assimpLibraryPath = "./CLOiSim_Data/Plugins";
#endif
		Assimp.Unmanaged.AssimpLibrary.Instance.LoadLibrary(assimpLibraryPath + "/libassimp");

		Application.targetFrameRate = 61;

		var mainCamera = Camera.main;
		mainCamera.depthTextureMode = DepthTextureMode.None;
		mainCamera.allowHDR = true;
		mainCamera.allowMSAA = true;

		modelsRoot = GameObject.Find("Models");

		lightsRoot = GameObject.Find("Lights");

		var UIRoot = GameObject.Find("UI");

		followingList = UIRoot.GetComponentInChildren<FollowingTargetList>();
		simulationDisplay = UIRoot.GetComponentInChildren<SimulationDisplay>();
		transformGizmo = UIRoot.GetComponentInChildren<RuntimeGizmos.TransformGizmo>();

		clock = GetComponent<Clock>();
		DeviceHelper.SetGlobalClock(clock);

		var sphericalCoordinates = GetComponent<SphericalCoordinates>();
		DeviceHelper.SetGlobalSphericalCoordinates(sphericalCoordinates);

		ResetRootModelsTransform();
	}

	void Start()
	{
		if (clearAllOnStart)
		{
			CleanAllResources();
		}

		var newWorldFilename = GetArgument("-world");

		if (string.IsNullOrEmpty(newWorldFilename))
		{
			newWorldFilename = GetArgument("-worldFile");
		}

		if (!string.IsNullOrEmpty(newWorldFilename))
		{
			worldFileName = newWorldFilename;
		}

		if (!doNotLoad && !string.IsNullOrEmpty(worldFileName))
		{
			simulationDisplay?.SetEventMessage("Start to load world file: " + worldFileName);
			StartCoroutine(LoadWorld());
		}
	}

	private IEnumerator LoadWorld()
	{
		// Debug.Log("Hello CLOiSim World!!!!!");
		Debug.Log("Target World: " + worldFileName);

		var sdf = new SDF.Root();
		sdf.SetTargetLogOutput(simulationDisplay);
		sdf.SetWorldFileName(worldFileName);
		sdf.fileDefaultPaths.AddRange(fileRootDirectories);
		sdf.modelDefaultPaths.AddRange(modelRootDirectories);
		sdf.worldDefaultPaths.AddRange(worldRootDirectories);

		if (sdf.DoParse())
		{
			yield return new WaitForSeconds(0.001f);

			var loader = new SDF.Import.Loader();
			loader.SetRootModels(modelsRoot);
			loader.SetRootLights(lightsRoot);

			yield return loader.StartImport(sdf.World());

			// for GUI
			simulationDisplay?.ClearLogMessage();
			followingList?.UpdateList();
		}
		else
		{
			var errorMessage = "Parsing failed!!!, failed to load world file: " + worldFileName;
			Debug.LogError(errorMessage);
			simulationDisplay?.SetErrorMessage(errorMessage);
		}

		yield return null;
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

	private void OnDestroy()
	{
		foreach (var worldPlugin in GetComponents<CLOiSimPlugin>())
		{
			worldPlugin.Stop();
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
		// Debug.LogWarning("Reset positions in simulation!!!");

		transformGizmo?.ClearTargets();

		foreach (var helper in modelsRoot.GetComponentsInChildren<SDF.Helper.Actor>())
		{
			helper.Reset();
		}

		foreach (var helper in modelsRoot.GetComponentsInChildren<SDF.Helper.Model>())
		{
			helper.Reset();
		}

		foreach (var helper in modelsRoot.GetComponentsInChildren<SDF.Helper.Link>())
		{
			helper.Reset();
		}

		foreach (var plugin in modelsRoot.GetComponentsInChildren<CLOiSimPlugin>())
		{
			plugin.Reset();
		}

		clock?.ResetTime();

		yield return new WaitForSeconds(0.5f);
		Debug.LogWarning("[Done] Reset positions in simulation!!!");
		isResetting = false;
	}

	/// <summary>
	/// Eg:  CLOiSim.x86_64 -worldFile lg_seocho.world
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