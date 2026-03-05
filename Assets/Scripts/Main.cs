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
using Assimp.Unmanaged;

[DefaultExecutionOrder(30)]
public class Main : MonoBehaviour
{
	private static float DefaultOrthographicSize = 8;

	[Header("Clean all models and lights before load model")]

	[SerializeField]
	private bool _clearAllOnStart = true;

	[Header("World File")]
	[SerializeField]
	private string _worldFilename;

	[Header("Screen capture file name")]
	[SerializeField]
	private string _screenCaptureFilename;

	private string _loadedWorldFilePath = string.Empty;

	[SerializeField]
	private List<string> _modelRootDirectories  = new();

	[SerializeField]
	private List<string> _worldRootDirectories = new();

	[SerializeField]
	private List<string> _fileRootDirectories = new();

	private FollowingTargetList _followingList = null;

	private static Main _instance = null;
	private GameObject _core = null;
	private GameObject _propsRoot = null;
	private GameObject _worldRoot = null;
	private GameObject _lightsRoot = null;
	private GameObject _roadsRoot = null;
	private GameObject _uiRoot = null;
	private GameObject _uiMainCanvasRoot = null;
	private SimulationWorld _simulationWorld = null;
	private UIController _uiController = null;
	private InfoDisplay _infoDisplay = null;
	private WorldNavMeshBuilder _worldNavMeshBuilder = null;
	private RuntimeGizmos.TransformGizmo _transformGizmo = null;
	private CameraControl _cameraControl = null;
	private Segmentation.Manager _segmentationManager = null;
	private MeshProcess.VHACD _vhacd = null;
	private ObjectSpawning _objectSpawning = null;
	private ModelImporter _modelImporter = null;
	private PluginStartTracker _pluginStartTracker = new();
	private Pose _cameraInitPose = Pose.identity;
	private string _trackVisualModelName = string.Empty;
	private Vector3 _trackVisualPosition = Vector3.zero;
	private bool _trackVisualInheritYaw = false;

	private bool _pluginAllStarted = false;
	private bool _isResetting = false;
	private bool _resetTriggered = false;
	private bool _startRecordTriggered = false;
	private bool _stopRecordTriggered = false;

	public static GameObject PropsRoot => _instance._propsRoot;
	public static GameObject WorldRoot => _instance._worldRoot;
	public static GameObject RoadsRoot => _instance._roadsRoot;
	public static GameObject CoreObject => _instance._core;
	public static GameObject UIObject => _instance._uiRoot;
	public static GameObject UIMainCanvas => _instance._uiMainCanvasRoot;
	public static RuntimeGizmos.TransformGizmo Gizmos => _instance._transformGizmo;
	public static ObjectSpawning ObjectSpawning => _instance._objectSpawning;
	public static ModelImporter ModelImporter => _instance._modelImporter;
	public static UIController UIController => _instance._uiController;
	public static InfoDisplay InfoDisplay => _instance._infoDisplay;
	public static WorldNavMeshBuilder WorldNavMeshBuilder => _instance._worldNavMeshBuilder;
	public static BridgeManager BridgeManager => _instance._bridgeManager;
	public static SimulationService SimulationService => _instance._simulationService;
	public static Segmentation.Manager SegmentationManager => _instance._segmentationManager;
	public static CameraControl CameraControl => _instance._cameraControl;
	public static MeshProcess.VHACD MeshVHACD => _instance._vhacd;
	public static Main Instance => _instance;
	public static Pose CameraInitPose
	{
		get => _instance._cameraInitPose;
		set => _instance._cameraInitPose = value;
	}
	public static string TrackVisualModelName
	{
		get => _instance._trackVisualModelName;
		set => _instance._trackVisualModelName = value;
	}
	public static Vector3 TrackVisualPosition
	{
		get => _instance._trackVisualPosition;
		set => _instance._trackVisualPosition = value;
	}
	public static bool TrackVisualInheritYaw
	{
		get => _instance._trackVisualInheritYaw;
		set => _instance._trackVisualInheritYaw = value;
	}

	#region SDF Parser
	private SDF.Root _sdfRoot = null;
	private SDF.Import.Loader _sdfLoader = null;
	#endregion

	#region Non-Component class
	private BridgeManager _bridgeManager = null;
	private SimulationService _simulationService = null;
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

		var logger = new DebugLogWriter();
		var loggerErr = new DebugLogWriter(true);
		Console.SetOut(logger);
		Console.SetError(loggerErr);

		GetResourcesPaths();

		// Load Library for Assimp
#if UNITY_EDITOR
 		var pluginsFolder = System.IO.Path.Combine(Application.dataPath, "Plugins");
		var AssimpVersion = "6.0.2.1";
#	if UNITY_EDITOR_LINUX
		var assimpLibraryPath = $"{pluginsFolder}/AssimpNetter.{AssimpVersion}/runtimes/linux-x64/native/libassimp";
#	elif UNITY_EDITOR_OSX // TODO: need to be verified,
		var assimpLibraryPath = $"{pluginsFolder}/AssimpNetter.{AssimpVersion}/runtimes/osx-x64/native/libassimp";
#	else // == UNITY_EDITOR_WIN
		var assimpLibraryPath = $"{pluginsFolder}/AssimpNetter.{AssimpVersion}/runtimes/win-x64/native/assimp";
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
  		AssimpLibrary.Instance.LoadLibrary(assimpLibraryPath);

		if (AssimpLibrary.Instance.IsLibraryLoaded == false)
		{
			Debug.LogError("Failed to load assimp library!!!!");
			return;
		}

		// Calling this method is required for windows version
		// refer to https://thomas.trocha.com/blog/netmq-on-unity3d/
		AsyncIO.ForceDotNet.Force();

		QualitySettings.vSyncCount = 0;
		OnDemandRendering.renderFrameInterval = 1;

		// Detect batchmode via Application.isBatchMode OR by scanning
		// command-line arguments — Unity 6000 player builds sometimes
		// report isBatchMode=false even when -batchmode was passed.
		var cmdArgs = Environment.GetCommandLineArgs();
		var hasBatchArg = System.Array.Exists(cmdArgs, a => a == "-batchmode");
		var isBatchMode = Application.isBatchMode || hasBatchArg;

		// Detect Xvfb virtual framebuffer (tiny display like 1x1x24).
		// When running on Xvfb, the screen resolution is tiny and there's
		// no human viewer — enable sensor-only mode automatically.
		// Note: Screen.width measures the Unity window size, not the desktop.
		// Screen.currentResolution measures the actual display resolution.
		var desktopRes = Screen.currentResolution;
		var isXvfb = (desktopRes.width <= 4 && desktopRes.height <= 4);
		var displayEnv = Environment.GetEnvironmentVariable("DISPLAY");
		if (isXvfb)
		{
			Debug.Log($"[Main] Tiny display detected ({desktopRes.width}x{desktopRes.height}) — Xvfb mode assumed (DISPLAY={displayEnv})");
		}

		var sensorOnlyEnv = Environment.GetEnvironmentVariable("CLOISIM_SENSOR_ONLY");
		var enableSensorOnly = isBatchMode || isXvfb
			|| (!string.IsNullOrEmpty(sensorOnlyEnv) && sensorOnlyEnv == "1");
		Debug.Log("[Main] Application.isBatchMode=" + Application.isBatchMode
			+ ", cmdline has -batchmode=" + hasBatchArg
			+ ", effective isBatchMode=" + isBatchMode
			+ ", isXvfb=" + isXvfb
			+ ", enableSensorOnly=" + enableSensorOnly);

		if (isBatchMode || enableSensorOnly)
		{
			// Sensor-only / headless: determine optimal frame rate.
			//
			// Xvfb mode: uncap FPS — there's no display, no thermal concern from
			// a tiny 1x1 backbuffer, and higher FPS = more render opportunities
			// for sensors = easier to hit target rates.
			//
			// Batchmode (non-Xvfb): cap at 60 to prevent GPU thermal throttling.
			// 9 cameras at 30 Hz = 270 renders/sec. At ~6 camera steps per frame,
			// we need 270/6 = 45 FPS minimum. 60 FPS gives 33% headroom.
			//
			// Env var CLOISIM_TARGET_FPS overrides all.
			var targetFps = isXvfb ? -1 : 60;
			var envFps = Environment.GetEnvironmentVariable("CLOISIM_TARGET_FPS");
			if (!string.IsNullOrEmpty(envFps) && int.TryParse(envFps, out var customFps))
			{
				targetFps = customFps;
			}
			Application.targetFrameRate = targetFps;
			QualitySettings.vSyncCount = 0; // disable vsync — use targetFrameRate only
			Debug.Log($"[Main] targetFrameRate={targetFps} for {(isXvfb ? "Xvfb" : isBatchMode ? "batchmode" : "sensor-only")} (set CLOISIM_TARGET_FPS to override, -1=uncapped)");
		}
		else
		{
			Application.targetFrameRate = 60;
		}

		// Debug.Log(    QualitySettings.GetQualityLevel());
		var qualityLevel = Environment.GetEnvironmentVariable("CLOISIM_QUALITY");
		var qualityLevelIndex = 3; // Very High Quality Preset
		if (!string.IsNullOrEmpty(qualityLevel))
		{
			qualityLevelIndex = int.Parse(qualityLevel);
			qualityLevelIndex = Mathf.Clamp(qualityLevelIndex, 0, 4);
		}
		QualitySettings.SetQualityLevel(qualityLevelIndex);

		// Enable texture streaming to reduce GPU memory usage for distant textures
		QualitySettings.streamingMipmapsActive = true;
		QualitySettings.streamingMipmapsMemoryBudget = 512;
		QualitySettings.streamingMipmapsAddAllCameras = true;

		// Reduce shadow distance for better performance
		QualitySettings.shadowDistance = 50f;
		QualitySettings.shadowResolution = ShadowResolution.Medium;

		var mainCamera = Camera.main;
		mainCamera.depthTextureMode = DepthTextureMode.None;
		mainCamera.allowHDR = false; // Deferred rendering doesn't benefit; saves bandwidth
		mainCamera.allowMSAA = false; // MSAA is ignored in deferred mode
		mainCamera.allowDynamicResolution = true;
		mainCamera.useOcclusionCulling = true;
		mainCamera.orthographic = false;

		// Set per-layer culling distances to reduce draw calls for distant objects
		var layerCullDistances = new float[32];
		for (var i = 0; i < layerCullDistances.Length; i++)
		{
			layerCullDistances[i] = mainCamera.farClipPlane;
		}
		// "Default" layer gets a tighter cull distance for small objects
		layerCullDistances[LayerMask.NameToLayer("Default")] = mainCamera.farClipPlane * 0.5f;
		mainCamera.layerCullDistances = layerCullDistances;
		mainCamera.layerCullSpherical = true;

		// Sensor-only mode: minimize main viewport camera to save GPU time.
		// Activated automatically in batchmode (no human viewer) or via CLOISIM_SENSOR_ONLY=1.
		if (enableSensorOnly)
		{
			// Sensor-only: no human viewer needs the main viewport.
			// Disable culling to save GPU time for sensor cameras.
			// Note: Cannot set mainCamera.enabled=false because Camera.main
			// returns null when disabled, breaking SDF import code.
			mainCamera.cullingMask = 0;
			mainCamera.clearFlags = CameraClearFlags.SolidColor;

			Debug.Log("[Main] Sensor-only mode: Main camera culling disabled");

			// Increase the sensor render budget — prioritize sensor throughput.
			var budgetMs = isXvfb ? 50f : 40f;
			SensorDevices.SensorRenderManager.Instance.FrameBudgetMs = budgetMs;
			Debug.Log($"[Main] SensorRenderManager.FrameBudgetMs raised to {budgetMs}ms for sensor-only");
		}

		_cameraControl = mainCamera.gameObject.AddComponent<PerspectiveCameraControl>();

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

		_simulationWorld = _worldRoot.AddComponent<SimulationWorld>();
		DeviceHelper.SetGlobalClock(_simulationWorld.GetClock());

		if (_uiRoot != null)
		{
			_infoDisplay = _uiRoot.GetComponentInChildren<InfoDisplay>();
			_transformGizmo = _uiRoot.GetComponentInChildren<RuntimeGizmos.TransformGizmo>();
			_uiController = _uiRoot.GetComponent<UIController>();

			_uiMainCanvasRoot = _uiRoot.transform.Find("Main Canvas").gameObject;
			_followingList = _uiMainCanvasRoot.GetComponentInChildren<FollowingTargetList>();
		}

		_bridgeManager = new();
		_simulationService = new();

		var sphericalCoordinates = new SphericalCoordinates();
		DeviceHelper.SetGlobalSphericalCoordinates(sphericalCoordinates);

		SensorDevices.DepthCamera.LoadComputeShader();

		_objectSpawning = gameObject.AddComponent<ObjectSpawning>();

		_modelImporter = gameObject.AddComponent<ModelImporter>();

		_segmentationManager = gameObject.AddComponent<Segmentation.Manager>();

		gameObject.AddComponent<SensorDevices.DXRSensorManager>();

		_vhacd = gameObject.AddComponent<MeshProcess.VHACD>();
		_vhacd.m_parameters = VHACD.Params;

		if (_clearAllOnStart)
		{
			CleanAllResources();
		}

		ResetRootModelsTransform();
	}

	void Start()
	{
		if (!SystemInfo.supportsAsyncGPUReadback)
		{
			Debug.LogError("This API does not support AsyncGPURreadback.");
			_uiController?.SetErrorMessage("This API does not support AsyncGPURreadback.");
			return;
		}

		if (_simulationService.IsStarted())
		{
			_screenCaptureFilename = GetArgument("-capture");

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
			_sdfRoot.modelDefaultPaths.AddRange(_modelRootDirectories);
			_sdfRoot.worldDefaultPaths.AddRange(_worldRootDirectories);
			_sdfRoot.UpdateResourceModelTable();

			ModelImporter.UpdateUIModelList(_sdfRoot.ResourceModelTable);

			if (!string.IsNullOrEmpty(_worldFilename))
			{
				_uiController?.SetEventMessage("Start to load world file: " + _worldFilename);
				StartCoroutine(LoadWorld());
			}
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

			if (childTransform.name.Equals(tmpModelName))
			{
				tmpModelName = modelName + "_clone_" + numbering++;
				i = 0;
			}
		}
		return tmpModelName;
	}

	public IEnumerator LoadModel(string modelPath, string modelFileName)
	{
		_uiController?.SetInfoMessage($"Model({modelFileName}) is now loading....");

		if (_sdfRoot.DoParse(out var model, modelPath, modelFileName))
		{
			_bridgeManager.ClearAllocatedHistory();

			// Debug.Log("Parsed: " + item.Key + ", " + item.Value.Item1 + ", " +  item.Value.Item2);
			model.Name = GetClonedModelName(model.Name);

			Physics.simulationMode = SimulationMode.Script;
			GameObject targetObject = null;
			yield return _sdfLoader.Start(model, onCreatedRoot: obj => targetObject = (obj as GameObject));

			yield return new WaitUntil(() => targetObject != null);

			_pluginAllStarted = false;

			_pluginStartTracker.AllStartedEvent -= OnAllPluginsStarted;
			_pluginStartTracker.AllStartedEvent += OnAllPluginsStarted;

			_pluginStartTracker.ProgressChanged -= OnPluginProgressChanged;
			_pluginStartTracker.ProgressChanged += OnPluginProgressChanged;

			_pluginStartTracker.Bind(targetObject);

			Physics.SyncTransforms();
			Physics.simulationMode = SimulationMode.FixedUpdate;

			_modelImporter?.SetModelForDeploy(targetObject.transform);

			_followingList?.UpdateList();

			yield return new WaitUntil(() => _pluginAllStarted);
			_bridgeManager.PrintAllocatedHistory();

			var message = $"Model({modelFileName}) is loaded > {model.Name}";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);
		}
	}

	private IEnumerator LoadWorld()
	{
		Debug.Log("Target World: " + _worldFilename);
		_uiController?.SetInfoMessage($"World({_worldFilename}) is now loading....");

		if (_sdfRoot.DoParse(out var world, out _loadedWorldFilePath, _worldFilename))
		{
			_sdfLoader = new SDF.Import.Loader();
			_sdfLoader.SetRootLights(_lightsRoot);
			_sdfLoader.SetRootRoads(_roadsRoot);

			Physics.simulationMode = SimulationMode.Script;
			yield return _sdfLoader.Start(world);

			yield return new WaitUntil(() => _worldRoot.transform.childCount > 0);

			_pluginAllStarted = false;

			_pluginStartTracker.AllStartedEvent -= OnAllPluginsStarted;
			_pluginStartTracker.AllStartedEvent += OnAllPluginsStarted;

			_pluginStartTracker.ProgressChanged -= OnPluginProgressChanged;
			_pluginStartTracker.ProgressChanged += OnPluginProgressChanged;

			_pluginStartTracker.Bind(_worldRoot);

			Physics.SyncTransforms();
			Physics.simulationMode = SimulationMode.FixedUpdate;

			Reset();

			_followingList?.UpdateList();

			yield return new WaitUntil(() => _pluginAllStarted);
			_bridgeManager.PrintAllocatedHistory();

			TrackModel();

			var message = $"World({_worldFilename}) is loaded";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);
		}
		else
		{
			var errorMessage = $"Parsing failed!!! Failed to load world file: {_worldFilename}";
			Debug.LogError(errorMessage);
			_uiController?.SetErrorMessage(errorMessage);
		}

		if (!string.IsNullOrEmpty(_screenCaptureFilename))
		{
			var recording = ToggleRecord();
			_uiController?.OnRecordClicked(recording);
		}
	}

	private void OnPluginProgressChanged(int started, int total)
	{
		_uiController?.SetInfoMessage($"Starting plugins... ({started}/{total})");
	}

	private void OnAllPluginsStarted()
	{
		_pluginAllStarted = true;

		if (!string.IsNullOrEmpty(_pluginStartTracker.AllSummaries))
		{
			Debug.LogWarning(_pluginStartTracker.AllSummaries);
		}

		var message = $"All plugins started! ({_pluginStartTracker.StartedCount}/{_pluginStartTracker.TotalCount})";
		_uiController?.SetInfoMessage(message);
		Debug.Log(message);
	}

	public void TrackModel()
	{
		if (!string.IsNullOrEmpty(_trackVisualModelName))
		{
			_followingList.StartFollowing(_trackVisualModelName);
			var followingCamera = Main.UIObject.GetComponentInChildren<FollowingCamera>();

			if (followingCamera != null)
			{
				followingCamera.SetInitialRelativePosition(_trackVisualPosition);
				followingCamera.AlignSameDirection(_trackVisualInheritYaw);
			}
		}
	}

	public bool ToggleRecord()
	{
		var recordStarted = false;
		var recorder = Camera.main.GetComponent<UltraFastWebMRecorder>();
		if (!recorder.IsRecording)
		{
			recorder.SetOutput(baseName: _screenCaptureFilename);
			recordStarted = recorder.StartCapture();
		}
		else
			recorder.StopCapture();

		return recordStarted;
	}

	public void StartRecord()
	{
		var recorder = Camera.main.GetComponent<UltraFastWebMRecorder>();
		if (recorder.IsRecording)
			return;

		recorder.SetOutput(baseName: _screenCaptureFilename);
		var recordStarted = recorder.StartCapture();
		Main.UIController?.OnRecordClicked(recordStarted);
	}

	public void StopRecord()
	{
		var recorder = UnityEngine.Camera.main.GetComponent<UltraFastWebMRecorder>();
		recorder.StopCapture();
		Main.UIController?.OnRecordClicked(false);
	}

	public void SaveWorld()
	{
		var saveDoc = _sdfRoot.GetOriginalDocument();

		var worldSaver = new WorldSaver(saveDoc);
		worldSaver.Update();

		_sdfRoot.Save(_loadedWorldFilePath);
	}

	public static void SetCameraPerspective(in bool isPerspectiveViewControl = true)
	{
		GameObject.Destroy(Camera.main.GetComponent<CameraControl>());

		Camera.main.orthographic = false;

		if (isPerspectiveViewControl)
		{
			Instance._cameraControl = Camera.main.gameObject.AddComponent<PerspectiveCameraControl>();
		}
		else
		{
			Instance._cameraControl = Camera.main.gameObject.AddComponent<OrthographicCameraControl>();
		}
	}

	public static void SetCameraOrthographic(in bool isOrthographicViewControl = true)
	{
		GameObject.Destroy(Camera.main.GetComponent<CameraControl>());

		Camera.main.orthographic = true;
		Camera.main.orthographicSize = DefaultOrthographicSize;

		if (isOrthographicViewControl)
		{
			Instance._cameraControl = Camera.main.gameObject.AddComponent<OrthographicCameraControl>();
		}
		else
		{
			Instance._cameraControl = Camera.main.gameObject.AddComponent<PerspectiveCameraControl>();

		}
	}

	void LateUpdate()
	{
		if (Input.GetKey(KeyCode.LeftControl))
		{
			// Debug.Log("LeftControl Triggered");
		 	if (Input.GetKeyUp(KeyCode.R))
			{
				// Debug.Log("Reset Triggered");
				_resetTriggered = true;
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

		if (_startRecordTriggered)
		{
			StartRecord();
			_startRecordTriggered = false;
		}
		else if (_stopRecordTriggered)
		{
			StopRecord();
			_stopRecordTriggered = false;
		}
	}

	public bool TriggerResetService()
	{
		if (_isResetting)
		{
			return false;
		}

		_resetTriggered = true;
		return true;
	}

	public void TriggerStartRecordService(in string captureFilename)
	{
		_screenCaptureFilename = captureFilename;
		_startRecordTriggered = true;
	}

	public void TriggerStopRecordService()
	{
		_stopRecordTriggered = true;
	}

	void Reset()
	{
		foreach (var helper in _worldRoot.GetComponentsInChildren<SDF.Helper.Base>())
		{
			helper.Reset();
		}

		foreach (var device in _worldRoot.GetComponentsInChildren<Device>())
		{
			device.Reset();
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

		_simulationWorld?.SignalReset();

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
			if (args[i].Equals(arg_name) && args.Length > i + 1)
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

		if (_simulationService != null)
		{
			_simulationService.Dispose();
		}

		if (Assimp.Unmanaged.AssimpLibrary.Instance.IsLibraryLoaded)
		{
			Assimp.Unmanaged.AssimpLibrary.Instance.FreeLibrary();
		}
	}
}
