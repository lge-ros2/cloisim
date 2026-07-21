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
using NetMQ;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Assimp.Unmanaged;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(30)]
public class Main : MonoBehaviour
{
	private static float DefaultOrthographicSize = 8;
	private const float PluginStartupTimeoutSeconds = 30f;

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
	// Hidden DontDestroyOnLoad root that holds quarantined articulated models —
	// see QuarantineArticulatedModel. Their ArticulationBody hierarchies are never
	// destroyed at runtime (that crashes PhysX); memory is reclaimed on full reload.
	private Transform _deferredCleanupRoot = null;
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

	public static void SuppressPhysicsDebugContacts(in string operationName)
	{
#if UNITY_EDITOR
		var physicsDebugWindows = Resources.FindObjectsOfTypeAll<PhysicsDebugWindow>();
		if (physicsDebugWindows == null || physicsDebugWindows.Length == 0)
		{
			return;
		}

		var contactFlagsWereEnabled = PhysicsVisualizationSettings.showContacts ||
			PhysicsVisualizationSettings.showAllContacts ||
			PhysicsVisualizationSettings.showContactImpulse ||
			PhysicsVisualizationSettings.showContactSeparation;

		PhysicsVisualizationSettings.showContacts = false;
		PhysicsVisualizationSettings.showAllContacts = false;
		PhysicsVisualizationSettings.showContactImpulse = false;
		PhysicsVisualizationSettings.showContactSeparation = false;

		foreach (var physicsDebugWindow in physicsDebugWindows)
		{
			physicsDebugWindow?.Close();
		}

		var contactMessage = contactFlagsWereEnabled ? " and disabled contact visualization" : string.Empty;
		Debug.LogWarning(
			$"[{nameof(Main)}] Closed Physics Debug Window{contactMessage} before {operationName} " +
			"to avoid a Unity editor crash in PhysicsDebugWindow.ReadContactsJob.");
#endif
	}

	private Transform GetDeferredCleanupRoot()
	{
		if (_deferredCleanupRoot != null)
		{
			return _deferredCleanupRoot;
		}

		var deferredCleanupObject = new GameObject("DeferredCleanupRoot")
		{
			hideFlags = HideFlags.HideAndDontSave
		};
		DontDestroyOnLoad(deferredCleanupObject);
		_deferredCleanupRoot = deferredCleanupObject.transform;
		return _deferredCleanupRoot;
	}

	private static bool HasRootArticulationBody(Transform target)
	{
		if (target == null)
		{
			return false;
		}

		var modelHelper = target.GetComponent<SDFormat.Helper.Model>();
		if (modelHelper != null)
		{
			return modelHelper.hasRootArticulationBody;
		}

		var articulationBody = target.GetComponentInChildren<ArticulationBody>();
		return articulationBody != null && articulationBody.isRoot;
	}

	private void SafeDestroyModelRootInternal(Transform target)
	{
		if (target == null)
		{
			return;
		}

		// Stop this subtree's sensor worker threads first so they stop reading
		// transforms/components that are about to be torn down (background-thread
		// race → native SIGSEGV). RequestStop is idempotent.
		StopDeviceWorkers(target);

		// Also request every plugin's threads to stop up front, in one pass. Without
		// this, each plugin's OnDestroy calls RequestStop() one at a time and then
		// blocks for its own ~100ms poll interval — for N plugins those waits stack
		// serially (up to N x 500ms worst case). Requesting them all together lets
		// the poll intervals overlap instead, so the total teardown wait stays close
		// to a single interval regardless of plugin count.
		StopPluginWorkers(target);

		if (!HasRootArticulationBody(target))
		{
			// Non-articulated models destroy cleanly.
			Destroy(target.gameObject);
			return;
		}

		// Articulated models are NOT destroyed. Destroying — or even deactivating —
		// an ArticulationBody hierarchy at runtime corrupts PhysX's internal
		// articulation cache: the first delete may succeed, but a later delete then
		// crashes in native PhysX teardown regardless of order. Whole-GameObject
		// Destroy, leaf-first per-component Destroy, and DestroyImmediate were all
		// confirmed to crash on the *second* delete in release builds (debug builds
		// only hide it via slower freed-memory reuse). So we quarantine instead.
		QuarantineArticulatedModel(target.gameObject);
	}

	// Tear down everything on an articulated model that is safe to destroy, then
	// hide and park the remaining ArticulationBody hierarchy without ever
	// destroying or deactivating it. Its memory is reclaimed by the next full scene
	// reload (Ctrl+Shift+R), which tears the whole scene down at once rather than
	// dismantling a live articulation piecemeal. See SafeDestroyModelRootInternal.
	private void QuarantineArticulatedModel(GameObject modelRoot)
	{
		if (modelRoot == null)
		{
			return;
		}

		Device.DrainReadbacksForTeardown();

		// Plugins first: CLOiSimPlugin.OnDestroy joins threads, disposes the NetMQ
		// transport, and deregisters its allocated ports (the "HashKey Removed"
		// log) — that is what frees the ports for a subsequent re-import. Plugins
		// hold no ArticulationBody, so destroying them is safe.
		foreach (var plugin in modelRoot.GetComponentsInChildren<CLOiSimPlugin>(true))
		{
			if (plugin != null)
			{
				Destroy(plugin);
			}
		}

		// Devices next: their OnDestroy releases sensor/URT/GPU resources.
		foreach (var device in modelRoot.GetComponentsInChildren<Device>(true))
		{
			if (device != null)
			{
				Destroy(device);
			}
		}

		// Make the quarantined model invisible and inert without touching the
		// ArticulationBody components (enabling/disabling a Renderer or Collider
		// does not perturb the PhysX articulation).
		foreach (var renderer in modelRoot.GetComponentsInChildren<Renderer>(true))
		{
			if (renderer != null)
			{
				renderer.enabled = false;
			}
		}
		foreach (var collider in modelRoot.GetComponentsInChildren<Collider>(true))
		{
			if (collider != null)
			{
				collider.enabled = false;
			}
		}

		// Pin the root so the now-invisible body cannot drift or fall.
		var rootBody = modelRoot.GetComponentInChildren<ArticulationBody>(true);
		if (rootBody != null && rootBody.isRoot)
		{
			rootBody.immovable = true;
		}

		// Rename so a re-import of the same model name does not collide, and park it
		// under the hidden DontDestroyOnLoad root so it is excluded from world save,
		// reset, and the following list.
		modelRoot.name = "(quarantined) " + modelRoot.name;
		modelRoot.transform.SetParent(GetDeferredCleanupRoot(), worldPositionStays: true);
	}

	private static void StopDeviceWorkers(Transform target)
	{
		if (target == null)
		{
			return;
		}

		var devices = target.GetComponentsInChildren<Device>(true);
		for (var i = 0; i < devices.Length; i++)
		{
			devices[i]?.RequestStop();
		}
	}

	private static void StopPluginWorkers(Transform target)
	{
		if (target == null)
		{
			return;
		}

		var plugins = target.GetComponentsInChildren<CLOiSimPlugin>(true);
		for (var i = 0; i < plugins.Length; i++)
		{
			plugins[i]?.RequestThreadStop();
		}
	}

	public static void SafeDestroyModelRoot(Transform target)
	{
		if (_instance == null || target == null)
		{
			return;
		}

		_instance.SafeDestroyModelRootInternal(target);
	}

	private IEnumerator CleanAllModels()
	{
		var worldRootTransform = _worldRoot.transform;
		var targets = new List<Transform>(worldRootTransform.childCount);
		for (var i = 0; i < worldRootTransform.childCount; i++)
		{
			targets.Add(worldRootTransform.GetChild(i));
		}

		foreach (var target in targets)
		{
			if (target == null || target.gameObject == null)
			{
				continue;
			}

			if (target.CompareTag("Model"))
			{
				SafeDestroyModelRoot(target);
			}
			else
			{
				Destroy(target.gameObject);
			}
		}

		// Let the plugin/device Destroy()s queued by quarantine flush (their
		// OnDestroy frees ports/threads) before the world is rebuilt on top.
		yield return null;
		yield return null;
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
		RenderSettings.sun = null;
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

	private void AbortBootstrap(in string errorMessage)
	{
		Debug.LogError(errorMessage);
		_uiController?.SetErrorMessage(errorMessage);
		enabled = false;
	}

	private static void AddMissingSceneRoot(List<string> missingSceneRoots, GameObject targetObject, in string objectName)
	{
		if (targetObject == null)
		{
			missingSceneRoots.Add(objectName);
		}
	}

	void Awake()
	{
		_instance = this;

		_crashReporter = new CrashReporter();

		// Background-thread watchdog: detects main-thread hard freezes (e.g. a
		// synchronous GPU stall in the URT pipeline) and logs the last breadcrumb
		// stage even when the main thread is fully blocked.
		gameObject.AddComponent<CLOiSim.Diagnostics.FreezeWatchdog>();

		ReplaceStandaloneInputModule();

		var logger = new DebugLogWriter();
		var loggerErr = new DebugLogWriter(true);
		Console.SetOut(logger);
		Console.SetError(loggerErr);

		GetResourcesPaths();

		// Load Library for Assimp
		var assimpLibraryPath = ResolveAssimpLibraryPath();
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

		// Debug.Log(QualitySettings.GetQualityLevel());
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

		// Keep shadow quality high enough for close-up robot inspection.
		QualitySettings.shadowDistance = 50f;

		var mainCamera = Camera.main;
		if (mainCamera == null)
		{
			AbortBootstrap("Failed to find the main camera.");
			return;
		}

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

		_cameraControl = mainCamera.gameObject.AddComponent<PerspectiveCameraControl>();

		_core = GameObject.Find("Core");
		_propsRoot = GameObject.Find("Props");
		_worldRoot = GameObject.Find("World");
		_lightsRoot = GameObject.Find("Lights");
		_roadsRoot = GameObject.Find("Roads");
		_uiRoot = GameObject.Find("UI");

		var missingSceneRoots = new List<string>();
		AddMissingSceneRoot(missingSceneRoots, _core, "Core");
		AddMissingSceneRoot(missingSceneRoots, _propsRoot, "Props");
		AddMissingSceneRoot(missingSceneRoots, _worldRoot, "World");
		AddMissingSceneRoot(missingSceneRoots, _lightsRoot, "Lights");
		AddMissingSceneRoot(missingSceneRoots, _roadsRoot, "Roads");
		AddMissingSceneRoot(missingSceneRoots, _uiRoot, "UI");

		if (missingSceneRoots.Count > 0)
		{
			AbortBootstrap($"Missing required scene roots: {string.Join(", ", missingSceneRoots)}");
			return;
		}

		_worldNavMeshBuilder = _worldRoot.GetComponent<WorldNavMeshBuilder>();

		_simulationWorld = _worldRoot.AddComponent<SimulationWorld>();
		DeviceHelper.SetGlobalClock(_simulationWorld.GetClock());

		if (_uiRoot != null)
		{
			_infoDisplay = _uiRoot.GetComponentInChildren<InfoDisplay>();
			_transformGizmo = _uiRoot.GetComponentInChildren<RuntimeGizmos.TransformGizmo>();
			_uiController = _uiRoot.GetComponent<UIController>();

			var uiMainCanvasTransform = _uiRoot.transform.Find("Main Canvas");
			if (uiMainCanvasTransform == null)
			{
				AbortBootstrap("Missing required scene object: UI/Main Canvas");
				return;
			}

			_uiMainCanvasRoot = uiMainCanvasTransform.gameObject;
			_followingList = _uiMainCanvasRoot.GetComponentInChildren<FollowingTargetList>();

			_uiRoot.AddComponent<ObjectInspectorWindow>();

			_loadingCursor = _uiRoot.AddComponent<LoadingCursor>();
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

		ResetRootModelsTransform();
	}

	private static string ResolveAssimpLibraryPath()
	{
#if UNITY_EDITOR
		var pluginsFolder = Path.Combine(Application.dataPath, "Plugins");
		var assimpDir = Directory.GetDirectories(pluginsFolder, "AssimpNetter.*").OrderByDescending(d => d).FirstOrDefault();

		if (assimpDir == null)
		{
			throw new Exception("AssimpNetter folder not found in Plugins");
		}

#if UNITY_EDITOR_LINUX
		return ResolveExistingPath(
			Path.Combine(assimpDir, "runtimes/linux-x64/native/libassimp.so"),
			Path.Combine(assimpDir, "runtimes/linux-x64/native/libassimp"));
#elif UNITY_EDITOR_OSX // TODO: need to be verified,
		return ResolveExistingPath(
			Path.Combine(assimpDir, "runtimes/osx-x64/native/libassimp.dylib"),
			Path.Combine(assimpDir, "runtimes/osx-x64/native/libassimp"));
#else // == UNITY_EDITOR_WIN
		return ResolveExistingPath(
			Path.Combine(assimpDir, "runtimes/win-x64/native/assimp.dll"),
			Path.Combine(assimpDir, "runtimes/win-x64/native/assimp"));
#endif
#else
#if UNITY_STANDALONE_WIN
		return ResolveExistingPath(
			"./CLOiSim_Data/Plugins/x86_64/assimp.dll",
			"./CLOiSim_Data/Plugins/x86_64/assimp");
#elif UNITY_STANDALONE_OSX // TODO: need to be verified,
		return ResolveExistingPath(
			"./Contents/PlugIns/libassimp.dylib",
			"./Contents/PlugIns/libassimp");
#else // == UNITY_STANDALONE_LINUX
		return ResolveExistingPath(
			"./CLOiSim_Data/Plugins/x86_64/libassimp.so",
			"./CLOiSim_Data/Plugins/libassimp.so",
			"./CLOiSim_Data/Plugins/x86_64/libassimp",
			"./CLOiSim_Data/Plugins/libassimp");
#endif
#endif
	}

	private static string ResolveExistingPath(params string[] candidates)
	{
		foreach (var candidate in candidates)
		{
			if (File.Exists(candidate))
			{
				return candidate;
			}
		}

		return candidates[0];
	}
	void Start()
	{
		if (!SystemInfo.supportsAsyncGPUReadback)
		{
			Debug.LogError("This API does not support AsyncGPURreadback.");
			_uiController?.SetErrorMessage("This API does not support AsyncGPURreadback.");
			return;
		}

		// Log GPU capabilities for diagnosing graphics-path crashes (e.g. the
		// Unified Ray Tracing backend used by lidar/depth camera sensors).
		Debug.Log($"[GPU] device='{SystemInfo.graphicsDeviceName}' type={SystemInfo.graphicsDeviceType} " +
			$"computeShaders={SystemInfo.supportsComputeShaders} " +
			$"rayTracing={SystemInfo.supportsRayTracing} rayTracingShaders={SystemInfo.supportsRayTracingShaders}");

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

	public bool TryGetModelResourcePath(in string modelName, out string path, out string filename)
	{
		if (_sdfRoot != null && _sdfRoot.ResourceModelTable.TryGetValue(modelName, out var entry))
		{
			path = entry.path;
			filename = entry.filename;
			return true;
		}

		path = string.Empty;
		filename = string.Empty;
		return false;
	}

	public IEnumerator LoadModel(string modelPath, string modelFileName, string modelNameOverride = null)
	{
		var loadStopwatch = System.Diagnostics.Stopwatch.StartNew();

		SuppressPhysicsDebugContacts("loading a model");

		_loadingCursor?.Activate();
		_uiController?.ShowLoadingOverlay("Loading model...", $"Preparing '{modelFileName}'");
		yield return null;

		if (_sdfRoot.DoParse(out var model, modelPath, modelFileName))
		{
			_uiController?.UpdateLoadingOverlay($"Importing '{model.Name}'");
			yield return null;

			_bridgeManager.ClearAllocatedHistory();

			// Debug.Log("Parsed: " + item.Key + ", " + item.Value.Item1 + ", " +  item.Value.Item2);
			// Use the caller-supplied base name (e.g. the copied object's current scene name)
			// when provided, so clones are named after what the user copied rather than the
			// SDF file's own <model name="..."> (which may differ from the scene instance name).
			var baseModelName = string.IsNullOrEmpty(modelNameOverride) ? model.Name : modelNameOverride;
			model.Name = GetClonedModelName(baseModelName);

			Physics.simulationMode = SimulationMode.Script;
			GameObject targetObject = null;
			CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
			try
			{
				yield return _sdfLoader.Start(model, onCreatedRoot: obj => targetObject = obj as GameObject);
			}
			finally
			{
				CLOiSim.Diagnostics.FreezeWatchdog.Restore();
			}

			yield return new WaitUntil(() => targetObject != null);

			var modelHelperForSource = targetObject.GetComponent<SDFormat.Helper.Model>();
			if (modelHelperForSource != null)
			{
				modelHelperForSource.sourcePath = modelPath;
				modelHelperForSource.sourceFilename = modelFileName;
			}

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

			var pluginStartupDeadline = Time.realtimeSinceStartup + PluginStartupTimeoutSeconds;
			while (!_pluginAllStarted)
			{
				if (HasPluginStartupTimedOut(pluginStartupDeadline, $"model '{model.Name}'"))
				{
					yield break;
				}

				yield return null;
			}
			_bridgeManager.PrintAllocatedHistory();

			loadStopwatch.Stop();
			var message = $"Model '{model.Name}' is successfully loaded. ({loadStopwatch.ElapsedMilliseconds}ms)";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);

			_loadingCursor?.Deactivate();
			_uiController?.HideLoadingOverlay();
		}
		else
		{
			_loadingCursor?.Deactivate();
			_uiController?.HideLoadingOverlay();
		}
	}

	private IEnumerator LoadWorld()
	{
		var loadStopwatch = System.Diagnostics.Stopwatch.StartNew();

		SuppressPhysicsDebugContacts("loading a world");

		Debug.Log("Target World: " + _worldFilename);
		_loadingCursor?.Activate();
		_uiController?.ShowLoadingOverlay("Loading world...", $"Preparing '{_worldFilename}'");
		yield return null;

		if (_sdfRoot.DoParse(out var world, out _loadedWorldFilePath, _worldFilename))
		{
			_uiController?.UpdateLoadingOverlay($"Importing '{_worldFilename}'");

			if (_clearAllOnStart)
			{
				yield return CleanAllModels();
				CleanAllLights();
				VHACD.ClearCache();
			}

			_sdfLoader = new SDFormat.Import.Loader();
			_sdfLoader.SetRootLights(_lightsRoot);

			Physics.simulationMode = SimulationMode.Script;
			// Suppress FreezeWatchdog during world loading: mesh import and SDF
			// parsing intentionally block the main thread and would otherwise fire
			// false-positive stall warnings.
			CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
			try
			{
				yield return _sdfLoader.Start(world);
			}
			finally
			{
				CLOiSim.Diagnostics.FreezeWatchdog.Restore();
			}

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

			var pluginStartupDeadline = Time.realtimeSinceStartup + PluginStartupTimeoutSeconds;
			while (!_pluginAllStarted)
			{
				if (HasPluginStartupTimedOut(pluginStartupDeadline, $"world '{_worldFilename}'"))
				{
					yield break;
				}

				yield return null;
			}
			_bridgeManager.PrintAllocatedHistory();

			TrackModel();

			loadStopwatch.Stop();
			var message = $"World '{_worldFilename}' is loaded ({loadStopwatch.ElapsedMilliseconds}ms)";
			Debug.Log(message);
			_uiController?.SetInfoMessage(message);

			_loadingCursor?.Deactivate();
			_uiController?.HideLoadingOverlay();
		}
		else
		{
			var errorMessage = $"Parsing failed!!! Failed to load world file: {_worldFilename}";
			Debug.LogError(errorMessage);
			_uiController?.SetErrorMessage(errorMessage);

			_loadingCursor?.Deactivate();
			_uiController?.HideLoadingOverlay();
		}

		if (!string.IsNullOrEmpty(_screenCaptureFilename))
		{
			var recording = ToggleRecord();
			_uiController?.OnRecordClicked(recording);
		}
	}

	private void OnPluginProgressChanged(int started, int total)
	{
		var message = $"Starting plugins... ({started}/{total})";
		_uiController?.SetInfoMessage(message);
		_uiController?.UpdateLoadingOverlay(message);
	}

	private bool HasPluginStartupTimedOut(in float deadline, in string targetDescription)
	{
		if (Time.realtimeSinceStartup < deadline)
		{
			return false;
		}

		var startedCount = _pluginStartTracker.StartedCount;
		var totalCount = _pluginStartTracker.TotalCount;
		_pluginStartTracker.Clear();

		var errorMessage = $"Timed out waiting for plugins while loading {targetDescription} ({startedCount}/{totalCount} started).";
		Debug.LogError(errorMessage);
		_uiController?.SetErrorMessage(errorMessage);
		_loadingCursor?.Deactivate();
		_uiController?.HideLoadingOverlay();
		return true;
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
				SuppressPhysicsDebugContacts("reloading the scene");
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
		SuppressPhysicsDebugContacts("resetting the simulation");

		Debug.LogWarning($"[Reset] Simulation reset triggered. elapsed={Time.realtimeSinceStartup:F3}s");
		_uiController?.SetWarningMessage("Resetting simulation...");
		_uiController?.ShowLoadingOverlay("Resetting simulation", "Reinitializing world and models...");
		yield return null; // let UI Toolkit flush the label before heavy sync work blocks the frame

		SensorRenderManager.Pause();

		// try/finally so rendering is ALWAYS resumed and the reset flag cleared,
		// even if a step below throws. Otherwise SensorRenderManager stays paused
		// (sensor feeds freeze permanently) and _isResetting stays true (every
		// future reset — Ctrl+R or the WebSocket service — is silently rejected).
		try
		{
			// Quiesce the GPU for sensor work before the scene is repositioned:
			// finish any in-flight AsyncGPUReadback (and thus the dispatches that feed
			// them) so nothing still references the URT acceleration structure.
			Device.DrainReadbacksForTeardown();

			_simulationWorld?.SignalReset();

			_transformGizmo?.ClearTargets();

			ResetWorld();

			// A reset repositions every model at once. Recreate the shared URT accel
			// structure from a clean slate so the post-resume rebuild does not operate
			// on stale instance handles.
			URTSensorManager.ResetScene();

			Debug.LogWarning("[Done] Reset positions in simulation!!!");
			_uiController?.SetInfoMessage("Simulation reset complete.");
			yield return new WaitForSeconds(0.1f);
		}
		finally
		{
			SensorRenderManager.Resume();
			_isResetting = false;
			_uiController?.HideLoadingOverlay();
		}
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

	void OnApplicationQuit()
	{
		// Application.quitting (subscribed in Device's static ctor) is not reliably
		// firing before OnDestroy on this Unity version/platform when the window is
		// closed via the window manager — Device.IsShuttingDown was still observed
		// false during Camera/Lidar.OnDestroy()'s GPU-quiescence probe on quit, which
		// defeats the "skip the fence-probe on quit" guard in
		// Device.DrainReadbacksForTeardown() and can lead to a false GPU-lost
		// diagnosis (and a SIGSEGV in whatever forces a free afterward). OnApplicationQuit
		// is the one teardown signal Unity sends unconditionally regardless of how quit
		// was initiated, so set the flag here directly instead of relying on the event.
		Device.SignalShuttingDown();

#if !UNITY_EDITOR
		// Environment.Exit(0) was tried here first and reliably hung: on Mono,
		// Environment.Exit() still runs an orderly managed-runtime shutdown internally,
		// which calls mono_thread_suspend_all_other_threads() to stop-the-world before
		// tearing down the GC heap. A gdb backtrace taken while hung showed the main
		// thread stuck inside exactly that call (pthread_kill on a target thread
		// returning ESRCH — the runtime's thread registry no longer matches reality),
		// independent of whether our own plugin threads were joined first. Skip Mono's
		// managed shutdown entirely: send this process a real SIGKILL, which the kernel
		// handles unconditionally and Mono cannot intercept, defer, or hang inside.
		// This also skips Unity's own scene-teardown cascade, which destroys every
		// GameObject (including CLOiD's live ArticulationBody hierarchy) as part of
		// normal quit — destroying a live articulation at runtime crashes PhysX natively
		// (SIGSEGV); see QuarantineArticulatedModel's comment. The process is exiting
		// anyway, so the OS reclaims GPU/PhysX/NetMQ native memory regardless; there is
		// nothing left for graceful teardown to protect.
		// Editor-excluded: killing the process here would kill the whole Editor, not
		// just this play session — so the Editor still goes through the graceful
		// thread-join + PerformFinalCleanup() path below.
		System.Diagnostics.Process.GetCurrentProcess().Kill();
#else
		// Unity does not guarantee OnDestroy() call order across independent
		// GameObjects during quit, so OnDestroy()'s NetMQConfig.Cleanup() below can
		// otherwise run while a CLOiSimPlugin's background thread (e.g.
		// PublishTfThread) is still sending on its own NetMQ socket, tearing down
		// the shared context out from under a live native call (SIGSEGV).
		// OnApplicationQuit runs on every object before any OnDestroy() during
		// quit, so stop every plugin thread here first. Joins can stack up to
		// 500ms per plugin across the whole scene, so suppress the freeze
		// watchdog for the duration like CLOiSimPlugin.OnDestroy does.
		CLOiSim.Diagnostics.FreezeWatchdog.Suppress();
		try
		{
			foreach (var plugin in FindObjectsByType<CLOiSimPlugin>())
			{
				plugin.StopThreadsForApplicationQuit();
			}
		}
		finally
		{
			CLOiSim.Diagnostics.FreezeWatchdog.Restore();
		}

		PerformFinalCleanup();
#endif
	}

	private bool _finalCleanupDone = false;

	// Shared by OnApplicationQuit (Editor path only; the standalone path skips this
	// and kills the process directly) and OnDestroy (the only path reached in the
	// Editor, where quit does not force-exit the process).
	private void PerformFinalCleanup()
	{
		if (_finalCleanupDone)
		{
			return;
		}
		_finalCleanupDone = true;

		if (_loadingCursor != null)
		{
			_loadingCursor.Deactivate();
		}

		_crashReporter?.Dispose();
		_crashReporter = null;

		SensorDevices.DepthCamera.UnloadComputeShader();

		if (_simulationService != null)
		{
			_simulationService.Dispose();
		}

		if (BridgeManager != null)
		{
			BridgeManager.Dispose();
		}

		NetMQConfig.Cleanup(false);

		if (AssimpLibrary.Instance.IsLibraryLoaded)
		{
			AssimpLibrary.Instance.FreeLibrary();
		}
	}

	void OnDestroy()
	{
		PerformFinalCleanup();
	}
}
