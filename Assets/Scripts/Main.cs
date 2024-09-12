/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DefaultExecutionOrder(30)]
public class Main : MonoBehaviour
{
	[Header("Clean all models and lights before load model")]
	[SerializeField]
	private bool _clearAllOnStart = true;

	[Header("World File")]
	[SerializeField]
	private string _worldFilename;

	[SerializeField]
	private List<string> _modelRootDirectories  = new List<string>();

	[SerializeField]
	private List<string> _worldRootDirectories = new List<string>();

	[SerializeField]
	private List<string> _fileRootDirectories = new List<string>();

	private FollowingTargetList _followingList = null;

	private static GameObject _core = null;
	private static GameObject _propsRoot = null;
	private static GameObject _worldRoot = null;
	private static GameObject _lightsRoot = null;
	private static GameObject _roadsRoot = null;
	private static GameObject _uiRoot = null;
	private static GameObject _uiMainCanvasRoot = null;

	private static UIController _uiController = null;
	private static InfoDisplay _infoDisplay = null;
	private static WorldNavMeshBuilder _worldNavMeshBuilder = null;
	private static RuntimeGizmos.TransformGizmo _transformGizmo = null;
	private static CameraControl _cameraControl = null;
	private static Segmentation.Manager _segmentationManager = null;
	private static MeshProcess.VHACD _vhacd = null;
	private static ObjectSpawning _objectSpawning = null;
	private static Main _instance = null;
	private static Pose _cameraInitPose = Pose.identity;

	private static bool _isResetting = false;
	private static bool _resetTriggered = false;

	public static GameObject PropsRoot => _propsRoot;
	public static GameObject WorldRoot => _worldRoot;
	public static GameObject RoadsRoot => _roadsRoot;
	public static GameObject CoreObject => _core;
	public static GameObject UIObject => _uiRoot;
	public static GameObject UIMainCanvas => _uiMainCanvasRoot;
	public static RuntimeGizmos.TransformGizmo Gizmos => _transformGizmo;
	public static ObjectSpawning ObjectSpawning => _objectSpawning;
	public static UIController UIController => _uiController;
	public static InfoDisplay InfoDisplay => _infoDisplay;
	public static WorldNavMeshBuilder WorldNavMeshBuilder => _worldNavMeshBuilder;
	public static BridgeManager BridgeManager => _bridgeManager;
	public static Segmentation.Manager SegmentationManager => _segmentationManager;
	public static CameraControl CameraControl => _cameraControl;
	public static MeshProcess.VHACD MeshVHACD => _vhacd;
	public static Main Instance => _instance;
	public static Pose CameraInitPose
	{
		get => _cameraInitPose;
		set => _cameraInitPose = value;
	}

	#region SDF Parser
	private SDF.Root _sdfRoot = null;
	private SDF.Import.Loader _sdfLoader = null;
	#endregion

	#region Non-Component class
	private static BridgeManager _bridgeManager = null;
	private static SimulationService _simulationService = null;
	#endregion

	private void CleanAllModels()
	{
		foreach (var child in _worldRoot.GetComponentsInChildren<Transform>())
		{
			// skip root gameobject
			if (child == null || child.gameObject == null || child.gameObject == _worldRoot)
			{
				continue;
			}

			GameObject.Destroy(child.gameObject);
		}
	}

	private void CleanAllLights()
	{
		foreach (var child in _lightsRoot.GetComponentsInChildren<Transform>())
		{
			// skip root gameobject
			if (child.gameObject == _lightsRoot)
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
		if (_worldRoot != null)
		{
			_worldRoot.transform.localRotation = Quaternion.identity;
			_worldRoot.transform.localPosition = Vector3.zero;
			_worldRoot.transform.localScale = Vector3.one;
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
			Debug.LogWarning("CLOISIM_FILES_PATH is null. It will use default path. \n" + String.Join(", ", _fileRootDirectories));
		}
		else
		{
			_fileRootDirectories.Clear();
			_fileRootDirectories.AddRange(filePaths);
			Debug.Log("Files Directory Paths: " + String.Join(", ", _fileRootDirectories));
		}

		var modelPathEnv = Environment.GetEnvironmentVariable("CLOISIM_MODEL_PATH");
		var modelPaths = modelPathEnv?.Split(separator, StringSplitOptions.RemoveEmptyEntries);

		if (modelPaths == null)
		{
			Debug.LogWarning("CLOISIM_MODEL_PATH is null. It will use default path. \n" + String.Join(", ", _modelRootDirectories ));
		}
		else
		{
			_modelRootDirectories .Clear();
			_modelRootDirectories .AddRange(modelPaths);
			Debug.Log("Models Directory Paths: " + String.Join(", ", _modelRootDirectories ));
		}

		var worldPathEnv = Environment.GetEnvironmentVariable("CLOISIM_WORLD_PATH");
		var worldPaths = worldPathEnv?.Split(separator, StringSplitOptions.RemoveEmptyEntries);

		if (worldPaths == null)
		{
			Debug.LogWarning("CLOISIM_WORLD_PATH is null. It will use default path. \n" + String.Join(", ", _worldRootDirectories));
		}
		else
		{
			_worldRootDirectories.Clear();
			_worldRootDirectories.AddRange(worldPaths);
			Debug.Log("World Directory Paths: " + String.Join(", ", _worldRootDirectories));
		}
#endif
	}

	void Awake()
	{
		_instance = this;

		GetResourcesPaths();

		// Load Library for Assimp
#if UNITY_EDITOR
		var AssimpVersion = "5.0.0-beta1";
#	if UNITY_EDITOR_LINUX
		var assimpLibraryPath = $"./Assets/Plugins/AssimpNet.{AssimpVersion}/runtimes/linux-x64/native/libassimp";
#	elif UNITY_EDITOR_OSX // TODO: need to be verified,
		var assimpLibraryPath = $"./Assets/Plugins/AssimpNet.{AssimpVersion}/runtimes/osx-x64/native/libassimp";
#	else // == UNITY_EDITOR_WIN
#		if UNITY_64
		var assimpLibraryPath = $"./Assets/Plugins/AssimpNet.{AssimpVersion}/runtimes/win-x64/native/assimp";
#		else
		var assimpLibraryPath = $"./Assets/Plugins/AssimpNet.{AssimpVersion}/runtimes/win-x86/native/assimp";
#		endif
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

		if (Assimp.Unmanaged.AssimpLibrary.Instance.IsLibraryLoaded == false)
		{
			Debug.LogError("Failed to load assimp library!!!!");
			return;
		}

		// Calling this method is required for windows version
		// refer to https://thomas.trocha.com/blog/netmq-on-unity3d/
		AsyncIO.ForceDotNet.Force();

		QualitySettings.vSyncCount = 0;
		Application.targetFrameRate = 60;
		OnDemandRendering.renderFrameInterval = 1;

		// Debug.Log(    QualitySettings.GetQualityLevel());
		var qualityLevel = Environment.GetEnvironmentVariable("CLOISIM_QUALITY");
		var qualityLevelIndex = 3; // Very High Quality Preset
		if (!string.IsNullOrEmpty(qualityLevel))
		{
			qualityLevelIndex = int.Parse(qualityLevel);
			qualityLevelIndex = Mathf.Clamp(qualityLevelIndex, 0, 4);
		}
		QualitySettings.SetQualityLevel(qualityLevelIndex);

		var mainCamera = Camera.main;
		mainCamera.depthTextureMode = DepthTextureMode.None;
		mainCamera.allowHDR = true;
		mainCamera.allowMSAA = true;

		_core = GameObject.Find("Core");
		if (_core == null)
		{
			Debug.LogError("Failed to Find 'Core'!!!!");
		}

		_propsRoot = GameObject.Find("Props");
		_worldRoot = GameObject.Find("World");
		_lightsRoot = GameObject.Find("Lights");
		_roadsRoot = GameObject.Find("Roads");
		_uiRoot = GameObject.Find("UI");

		_worldNavMeshBuilder = _worldRoot.GetComponent<WorldNavMeshBuilder>();

		var simWorld = _worldRoot.AddComponent<SimulationWorld>();
		DeviceHelper.SetGlobalClock(simWorld.GetClock());

		if (_uiRoot != null)
		{
			_infoDisplay = _uiRoot.GetComponentInChildren<InfoDisplay>();
			_transformGizmo = _uiRoot.GetComponentInChildren<RuntimeGizmos.TransformGizmo>();
			_uiController = _uiRoot.GetComponent<UIController>();

			_uiMainCanvasRoot = _uiRoot.transform.Find("Main Canvas").gameObject;
			_followingList = _uiMainCanvasRoot.GetComponentInChildren<FollowingTargetList>();
		}

		_cameraControl = mainCamera.GetComponent<CameraControl>();

		Main._bridgeManager = new BridgeManager();
		Main._simulationService = new SimulationService();

		var sphericalCoordinates = new SphericalCoordinates();
		DeviceHelper.SetGlobalSphericalCoordinates(sphericalCoordinates);

		SensorDevices.DepthCamera.LoadComputeShader();

		_objectSpawning = gameObject.AddComponent<ObjectSpawning>();

		_segmentationManager = gameObject.AddComponent<Segmentation.Manager>();

		_vhacd = gameObject.AddComponent<MeshProcess.VHACD>();
		_vhacd.m_parameters = VHACD.Params;
	}

	void Start()
	{
		if (!SystemInfo.supportsAsyncGPUReadback)
		{
			Debug.LogError("This API does not support AsyncGPURreadback.");
			_uiController?.SetErrorMessage("This API does not support AsyncGPURreadback.");
			return;
		}

		ResetRootModelsTransform();

		if (_clearAllOnStart)
		{
			CleanAllResources();
		}

		if (_simulationService.IsStarted())
		{
			var newWorldFilename = GetArgument("-world");

			if (string.IsNullOrEmpty(newWorldFilename))
			{
				newWorldFilename = GetArgument("-worldFile");
			}

			if (!string.IsNullOrEmpty(newWorldFilename))
			{
				_worldFilename = newWorldFilename;
			}

			_sdfRoot = new SDF.Root();
			_sdfRoot.fileDefaultPaths.AddRange(_fileRootDirectories);
			_sdfRoot.modelDefaultPaths.AddRange(_modelRootDirectories );
			_sdfRoot.worldDefaultPaths.AddRange(_worldRootDirectories);
			_sdfRoot.UpdateResourceModelTable();

			UpdateUIModelList();

			if (!string.IsNullOrEmpty(_worldFilename))
			{
				_uiController?.SetEventMessage("Start to load world file: " + _worldFilename);
				StartCoroutine(LoadWorld());
			}
		}
	}

	private void UpdateUIModelList()
	{
		if (_sdfRoot == null)
		{
			Debug.LogWarning("_sdfRoot is null");
			return;
		}

		// Update UI Model list
		var modelList = _uiMainCanvasRoot.transform.Find("ModelList").gameObject;
		var viewport = modelList.transform.GetChild(0);
		var buttonTemplate = viewport.Find("ButtonTemplate").gameObject;

		var contentList = viewport.GetChild(0).gameObject;
		foreach (var child in contentList.GetComponentsInChildren<Button>())
		{
			GameObject.Destroy(child.gameObject);
		}

		foreach (var item in _sdfRoot.resourceModelTable)
		{
			var itemValue = item.Value;
			var duplicatedbutton = GameObject.Instantiate(buttonTemplate);
			duplicatedbutton.SetActive(true);
			duplicatedbutton.transform.SetParent(contentList.transform, false);

			var textComponent = duplicatedbutton.GetComponentInChildren<Text>();
			textComponent.text = itemValue.Item1;

			var buttonComponent = duplicatedbutton.GetComponentInChildren<Button>();
			buttonComponent.onClick.AddListener(delegate ()
			{
				// Debug.Log(itemValue.Item1 + ", " + itemValue.Item2 + ", " + itemValue.Item3);
				StartCoroutine(LoadModel(itemValue.Item2, itemValue.Item3));
			});
		}
	}

	private string GetClonedModelName(in string modelName)
	{
		var worldTrnasform = _worldRoot.transform;
		var numbering = 0;
		var tmpModelName = modelName;
		for (var i = 0; i < worldTrnasform.childCount; i++)
		{
			var childTransform = worldTrnasform.GetChild(i);

			if (childTransform.name.CompareTo(tmpModelName) == 0)
			{
				tmpModelName = modelName + "_clone_" + numbering++;
				i = 0;
			}
		}
		return tmpModelName;
	}

	public GameObject GetModel(string modelPath)
	{
		if (modelPath.EndsWith("/"))
		{
			modelPath = modelPath.Substring(0, modelPath.Length - 1);
		}

		foreach (var item in _sdfRoot.resourceModelTable)
		{
			var itemValue = item.Value;

			// Debug.Log(itemValue.Item1 + ", " + itemValue.Item2 + ", " + itemValue.Item3);
			if (itemValue.Item2.CompareTo(modelPath) == 0)
			{
				// Debug.Log(itemValue.Item1 + ", " + itemValue.Item2 + ", " + itemValue.Item3);
				var modelFileName = itemValue.Item3;
				if (_sdfRoot.DoParse(out var model, modelPath, modelFileName))
				{
					model.Name = GetClonedModelName(model.Name);

					StartCoroutine(_sdfLoader.Start(model));

					var targetObject = _worldRoot.transform.Find(model.Name);
					// Debug.Log(targetObject);
					return targetObject.gameObject;
				}
			}
		}

		return null;
	}

	private IEnumerator LoadModel(string modelPath, string modelFileName)
	{
		if (_sdfRoot.DoParse(out var model, modelPath, modelFileName))
		{
			// Debug.Log("Parsed: " + item.Key + ", " + item.Value.Item1 + ", " +  item.Value.Item2);
			model.Name = GetClonedModelName(model.Name);

			yield return StartCoroutine(_sdfLoader.Start(model));

			var targetObject = _worldRoot.transform.Find(model.Name);

			var modelImporter = _uiMainCanvasRoot.GetComponentInChildren<ModelImporter>();
			modelImporter.SetModelForDeploy(targetObject);

			// Debug.Log("Model Loaded:" + targetObject.name);
			yield return new WaitForEndOfFrame();

			// for GUI
			_followingList?.UpdateList();
		}

		yield return null;
	}

	private IEnumerator LoadWorld()
	{
		// Debug.Log("Hello CLOiSim World!!!!!");
		Debug.Log("Target World: " + _worldFilename);

		if (_sdfRoot.DoParse(out var world, _worldFilename))
		{
			SDF.Import.Util.RootModels = _worldRoot;

			_sdfLoader = new SDF.Import.Loader();
			_sdfLoader.SetRootLights(_lightsRoot);
			_sdfLoader.SetRootRoads(_roadsRoot);

			yield return _sdfLoader.Start(world);

			// for GUI
			_followingList?.UpdateList();

			yield return new WaitForEndOfFrame();

			Reset();
		}
		else
		{
			var errorMessage = "Parsing failed!!! Failed to load world file: " + _worldFilename;
			Debug.LogError(errorMessage);
			_uiController?.SetErrorMessage(errorMessage);
		}

		_bridgeManager.PrintLog();
	}

	public void OnSaveButtonClicked()
	{
		// Debug.Log("OnSaveButtonClicked");
		SaveWorld();
	}

	private void SaveWorld()
	{
		var saveDoc = _sdfRoot.GetOriginalDocument();

		var worldSaver = new WorldSaver(saveDoc);
		worldSaver.Update();

		_sdfRoot.Save();
	}


	void LateUpdate()
	{
#if UNITY_EDITOR
		if (Input.GetKey(KeyCode.LeftAlt))
#else
		if (Input.GetKey(KeyCode.LeftControl))
#endif
		{
			// Debug.Log("LeftControl Triggered");
		 	if (Input.GetKeyUp(KeyCode.R))
			{
				// Debug.Log("Reset Triggered");
				_resetTriggered = true;
			}
			else if (Input.GetKeyUp(KeyCode.S))
			{
				// Debug.Log("Save World");
				OnSaveButtonClicked();
			}
		}

		if (_resetTriggered && !_isResetting)
		{
			if (Input.GetKey(KeyCode.LeftShift))
			{
				// full Reset
				_isResetting = true;
				SceneManager.LoadScene(SceneManager.GetActiveScene().name);
				_isResetting = false;
			}
			else
			{
				_resetTriggered = false;
				StartCoroutine(ResetSimulation());
			}
		}
	}

	public static bool TriggerResetService()
	{
		if (_isResetting)
		{
			return false;
		}

		_resetTriggered = true;
		return true;
	}

	void Reset()
	{
		foreach (var helper in _worldRoot.GetComponentsInChildren<SDF.Helper.Base>())
		{
			helper.Reset();
		}

		foreach (var plugin in _worldRoot.GetComponentsInChildren<CLOiSimPlugin>())
		{
			plugin.Reset();
		}
	}

	private IEnumerator ResetSimulation()
	{
		_isResetting = true;
		// Debug.LogWarning("Reset positions in simulation!!!");

		_transformGizmo?.ClearTargets();

		Reset();

		DeviceHelper.GetGlobalClock()?.ResetTime();
		Debug.LogWarning("[Done] Reset positions in simulation!!!");
		yield return new WaitForSeconds(0.1f);

		_isResetting = false;
	}

	/// <summary>
	/// Eg:  CLOiSim.x86_64 -worldFile lg_seocho.world
	/// read the "-worldFile" command line argument
	/// </summary>
	private string GetArgument(in string arg_name)
	{
		var args = Environment.GetCommandLineArgs();
		for (var i = 0; i < args.Length; i++)
		{
			if (args[i].CompareTo(arg_name) == 0 && args.Length > i + 1)
			{
				return args[i + 1];
			}
		}
		return null;
	}

	void OnDestroy()
	{
		SensorDevices.DepthCamera.UnloadComputeShader();

		if (Main.BridgeManager != null)
		{
			Main.BridgeManager.Dispose();
		}

		if (Main._simulationService != null)
		{
			Main._simulationService.Dispose();
		}

		if (Assimp.Unmanaged.AssimpLibrary.Instance.IsLibraryLoaded)
		{
			Assimp.Unmanaged.AssimpLibrary.Instance.FreeLibrary();
		}
	}
}
