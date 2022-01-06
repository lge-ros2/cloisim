/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(30)]
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

	private FollowingTargetList followingList = null;

	private static GameObject coreObject = null;
	private static GameObject worldRoot = null;
	private static GameObject lightsRoot = null;
	private static GameObject uiRoot = null;

	private static SimulationDisplay simulationDisplay = null;
	private static InfoDisplay _infoDisplay = null;
	private static WorldNavMeshBuilder worldNavMeshBuilder = null;
	private static RuntimeGizmos.TransformGizmo transformGizmo = null;

#region "Non-Component class"
	private static BridgeManager bridgeManager = null;
	private static SimulationService simulationService = null;
#endregion

	private static bool isResetting = false;
	private static bool resetTriggered = false;

	public static GameObject WorldRoot => worldRoot;
	public static GameObject CoreObject => coreObject;
	public static GameObject UIObject => uiRoot;
	public static RuntimeGizmos.TransformGizmo Gizmos => transformGizmo;
	public static SimulationDisplay Display => simulationDisplay;
	public static InfoDisplay InfoDisplay => _infoDisplay;
	public static WorldNavMeshBuilder WorldNavMeshBuilder => worldNavMeshBuilder;
	public static BridgeManager BridgeManager => bridgeManager;

#region "SDFParser"
	private SDF.Root sdfRoot = null;
	private SDF.Import.Loader sdfLoader = null;
#endregion

	private void CleanAllModels()
	{
		foreach (var child in worldRoot.GetComponentsInChildren<Transform>())
		{
			// skip root gameobject
			if (child == null || child.gameObject == null || child.gameObject == worldRoot)
			{
				continue;
			}

			GameObject.Destroy(child.gameObject);
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

			GameObject.Destroy(child.gameObject);
		}
	}

	private void CleanAllResources()
	{
		CleanAllLights();
		CleanAllModels();
	}

	private void ResetRootModelsTransform()
	{
		if (worldRoot != null)
		{
			worldRoot.transform.localRotation = Quaternion.identity;
			worldRoot.transform.localPosition = Vector3.zero;
			worldRoot.transform.localScale = Vector3.one;
		}
	}

	private void GetResourcesPaths()
	{
		var separator = new char[] { ':' };
#if UNITY_EDITOR
#else
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
#endif
	}

	void Awake()
	{
		GetResourcesPaths();

		// Load Library for Assimp
#if UNITY_EDITOR
#	if UNITY_EDITOR_LINUX
		var assimpLibraryPath = "./Assets/Plugins/AssimpNet.4.1.0/runtimes/linux-x64/native/libassimp";
#	elif UNITY_EDITOR_OSX // TODO: need to be verified,
		var assimpLibraryPath = "./Assets/Plugins/AssimpNet.4.1.0/runtimes/osx-x64/native/libassimp";
#	else // == UNITY_EDITOR_WIN
		var assimpLibraryPath = "./Assets/Plugins/AssimpNet.4.1.0/runtimes/win-x64/native/assimp";
#	endif
#else
#	if UNITY_STANDALONE_WIN
		var assimpLibraryPath = "./CLOiSim_Data/Plugins/x86_64/assimp";
#	elif UNITY_STANDALONE_OSX // TODO: need to be verified,
		var assimpLibraryPath = "./Contents/PlugIns/libassimp";
#	else // == UNITY_STANDALONE_LINUX
		var assimpLibraryPath = "./CLOiSim_Data/Plugins/libassimp";
#	endif
#endif
		Assimp.Unmanaged.AssimpLibrary.Instance.LoadLibrary(assimpLibraryPath);

		// Calling this method is required for windows version
		// refer o https://thomas.trocha.com/blog/netmq-on-unity3d/
		AsyncIO.ForceDotNet.Force();

		Application.targetFrameRate = 61;

		var mainCamera = Camera.main;
		mainCamera.depthTextureMode = DepthTextureMode.None;
		mainCamera.allowHDR = true;
		mainCamera.allowMSAA = true;

		coreObject = GameObject.Find("Core");
		if (coreObject == null)
		{
			Debug.LogError("Failed to Find 'Core'!!!!");
		}

		worldRoot = GameObject.Find("World");

		lightsRoot = GameObject.Find("Lights");

		uiRoot = GameObject.Find("UI");

		worldNavMeshBuilder = worldRoot.GetComponent<WorldNavMeshBuilder>();

		Main.bridgeManager = new BridgeManager();
		Main.simulationService = new SimulationService();

		var simWorld = gameObject.AddComponent<SimulationWorld>();
		DeviceHelper.SetGlobalClock(simWorld.GetClock());

		var sphericalCoordinates = new SphericalCoordinates();
		DeviceHelper.SetGlobalSphericalCoordinates(sphericalCoordinates);

		followingList = uiRoot.GetComponentInChildren<FollowingTargetList>();
		simulationDisplay = uiRoot.GetComponentInChildren<SimulationDisplay>();
		_infoDisplay = uiRoot.GetComponentInChildren<InfoDisplay>();

		transformGizmo = uiRoot.GetComponentInChildren<RuntimeGizmos.TransformGizmo>();

		gameObject.AddComponent<ObjectSpawning>();
	}

	void Start()
	{
		if (!SystemInfo.supportsAsyncGPUReadback)
		{
			Debug.LogError("This API does not support AsyncGPURreadback.");
			simulationDisplay?.SetErrorMessage("This API does not support AsyncGPURreadback.");
			return;
		}

		ResetRootModelsTransform();

		if (clearAllOnStart)
		{
			CleanAllResources();
		}

		var newWorldFilename = GetArgument("-world");

		if (string.IsNullOrEmpty(newWorldFilename))
		{
			newWorldFilename = GetArgument("-worldFile");
			if (!string.IsNullOrEmpty(newWorldFilename))
			{
				worldFileName = newWorldFilename;
			}
		}

		if (!doNotLoad && !string.IsNullOrEmpty(worldFileName))
		{
			simulationDisplay?.ClearLogMessage();
			simulationDisplay?.SetEventMessage("Start to load world file: " + worldFileName);
			StartCoroutine(LoadWorld());
		}
	}

	private void UpdateUIModelList(ref SDF.Root root)
	{
		// Update UI Model list
		var modelList = Main.UIObject.transform.GetChild(0).Find("ModelList").gameObject;
		var buttonTemplate = modelList.transform.GetChild(0).Find("ButtonTemplate").gameObject;

		var contentList = modelList.transform.GetChild(0).GetChild(0).gameObject;

		foreach (var child in contentList.GetComponentsInChildren<Button>())
		{
			GameObject.Destroy(child.gameObject);
		}

		foreach (var item in root.resourceModelTable)
		{
			var duplicatedbutton = GameObject.Instantiate(buttonTemplate);
			duplicatedbutton.SetActive(true);
			duplicatedbutton.transform.SetParent(contentList.transform, false);

			var textComponent = duplicatedbutton.GetComponentInChildren<Text>();
			textComponent.text = item.Key;

			var buttonComponent = duplicatedbutton.GetComponentInChildren<Button>();
			buttonComponent.onClick.AddListener(delegate ()
			{
				StartCoroutine(LoadModel(item.Value.Item1, item.Value.Item2));
			});
		}
	}

	private IEnumerator LoadModel(string modelPath, string modelFileName)
	{
		if (sdfRoot.DoParse(out var model, modelPath, modelFileName))
		{
			// Debug.Log("Parsed: " + item.Key + ", " + item.Value.Item1 + ", " +  item.Value.Item2);

			// Check name duplication
			var worldTrnasform = worldRoot.transform;
			var numbering = 0;
			var tmpModelName = model.Name;
			for (var i = 0; i < worldTrnasform.childCount; i++)
			{
				var childTransform = worldTrnasform.GetChild(i);

				if (childTransform.name.CompareTo(tmpModelName) == 0)
				{
					tmpModelName = model.Name + "_clone_" + numbering++;
				}
			}
			model.Name = tmpModelName;
			yield return StartCoroutine(sdfLoader.StartImport(model));

			var targetObject = Main.WorldRoot.transform.Find(model.Name);
		}

		yield return null;
	}

	private IEnumerator LoadWorld()
	{
		Console.SetOut(new DebugLogWriter());
		Console.SetError(new DebugLogWriter(true));

		// Debug.Log("Hello CLOiSim World!!!!!");
		Debug.Log("Target World: " + worldFileName);

		sdfRoot = new SDF.Root();
		sdfRoot.fileDefaultPaths.AddRange(fileRootDirectories);
		sdfRoot.modelDefaultPaths.AddRange(modelRootDirectories);
		sdfRoot.worldDefaultPaths.AddRange(worldRootDirectories);
		sdfRoot.UpdateResourceModelTable();

		UpdateUIModelList(ref sdfRoot);

		if (sdfRoot.DoParse(out var world, worldFileName))
		{
			sdfLoader = new SDF.Import.Loader();
			sdfLoader.SetRootModels(worldRoot);
			sdfLoader.SetRootLights(lightsRoot);

			yield return sdfLoader.StartImport(world);

			// for GUI
			simulationDisplay?.ClearLogMessage();
			followingList?.UpdateList();

			yield return new WaitForEndOfFrame();

			Reset();
		}
		else
		{
			var errorMessage = "Parsing failed!!! Failed to load world file: " + worldFileName;
			Debug.LogError(errorMessage);
			simulationDisplay?.SetErrorMessage(errorMessage);
		}

		bridgeManager.PrintLog();
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

	public static bool TriggerResetService()
	{
		if (isResetting)
		{
			return false;
		}

		resetTriggered = true;
		return true;
	}

	void Reset()
	{
		foreach (var helper in worldRoot.GetComponentsInChildren<SDF.Helper.Visual>())
		{
			helper.Reset();
		}

		foreach (var helper in worldRoot.GetComponentsInChildren<SDF.Helper.Actor>())
		{
			helper.Reset();
		}

		foreach (var helper in worldRoot.GetComponentsInChildren<SDF.Helper.Link>())
		{
			helper.Reset();
		}

		foreach (var helper in worldRoot.GetComponentsInChildren<SDF.Helper.Model>())
		{
			helper.Reset();
		}

		foreach (var plugin in worldRoot.GetComponentsInChildren<CLOiSimPlugin>())
		{
			plugin.Reset();
		}
	}

	private IEnumerator ResetSimulation()
	{
		isResetting = true;
		// Debug.LogWarning("Reset positions in simulation!!!");

		transformGizmo?.ClearTargets();

		Reset();

		DeviceHelper.GetGlobalClock()?.ResetTime();
		Debug.LogWarning("[Done] Reset positions in simulation!!!");
		yield return new WaitForSeconds(0.1f);

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

	void OnDestroy()
	{
		Main.bridgeManager.Dispose();
		Main.simulationService.Dispose();

		if (Assimp.Unmanaged.AssimpLibrary.Instance.IsLibraryLoaded)
		{
			Assimp.Unmanaged.AssimpLibrary.Instance.FreeLibrary();
		}
	}
}