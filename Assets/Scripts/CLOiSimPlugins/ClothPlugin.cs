/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using CLOiSim.Cloth;
using SDFormat;
using UEPhysics = UnityEngine.Physics;

[DefaultExecutionOrder(100)]
public class ClothPlugin : CLOiSimPlugin
{
	[Header("ClothPlugin Settings")]
	[SerializeField]
	private UnityEngine.Mesh _clothMesh;

	[SerializeField]
	private BurstCloth _cloth;

	[SerializeField]
	private ColliderBridge _colliderBridge;

	[SerializeField]
	private Transform _clothRoot;

	[SerializeField]
	private Transform _meshTransform;

	[SerializeField]
	private float _searchMargin = 10f;

	[SerializeField]
	private float _colliderUpdateInterval = 1.0f;

	[SerializeField]
	private float _totalMass = 0f;

	private BoxCollider _clothCollider;
	private float _nextColliderUpdate;
	private float _nextSelectionColliderUpdate;
	private Transform _worldRoot;
	private Transform _propsRoot;
	private readonly HashSet<Transform> _currentColliderSet = new();
	private Vector3 _lastMeshWorldPosition;
	private Quaternion _lastMeshWorldRotation;
	private ArticulationBody _clothArticulationBody;
	private Rigidbody _clothRigidbody;
	private bool _wasGizmoSelected = false;
	private Vector3 _cachedSyncTarget;  // computed each LateUpdate, used by SyncRootToClothCentroid

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.NONE;
	}

	protected override IEnumerator OnStart()
	{
		var pluginParams = GetPluginParameters();
		if (pluginParams == null)
		{
			Debug.LogError("ClothPlugin: Plugin parameters not found");
			yield break;
		}

		var target = pluginParams.GetValue<string>("target");
		_clothRoot = ResolveTargetTransform(target);
		if (_clothRoot == null)
		{
			Debug.LogError($"ClothPlugin: Cannot resolve target link '{target}'");
			yield break;
		}

		_cloth = transform.gameObject.AddComponent<BurstCloth>();

		if (!InitializeClothMesh(target))
			yield break;

		yield return WaitForModelDeployment();
		yield return null;

		_worldRoot = Main.WorldRoot.transform;
		_propsRoot = Main.PropsRoot.transform;
		ApplyColliderDiscoveryParameters(pluginParams);

		DisableModelPhysics();
		ConfigureClothFromMesh(pluginParams);
		InitializeColliderBridge();

		StartSummary.AppendLine($"Cloth initialized from mesh '{_clothMesh.name}': " +
			$"{_clothMesh.vertexCount} vertices, {_currentColliderSet.Count} scene colliders");

		yield return null;
	}

	private bool InitializeClothMesh(string target)
	{
		var meshFilter = _clothRoot.GetComponentInChildren<MeshFilter>();
		if (meshFilter == null || meshFilter.sharedMesh == null)
		{
			Debug.LogError($"ClothPlugin: No mesh found under target link '{target}'");
			return false;
		}

		_clothMesh = Instantiate(meshFilter.sharedMesh);
		_clothMesh.name = meshFilter.sharedMesh.name + "_cloth";
		_clothMesh.MarkDynamic();
		meshFilter.mesh = _clothMesh;
		_meshTransform = meshFilter.transform;

		// Trigger BoxCollider on the visual GO so gizmo raycast can select via
		// GetComponentInParent<SDFormat.Helper.Link>()
		// Using BoxCollider instead of convex MeshCollider because flat/coplanar
		// cloth meshes cause PhysX QuickHull failures.
		var collider = meshFilter.gameObject.AddComponent<BoxCollider>();
		collider.isTrigger = true;
		var bounds = _clothMesh.bounds;
		collider.center = bounds.center;
		collider.size = bounds.size;
		_clothCollider = collider;

		var rootModel = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
		if (rootModel != null &&!rootModel.hasRootArticulationBody)
		{
			rootModel.gameObject.layer = LayerMask.NameToLayer("Cloth");
		}

		return true;
	}

	private WaitUntil WaitForModelDeployment()
	{
		return new WaitUntil(() =>
		{
			var modelHelper = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
			if (modelHelper == null) return true;
			var rootModel = modelHelper.RootModel ?? modelHelper;
			var artBody = rootModel.GetComponentInChildren<ArticulationBody>();
			return artBody == null || !artBody.immovable;
		});
	}

	private void DisableModelPhysics()
	{
		var modelHelper = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
		if (modelHelper == null) return;

		var root = modelHelper.RootModel ?? modelHelper;
		_clothArticulationBody = root.GetComponentInChildren<ArticulationBody>();
		if (_clothArticulationBody != null)
		{
			_clothArticulationBody.immovable = true;
			_clothArticulationBody.useGravity = false;
		}
		else
		{
			_clothRigidbody = root.GetComponentInChildren<Rigidbody>();
			if (_clothRigidbody != null)
			{
				_clothRigidbody.isKinematic = true;
				_clothRigidbody.useGravity = false;
			}
		}
	}

	private void InitializeColliderBridge()
	{
		_colliderBridge = transform.gameObject.AddComponent<ColliderBridge>();
		var sceneColliders = DiscoverSceneColliders();
		_colliderBridge.Initialize(_cloth, sceneColliders);
		_currentColliderSet.UnionWith(sceneColliders);
		_nextColliderUpdate = Time.time + _colliderUpdateInterval;
		_lastMeshWorldPosition = _meshTransform.position;
		_lastMeshWorldRotation = _meshTransform.rotation;
	}

	private void ApplyColliderDiscoveryParameters(Plugin plugin)
	{
		_searchMargin = plugin.GetValue("cloth/collider/search_margin", _searchMargin);
		_colliderUpdateInterval = plugin.GetValue("cloth/collider/update_interval", _colliderUpdateInterval);
	}

	protected override void OnReset()
	{
		_cloth?.ResetToInitialState();
	}

	private bool _wasTransforming = false;

	private void Update()
	{
		if (_cloth == null || _meshTransform == null) return;

		var gizmo = Main.Gizmos;
		var isSelected = gizmo != null && IsGizmoTargetingSelf(gizmo);
		var isTransforming = isSelected && gizmo.isTransforming;

		// On selection start (click): sync root body to cached cloth centroid (computed last LateUpdate)
		if (!_wasGizmoSelected && isSelected)
			SyncRootToClothCentroid();
		_wasGizmoSelected = isSelected;

		// On transform end: deselect so gizmo disappears
		if (_wasTransforming && !isTransforming && isSelected)
			gizmo.ClearTargets();
		_wasTransforming = isTransforming;

		// Pause simulation while selected (prevents jitter after SyncRootToClothCentroid
		// moves the root and wakes the cloth via Translate).
		// Cloth resumes when ClearTargets() is called on transform end.
		_cloth.Paused = isSelected;

		// Detect if the parent transform was moved/rotated (e.g. by gizmo) and keep the
		// cloth particles aligned with that rigid transform while simulation is paused.
		var currentPos = _meshTransform.position;
		var currentRotation = _meshTransform.rotation;
		var delta = currentPos - _lastMeshWorldPosition;
		var positionChanged = delta.sqrMagnitude > 1e-8f;
		var rotationChanged = Quaternion.Angle(_lastMeshWorldRotation, currentRotation) > 1e-4f;
		if (positionChanged)
		{
			_cloth.Translate(delta);
		}

		if (rotationChanged)
		{
			var deltaRotation = currentRotation * Quaternion.Inverse(_lastMeshWorldRotation);
			_cloth.RotateAround(
				(float3)currentPos,
				new quaternion(deltaRotation.x, deltaRotation.y, deltaRotation.z, deltaRotation.w));
		}

		if (positionChanged || rotationChanged)
		{
			_lastMeshWorldPosition = currentPos;
			_lastMeshWorldRotation = currentRotation;
		}

		// Re-enforce immovable after gizmo releases (gizmo sets immovable=false on release)
		if (_clothArticulationBody != null && !_clothArticulationBody.immovable)
			_clothArticulationBody.immovable = true;
		else if (_clothRigidbody != null && !_clothRigidbody.isKinematic)
			_clothRigidbody.isKinematic = true;
	}

	private void CacheClothSyncTarget(NativeArray<float3> positions)
	{
		if (!positions.IsCreated || positions.Length == 0) return;

		// X, Z: centroid of all particles; Y: highest particle + initial offset
		var centroid = Vector3.zero;
		var maxY = float.MinValue;
		for (var i = 0; i < positions.Length; i++)
		{
			var p = (Vector3)positions[i];
			centroid += p;
			if (p.y > maxY) maxY = p.y;
		}
		centroid /= positions.Length;
		_cachedSyncTarget = new Vector3(centroid.x, maxY, centroid.z);
	}

	private void SyncRootToClothCentroid()
	{
		var targetPos = _cachedSyncTarget;

		// Get current root position before moving
		var currentRootPos = Vector3.zero;
		if (_clothArticulationBody != null)
			currentRootPos = _clothArticulationBody.transform.position;
		else if (_clothRigidbody != null)
			currentRootPos = _clothRigidbody.transform.position;
		else
			currentRootPos = _meshTransform.root.position;

		// Calculate delta
		var rootDelta = targetPos - currentRootPos;

		// Move root to sync point
		if (_clothArticulationBody != null)
		{
			_clothArticulationBody.TeleportRoot(targetPos, _clothArticulationBody.transform.rotation);
		}
		else if (_clothRigidbody != null)
		{
			_clothRigidbody.position = targetPos;
		}
		else
		{
			_meshTransform.root.position = targetPos;
		}

		// Keep cloth particles at same world position (don't move them when root moves)
		_cloth.Translate(-rootDelta);

		UEPhysics.SyncTransforms();

		_lastMeshWorldPosition = _meshTransform.position;
		_lastMeshWorldRotation = _meshTransform.rotation;
	}

	private bool IsGizmoTargetingSelf(RuntimeGizmos.TransformGizmo gizmo)
	{
		gizmo.GetSelectedTargets(out var targets);
		if (targets == null) return false;

		for (var i = 0; i < targets.Count; i++)
		{
			if (targets[i] == null) continue;
			if (_meshTransform == null) return false;
			if (_meshTransform.IsChildOf(targets[i]))
				return true;
		}
		return false;
	}

	private void LateUpdate()
	{
		if (_cloth == null || _clothMesh == null || _meshTransform == null) return;

		var positions = _cloth.GetPositions();
		if (!positions.IsCreated || positions.Length != _clothMesh.vertexCount) return;

		// Cache sync target here — after BurstCloth.LateUpdate() has run fresh simulation
		CacheClothSyncTarget(positions);

		UpdateClothMesh(positions);
		UpdateSelectionCollider();

		if (_colliderBridge != null && _clothRoot != null && Time.time >= _nextColliderUpdate)
		{
			_nextColliderUpdate = Time.time + _colliderUpdateInterval;
			RefreshSceneColliders();
		}
	}

	private void UpdateClothMesh(NativeArray<float3> positions)
	{
		var localPositions = new NativeArray<Vector3>(positions.Length, Allocator.Temp);
		var worldToLocal = _meshTransform.worldToLocalMatrix;
		for (var i = 0; i < positions.Length; i++)
			localPositions[i] = worldToLocal.MultiplyPoint3x4((Vector3)positions[i]);

		_clothMesh.SetVertices(localPositions);
		_clothMesh.RecalculateNormals();
		_clothMesh.RecalculateBounds();
		localPositions.Dispose();
	}

	private void UpdateSelectionCollider()
	{
		if (_clothCollider == null || Time.time < _nextSelectionColliderUpdate) return;

		_nextSelectionColliderUpdate = Time.time + 0.5f;
		var bounds = _clothMesh.bounds;
		_clothCollider.center = bounds.center;
		_clothCollider.size = bounds.size;
	}


	private static Transform FindInHierarchy(Transform root, string name)
	{
		if (root.name == name)
			return root;

		foreach (Transform child in root)
		{
			var result = FindInHierarchy(child, name);
			if (result != null) return result;
		}
		return null;
	}

	private Transform ResolveTargetTransform(string target)
	{
		if (string.IsNullOrEmpty(target))
			return null;
		(_, var linkName) = SDF2Unity.GetModelLinkName(target);
		return FindInHierarchy(transform, linkName);
	}

	private Transform[] DiscoverSceneColliders()
	{
		var modelHelper = _clothRoot.GetComponentInParent<SDFormat.Helper.Model>();
		var modelTransform = modelHelper != null ? modelHelper.transform : _clothRoot;

		// Use mesh bounds expanded by a margin to find nearby colliders
		var meshBounds = _clothMesh.bounds;
		var worldCenter = _meshTransform.TransformPoint(meshBounds.center);
		var worldExtents = Vector3.Scale(meshBounds.extents, _meshTransform.lossyScale);
		worldExtents += Vector3.one * _searchMargin; // Search margin

		var overlapping = UEPhysics.OverlapBox(worldCenter, worldExtents, _meshTransform.rotation);
		var colliderTransforms = new List<Transform>();

		foreach (var col in overlapping)
		{
			if (!ShouldIncludeCollider(col, modelTransform)) continue;
			if (!colliderTransforms.Contains(col.transform))
				colliderTransforms.Add(col.transform);
		}

		return colliderTransforms.ToArray();
	}

	private bool ShouldIncludeCollider(Collider collider, Transform modelTransform)
	{
		if (collider == null || collider.isTrigger)
			return false;

		var colliderTransform = collider.transform;
		if (colliderTransform.IsChildOf(modelTransform))
			return false;

		if (!colliderTransform.IsChildOf(_worldRoot) && !colliderTransform.IsChildOf(_propsRoot))
			return false;

		return true;
	}

	private void RefreshSceneColliders()
	{
		var newColliders = DiscoverSceneColliders();
		var newSet = new HashSet<Transform>(newColliders);

		if (!newSet.SetEquals(_currentColliderSet))
		{
#if UNITY_EDITOR
			Debug.Log($"ClothPlugin: Colliders updated — {newColliders.Length} scene colliders near cloth");
#endif
			_currentColliderSet.Clear();
			_currentColliderSet.UnionWith(newColliders);
			_colliderBridge.UpdateSceneColliders(newColliders);
		}
	}

	private void ConfigureClothFromMesh(Plugin plugin)
	{
		var worldVerts = ConvertVerticesToWorldSpace();
		var masses = ComputeVertexMasses(plugin, worldVerts.Length);
		ApplyAnchorPins(plugin, worldVerts, masses);

		var weldConstraints = BuildWeldConstraints(worldVerts, masses, out var weldCount);
		BuildEdgeAndBendingConstraints(plugin, worldVerts, _clothMesh.triangles, weldConstraints,
			out var constraints, out var bendingConstraints);

		ApplyClothParameters(plugin);
		_cloth.Initialize(worldVerts, masses, constraints, bendingConstraints);

		StartSummary.AppendLine(
			$"Constraints: {constraints.Length} stretch ({weldCount} weld) + {bendingConstraints.Length} bending, " +
			$"stiffness={plugin.GetValue("cloth/constraints/stretching_stiffness", 1.0f)}/" +
			$"{plugin.GetValue("cloth/constraints/bending_stiffness", 0.8f)}, " +
			$"damping={_cloth.Damping}, iterations={_cloth.SolverIterations}");
	}

	private float3[] ConvertVerticesToWorldSpace()
	{
		var srcVertices = _clothMesh.vertices;
		var worldVerts = new float3[srcVertices.Length];
		for (var i = 0; i < srcVertices.Length; i++)
			worldVerts[i] = (float3)_meshTransform.TransformPoint(srcVertices[i]);
		return worldVerts;
	}

	private float[] ComputeVertexMasses(Plugin plugin, int vertexCount)
	{
		if (_clothArticulationBody != null)
			_totalMass = _clothArticulationBody.mass;
		else if (_clothRigidbody != null)
			_totalMass = _clothRigidbody.mass;

		if (_totalMass <= 0f)
			_totalMass = plugin.GetValue("cloth/simulation/mass", 1.0f);

		var perVertexMass = _totalMass / vertexCount;
		var masses = new float[vertexCount];
		for (var i = 0; i < masses.Length; i++) masses[i] = perVertexMass;
		return masses;
	}

	private void ApplyAnchorPins(Plugin plugin, float3[] worldVerts, float[] masses)
	{
		if (!plugin.HasElement("cloth/anchor")) return;

		var axisStr = plugin.GetValue("cloth/anchor/axis", "y").ToLower();
		var sideStr = plugin.GetValue("cloth/anchor/side", "max").ToLower();
		var threshold = plugin.GetValue("cloth/anchor/threshold", 0.01f);

		var axisIndex = axisStr switch { "x" => 0, "y" => 1, "z" => 2, _ => 1 };
		var findMax = sideStr == "max";

		var extremeValue = findMax ? float.MinValue : float.MaxValue;
		foreach (var v in worldVerts)
		{
			var val = v[axisIndex];
			extremeValue = findMax ? math.max(extremeValue, val) : math.min(extremeValue, val);
		}

		var pinnedCount = 0;
		for (var i = 0; i < worldVerts.Length; i++)
		{
			if (math.abs(worldVerts[i][axisIndex] - extremeValue) <= threshold)
			{
				masses[i] = 0f;
				pinnedCount++;
			}
		}

		StartSummary.AppendLine($"Anchor: axis={axisStr}({axisIndex}), side={sideStr}, " +
			$"threshold={threshold}, pinned {pinnedCount}/{worldVerts.Length} vertices");
	}

	private static List<DistanceConstraint> BuildWeldConstraints(float3[] worldVerts, float[] masses, out int weldCount)
	{
		weldCount = 0;
		var weldThreshold = 1e-6f;
		var weldConstraints = new List<DistanceConstraint>();
		var bucketSize = 0.001f;
		var buckets = new Dictionary<long, List<int>>();

		for (var i = 0; i < worldVerts.Length; i++)
		{
			var bx = (long)math.floor(worldVerts[i].x / bucketSize);
			var by = (long)math.floor(worldVerts[i].y / bucketSize);
			var bz = (long)math.floor(worldVerts[i].z / bucketSize);
			var bucketKey = bx * 73856093L ^ by * 19349663L ^ bz * 83492791L;

			if (!buckets.TryGetValue(bucketKey, out var bucket))
			{
				bucket = new List<int>();
				buckets[bucketKey] = bucket;
			}

			foreach (var j in bucket)
			{
				if (math.lengthsq(worldVerts[i] - worldVerts[j]) < weldThreshold)
				{
					weldConstraints.Add(new DistanceConstraint
					{
						IndexA = math.min(i, j),
						IndexB = math.max(i, j),
						RestLength = 0f,
						Stiffness = 1.0f
					});
					if (masses[i] == 0f && masses[j] != 0f) masses[j] = 0f;
					else if (masses[j] == 0f && masses[i] != 0f) masses[i] = 0f;
					weldCount++;
				}
			}
			bucket.Add(i);
		}

		return weldConstraints;
	}

	private static void BuildEdgeAndBendingConstraints(
		Plugin plugin, float3[] worldVerts, int[] triangles,
		List<DistanceConstraint> weldConstraints,
		out DistanceConstraint[] outConstraints, out BendingConstraint[] outBendingConstraints)
	{
		var stiffness = plugin.GetValue("cloth/constraints/stretching_stiffness", 1.0f);
		var bendingStiffness = plugin.GetValue("cloth/constraints/bending_stiffness", 0.8f);
		var edgeSet = new HashSet<long>();
		var constraints = new List<DistanceConstraint>(weldConstraints);
		var edgeToTriOpposite = new Dictionary<long, int>();
		var bendingConstraints = new List<BendingConstraint>();

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var i0 = triangles[i];
			var i1 = triangles[i + 1];
			var i2 = triangles[i + 2];

			TryAddEdge(edgeSet, constraints, worldVerts, i0, i1, stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, i1, i2, stiffness);
			TryAddEdge(edgeSet, constraints, worldVerts, i0, i2, stiffness);

			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i0, i1, i2, bendingStiffness);
			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i1, i2, i0, bendingStiffness);
			TryAddBending(edgeToTriOpposite, bendingConstraints, worldVerts, i0, i2, i1, bendingStiffness);
		}

		outConstraints = constraints.ToArray();
		outBendingConstraints = bendingConstraints.ToArray();
	}

	private void ApplyClothParameters(Plugin plugin)
	{
		_cloth.SolverIterations = plugin.GetValue("cloth/solver/iterations", 10);
		_cloth.Damping = plugin.GetValue("cloth/simulation/damping", 0.9f);
		_cloth.VelocityDecay = plugin.GetValue("cloth/simulation/velocity_decay", _cloth.VelocityDecay);
		_cloth.Gravity = new float3(
			plugin.GetValue("cloth/simulation/gravity/x", 0f),
			plugin.GetValue("cloth/simulation/gravity/y", -9.81f),
			plugin.GetValue("cloth/simulation/gravity/z", 0f));
		_cloth.ParticleRadius = plugin.GetValue("cloth/collider/particle_radius", 0.01f);
		_cloth.CollisionSurfaceOffset = plugin.GetValue("cloth/collider/surface_offset", _cloth.CollisionSurfaceOffset);
		_cloth.SubSteps = plugin.GetValue("cloth/solver/sub_steps", 4);
		_cloth.Friction = plugin.GetValue("cloth/simulation/friction", 0.8f);
		var configuredSleepThreshold = plugin.GetValue("cloth/simulation/sleep_threshold", 0.05f);
		// BurstCloth derives a per-step velocity snap threshold from SleepThreshold.
		// Large SDF values such as 0.05-0.1 stop ordinary cloth drift instead of only killing jitter.
		_cloth.SleepThreshold = Mathf.Clamp(configuredSleepThreshold, 0.0001f, 0.01f);
	}

	private static void TryAddEdge(
		HashSet<long> edgeSet, List<DistanceConstraint> constraints,
		float3[] vertices, int a, int b, float stiffness)
	{
		var lo = math.min(a, b);
		var hi = math.max(a, b);
		var key = (long)lo << 32 | (uint)hi;
		if (!edgeSet.Add(key))
			return;

		constraints.Add(new DistanceConstraint
		{
			IndexA = lo,
			IndexB = hi,
			RestLength = math.length(vertices[hi] - vertices[lo]),
			Stiffness = stiffness
		});
	}

	/// <summary>
	/// For edge (edgeA, edgeB) with opposite vertex 'opposite' in this triangle,
	/// if we've already seen this edge from another triangle, create a bending constraint
	/// between the two opposite vertices.
	/// </summary>
	private static void TryAddBending(
		Dictionary<long, int> edgeToOpposite, List<BendingConstraint> bendingConstraints,
		float3[] vertices, int edgeA, int edgeB, int opposite, float stiffness)
	{
		var lo = math.min(edgeA, edgeB);
		var hi = math.max(edgeA, edgeB);
		var key = (long)lo << 32 | (uint)hi;

		if (edgeToOpposite.TryGetValue(key, out var otherOpposite))
		{
			// Second triangle sharing this edge — create bending constraint
			bendingConstraints.Add(new BendingConstraint
			{
				IndexA = otherOpposite,
				IndexB = opposite,
				RestLength = math.length(vertices[opposite] - vertices[otherOpposite]),
				Stiffness = stiffness
			});
		}
		else
		{
			// First triangle with this edge — store opposite vertex
			edgeToOpposite[key] = opposite;
		}
	}
}
