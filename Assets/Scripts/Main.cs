/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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

	private CrashReporter _crashReporter = null;

	private LoadingCursor _loadingCursor = null;

	private bool _pluginAllStarted = false;
	private bool _isResetting = false;
	private bool _resetTriggered = false;
	private bool _startRecordTriggered = false;
	private bool _stopRecordTriggered = false;
	private bool _teleportTriggered = false;
	private TeleportOperation _pendingTeleportOperation;

	private string _pendingModelInfoQuery = null;
	private Pose _pendingModelInfoResult;
	private readonly ManualResetEventSlim _modelInfoQueryEvent = new(false);

	public static GameObject PropsRoot => _instance._propsRoot;
	public static GameObject WorldRoot => _instance._worldRoot;
	public static GameObject RoadsRoot => _instance._roadsRoot;

	public static GameObject Core => _instance._core;

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
	private SDFormat.RootLoader _sdfRoot = null;
	private SDFormat.Import.Loader _sdfLoader = null;
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

			Destroy(child.gameObject);
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

			Destroy(child.gameObject);
		}
	}

	private void CleanAllResources()
	{
		CleanAllLights();
		CleanAllModels();
		VHACD.ClearCache();
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

	private static void ReplaceStandaloneInputModule()
	{
		var eventSystem = UnityEngine.EventSystems.EventSystem.current;
		if (eventSystem != null)
		{
			var standaloneModule = eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
			if (standaloneModule != null)
			{
				Destroy(standaloneModule);
				if (eventSystem.GetComponent<InputSystemUIInputModule>() == null)
				{
					eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
				}
			}
		}
		else
		{
			var go = new GameObject("EventSystem");
			go.AddComponent<UnityEngine.EventSystems.EventSystem>();
			go.AddComponent<InputSystemUIInputModule>();
		}
	}

	void Awake()
	{
		_instance = this;

		_crashReporter = new CrashReporter();

		ReplaceStandaloneInputModule();

		var logger = new DebugLogWriter();
		var loggerErr = new DebugLogWriter(true);
		Console.SetOut(logger);
		Console.SetError(loggerErr);

		GetResourcesPaths();

		// Load Library for Assimp
#if UNITY_EDITOR
 		var pluginsFolder = Path.Combine(Application.dataPath, "Plugins");
		var assimpDir = Directory.GetDirectories(pluginsFolder, "AssimpNetter.*").OrderByDescending(d => d).FirstOrDefault();

		if (assimpDir == null)
		{
			throw new Exception("AssimpNetter folder not found in Plugins");
		}

#	if UNITY_EDITOR_LINUX
		var assimpLibraryPath = Path.Combine(assimpDir, "runtimes/linux-x64/native/libassimp");
#	elif UNITY_EDITOR_OSX // TODO: need to be verified,
		var assimpLibraryPath = Path.Combine(assimpDir, "runtimes/osx-x64/native/libassimp");
#	else // == UNITY_EDITOR_WIN
		var assimpLibraryPath = Path.Combine(assimpDir, "runtimes/win-x64/native/assimp");
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

		_vhacd = gameObject.AddComponent<MeshProcess.VHACD>();
		_vhacd.m_parameters = VHACD.Params;

		_loadingCursor = gameObject.AddComponent<LoadingCursor>();

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

			_sdfRoot = new SDFormat.RootLoader();
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
		if (_sdfRoot.DoParse(out var model, modelPath, modelFileName))
		{
			_loadingCursor?.Activate();
			_uiController?.SetInfoMessage($"Model '{model.Name}' is now loading....");
			yield return null;

			_bridgeManager.ClearAllocatedHistory();

			// Debug.Log("Parsed: " + item.Key + ", " + item.Value.Item1 + ", " +  item.Value.Item2);
			model.Name = GetClonedModelName(model.Name);

			Physics.simulationMode = SimulationMode.Script;
			GameObject targetObject = null;
			yield return _sdfLoader.Start(model, onCreatedRoot: obj => targetObject = obj as GameObject);

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

			var message = $"Model '{model.Name}' is successfully loaded.";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);

			_loadingCursor?.Deactivate();
		}
	}

	private IEnumerator LoadWorld()
	{
		Debug.Log("Target World: " + _worldFilename);
		_uiController?.SetInfoMessage($"World '{_worldFilename}' is now loading....");
		_loadingCursor?.Activate();

		if (_sdfRoot.DoParse(out var world, out _loadedWorldFilePath, _worldFilename))
		{
			_sdfLoader = new SDFormat.Import.Loader();
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

			ResetWorld();

			_followingList?.UpdateList();

			yield return new WaitUntil(() => _pluginAllStarted);
			_bridgeManager.PrintAllocatedHistory();

			TrackModel();

			var message = $"World '{_worldFilename}' is loaded";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);

			_loadingCursor?.Deactivate();
		}
		else
		{
			var errorMessage = $"Parsing failed!!! Failed to load world file: {_worldFilename}";
			Debug.LogError(errorMessage);
			_uiController?.SetErrorMessage(errorMessage);

			_loadingCursor?.Deactivate();
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
			var followingCamera = UIObject.GetComponentInChildren<FollowingCamera>();

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
		UIController?.OnRecordClicked(recordStarted);
	}

	public void StopRecord()
	{
		var recorder = Camera.main.GetComponent<UltraFastWebMRecorder>();
		recorder.StopCapture();
		UIController?.OnRecordClicked(false);
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
		Destroy(Camera.main.GetComponent<CameraControl>());

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
		Destroy(Camera.main.GetComponent<CameraControl>());

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
#if DEVELOPMENT_BUILD || UNITY_EDITOR
		// Ctrl+Shift+F12: Trigger test crash for CrashReporter verification
		if (Keyboard.current[Key.LeftCtrl].isPressed &&
			Keyboard.current[Key.LeftShift].isPressed &&
			Keyboard.current[Key.F12].wasReleasedThisFrame)
		{
			Debug.LogWarning("[CrashReporter] Test crash triggered by user (Ctrl+Shift+F12)");
			throw new System.Exception("[CrashReporter TEST] Intentional test crash to verify dump collection.");
		}
#endif

		if ((Keyboard.current[Key.LeftCtrl].isPressed && Keyboard.current[Key.R].wasReleasedThisFrame) ||
			Keyboard.current[Key.F5].wasReleasedThisFrame)
		{
			_resetTriggered = true;
		}

		if (_resetTriggered && !_isResetting)
		{
			if (Keyboard.current[Key.LeftShift].isPressed)
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

		if (_teleportTriggered)
		{
			_teleportTriggered = false;
			StartCoroutine(DoTeleportModel());
		}

		if (_pendingModelInfoQuery != null)
		{
			var modelTransform = _worldRoot.transform.Find(_pendingModelInfoQuery);
			if (modelTransform != null)
			{
				_pendingModelInfoResult = new Pose(modelTransform.localPosition, modelTransform.localRotation);
			}
			_pendingModelInfoQuery = null;
			_modelInfoQueryEvent.Set();
		}
	}

	public bool TriggerModelInfoQuery(in string modelName, out Pose pose, int timeoutMs = 1000)
	{
		pose = Pose.identity;
		_modelInfoQueryEvent.Reset();
		_pendingModelInfoQuery = modelName;

		if (!_modelInfoQueryEvent.Wait(timeoutMs))
		{
			return false;
		}

		pose = _pendingModelInfoResult;
		return pose != Pose.identity;
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

	public void TriggerTeleportService(in TeleportOperation operation)
	{
		_pendingTeleportOperation = operation;
		_teleportTriggered = true;
	}

	void Reset()
	{
		ResetWorld();
	}

	private void ResetWorld()
	{
		ResetModel(_worldRoot);
	}

	private void ResetModel(GameObject targetObject)
	{
		var helpers = targetObject.GetComponentsInChildren<SDFormat.Helper.Base>();
		foreach (var helper in helpers)
		{
			helper.Reset();
		}

		// Also reset devices and plugins for this model
		var devices = targetObject.GetComponentsInChildren<Device>();
		foreach (var device in devices)
		{
			device.Reset();
		}

		var plugins = targetObject.GetComponentsInChildren<CLOiSimPlugin>();
		foreach (var plugin in plugins)
		{
			plugin.Reset();
		}
	}

	private IEnumerator ResetSimulation()
	{
		_isResetting = true;
		// Debug.LogWarning("Reset positions in simulation!!!");

		SensorRenderManager.Pause();

		_simulationWorld?.SignalReset();

		_transformGizmo?.ClearTargets();

		ResetWorld();

		Debug.LogWarning("[Done] Reset positions in simulation!!!");
		yield return new WaitForSeconds(0.1f);

		SensorRenderManager.Resume();

		_isResetting = false;
	}

	private IEnumerator DoTeleportModel()
	{
		var operation = _pendingTeleportOperation;

		try
		{
			// Switch to script mode for physics manipulation
			Physics.simulationMode = SimulationMode.Script;

			// Teleport all targets
			foreach (var target in operation.targets)
			{
				// Convert SDF pose (right-handed) to Unity pose (left-handed)
				var position = SDF2Unity.Position(target.pose.x, target.pose.y, target.pose.z);
				var quaternion = SDFormat.Math.Quaterniond.FromEuler(target.pose.roll, target.pose.pitch, target.pose.yaw);
				var rotation = SDF2Unity.ToUnity(quaternion);
				TeleportSingleModel(target.target, position, rotation, target.reset);
			}

			Physics.simulationMode = SimulationMode.FixedUpdate;
			_uiController?.SetInfoMessage($"Teleport operation completed");
		}
		catch (Exception ex)
		{
			Debug.LogError($"Teleport failed: {ex.Message}\n{ex.StackTrace}");
			_uiController?.SetInfoMessage($"Teleport operation failed");
			Physics.simulationMode = SimulationMode.FixedUpdate;
		}

		yield return null;

		// Handle world reset if requested (do this outside try-catch to allow yields)
		if (operation.worldReset)
		{
			_uiController?.SetInfoMessage($"Resetting world after teleporting models");
			yield return ResetSimulation();
		}
	}

	private void TeleportSingleModel(string modelName, Vector3 position, Quaternion rotation, bool doReset)
	{
		// Find the model in the scene
		var modelObject = _worldRoot.transform.Find(modelName)?.gameObject;
		if (modelObject == null)
		{
			Debug.LogError($"Teleport failed: Model '{modelName}' not found in scene");
			return;
		}

		// If restart is requested, reset the specific model
		if (doReset)
		{
			Debug.Log($"Resetting model '{modelName}' before teleport");
			ResetModel(modelObject);
		}

		Debug.Log($"Teleporting model '{modelName}' to position ({position})");

		// Try to teleport using ArticulationBody if available
		var articulationBody = modelObject.GetComponentInChildren<ArticulationBody>();
		if (articulationBody != null && articulationBody.isRoot)
		{
			articulationBody.Sleep();
			articulationBody.TeleportRoot(position, rotation);
		}
		else
		{
			// Fallback to direct transform manipulation
			modelObject.transform.position = position;
			modelObject.transform.rotation = rotation;
		}

		Physics.SyncTransforms();

		Debug.Log($"Teleport completed for model '{modelName}'");
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
		_loadingCursor?.Deactivate();

		_crashReporter?.Dispose();
		_crashReporter = null;

		SensorDevices.DepthCamera.UnloadComputeShader();

		if (BridgeManager != null)
		{
			BridgeManager.Dispose();
		}

		if (_simulationService != null)
		{
			_simulationService.Dispose();
		}

		if (AssimpLibrary.Instance.IsLibraryLoaded)
		{
			AssimpLibrary.Instance.FreeLibrary();
		}
	}
}
