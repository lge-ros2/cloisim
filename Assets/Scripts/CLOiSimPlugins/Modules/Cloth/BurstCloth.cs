/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CLOiSim.Cloth
{
	public enum ColliderType
	{
		Sphere, Box, Capsule, Mesh, Plane
	}

	public struct ClothCollider
	{
		public ColliderType Type;
		public float3 Position;
		public quaternion Rotation;
		public float3 Scale; // Radius for Sphere (x), Half-extents for Box

		// Mesh specific properties
		public int TriStart;
		public int TriCount;
		public float3 BoundsMin;
		public float3 BoundsMax;
	}

	public struct DistanceConstraint
	{
		public int IndexA;
		public int IndexB;
		public float RestLength;
		public float Stiffness;
	}

	public struct BendingConstraint
	{
		public int IndexA;  // opposite vertex of triangle 1
		public int IndexB;  // opposite vertex of triangle 2
		public float RestLength;  // rest distance between the two opposite vertices
		public float Stiffness;
	}

	/// <summary>
	/// High-performance cloth simulator utilizing Position-Based Dynamics (PBD) and Burst.
	/// </summary>
	public class BurstCloth : MonoBehaviour
	{
		[Header("Simulation Settings")]
		public int SolverIterations = 5;
		public int SubSteps = 4;
		public float3 Gravity = new float3(0, -9.81f, 0);
		public float Damping = 0.98f;
		public float Friction = 0.5f;
		public float SleepThreshold = 0.001f;
		public float VelocityDecay = 10f;
		public float CollisionSurfaceOffset = 0.001f;

		/// <summary>
		/// When true, the simulation is paused (e.g. during gizmo manipulation).
		/// </summary>
		[NonSerialized]
		public bool Paused = false;
		public float ParticleRadius = 0.01f;

		[Header("Editor Visualization")]
		public bool DrawVertices = true;
		public bool DrawPinnedVertices = true;
		public bool DrawConstraints = false;
		public bool DrawVertexLabels = false;
		public float VertexSize = 0.006f;
		public Color PinnedVertexColor = Color.red;
		public Color FreeVertexColor = Color.cyan;
		public Color GrabbedVertexColor = Color.blue;
		public Color ConstraintColor = new Color(0.2f, 1f, 0.2f, 0.5f);

		// Native collections for Burst Jobs
		private NativeArray<float3> _positions;
		private NativeArray<float3> _predictedPositions;
		private NativeArray<float3> _velocities;
		private NativeArray<float> _inverseMasses;
		private NativeArray<DistanceConstraint> _constraints;
		private NativeArray<BendingConstraint> _bendingConstraints;
		private NativeArray<ClothCollider> _colliders;
		private NativeArray<float3> _meshVertices;
		private NativeArray<int> _meshTriangles;
		private NativeArray<float3> _initialPositions;
		private NativeArray<float3> _preCollisionPositions;
		private NativeArray<float3> _contactNormals;
		private NativeArray<float3> _currentContactNormals;
		private NativeArray<float> _originalInverseMasses;

		private JobHandle _clothJobHandle;
		private int _lastJobCompletedFrame = -1;
		private bool _isInitialized = false;

		public bool IsInitialized => _isInitialized;
		public int VertexCount => _isInitialized ? _positions.Length : 0;
		public bool IsVertexPinned(int index) => _isInitialized && index >= 0 && index < _inverseMasses.Length && _originalInverseMasses[index] == 0f;
		private int _colliderCount = 0;
		private bool _hasMeshColliders = false;
		private bool _isSleeping = false;
		private int _sleepCounter = 0;
		private const int SleepFramesRequired = 10;
		private const int MeshCollisionIterationStride = 2;
		private const float RestingContactNormalThresholdSq = 0.01f;

		// Grab system
		private readonly HashSet<int> _grabbedVertices = new();
		private static readonly List<BurstCloth> _instances = new();
		public static IReadOnlyList<BurstCloth> Instances => _instances;

		private void OnEnable() => _instances.Add(this);
		private void OnDisable() => _instances.Remove(this);

		// Call this to setup the cloth topology
		public void Initialize(float3[] vertices, float[] masses, DistanceConstraint[] structuralConstraints, BendingConstraint[] bendingConstraints = null)
		{
			int count = vertices.Length;
			_positions = new(count, Allocator.Persistent);
			_predictedPositions = new(count, Allocator.Persistent);
			_velocities = new(count, Allocator.Persistent);
			_inverseMasses = new(count, Allocator.Persistent);
			_initialPositions = new(count, Allocator.Persistent);
			_preCollisionPositions = new(count, Allocator.Persistent);
			_contactNormals = new(count, Allocator.Persistent);
			_currentContactNormals = new(count, Allocator.Persistent);
			_constraints = new(structuralConstraints.Length, Allocator.Persistent);
			_bendingConstraints = new(bendingConstraints != null ? bendingConstraints.Length : 0, Allocator.Persistent);

			// Allow up to 10 max colliders initially (can be resized dynamically)
			_colliders = new(10, Allocator.Persistent);

			// Pre-allocate dummy mesh arrays to prevent Burst Job errors when no MeshColliders are used
			_meshVertices = new(1, Allocator.Persistent);
			_meshTriangles = new(3, Allocator.Persistent);

			for (var i = 0; i < count; i++)
			{
				_positions[i] = vertices[i];
				_predictedPositions[i] = vertices[i];
				_initialPositions[i] = vertices[i];
				_velocities[i] = float3.zero;
				_contactNormals[i] = float3.zero;
				_currentContactNormals[i] = float3.zero;
				_inverseMasses[i] = masses[i] > 0f ? 1f / masses[i] : 0f; // 0 inverse mass = pinned/kinematic
			}

			_originalInverseMasses = new(count, Allocator.Persistent);
			NativeArray<float>.Copy(_inverseMasses, _originalInverseMasses);

			_constraints.CopyFrom(structuralConstraints);
			if (bendingConstraints != null && bendingConstraints.Length > 0)
				_bendingConstraints.CopyFrom(bendingConstraints);
			_isInitialized = true;
		}

		/// <summary>
		/// Gets the current positions of the cloth vertices.
		/// </summary>
		public NativeArray<float3> GetPositions() => _positions;

		public void ResetToInitialState()
		{
			if (!_isInitialized) return;

			_clothJobHandle.Complete();
			ReleaseAllGrabs();

			for (var i = 0; i < _positions.Length; i++)
			{
				_positions[i] = _initialPositions[i];
				_predictedPositions[i] = _initialPositions[i];
				_velocities[i] = float3.zero;
				_contactNormals[i] = float3.zero;
				_currentContactNormals[i] = float3.zero;
			}
			_isSleeping = false;
			_sleepCounter = 0;
		}

		/// <summary>
		/// Translates all cloth positions (current, predicted, and initial) by a world-space delta.
		/// Use when the parent transform is moved externally (e.g. by a gizmo).
		/// </summary>
		public void Translate(float3 delta)
		{
			if (!_isInitialized) return;

			_clothJobHandle.Complete();

			for (var i = 0; i < _positions.Length; i++)
			{
				_positions[i] += delta;
				_predictedPositions[i] += delta;
				_initialPositions[i] += delta;
				_velocities[i] = float3.zero;
				_contactNormals[i] = float3.zero;
				_currentContactNormals[i] = float3.zero;
			}
			_isSleeping = false;
			_sleepCounter = 0;
		}

		/// <summary>
		/// Rotates all cloth positions around a world-space pivot.
		/// Use when the parent transform is rotated externally (e.g. by a gizmo).
		/// </summary>
		public void RotateAround(float3 pivot, quaternion deltaRotation)
		{
			if (!_isInitialized) return;

			_clothJobHandle.Complete();

			for (var i = 0; i < _positions.Length; i++)
			{
				_positions[i] = pivot + math.mul(deltaRotation, _positions[i] - pivot);
				_predictedPositions[i] = pivot + math.mul(deltaRotation, _predictedPositions[i] - pivot);
				_initialPositions[i] = pivot + math.mul(deltaRotation, _initialPositions[i] - pivot);
				_velocities[i] = float3.zero;
				_contactNormals[i] = float3.zero;
				_currentContactNormals[i] = float3.zero;
			}
			_isSleeping = false;
			_sleepCounter = 0;
		}

		#region Grab API

		/// <summary>
		/// Completes the cloth job handle only if it hasn't been completed this frame.
		/// </summary>
		private void EnsureJobCompleted()
		{
			var currentFrame = Time.frameCount;
			if (_lastJobCompletedFrame != currentFrame)
			{
				_clothJobHandle.Complete();
				_lastJobCompletedFrame = currentFrame;
			}
		}

		/// <summary>
		/// Finds the nearest non-grabbed, non-pinned vertex within maxRadius.
		/// Returns -1 if none found.
		/// </summary>
		public int FindNearestVertex(float3 worldPosition, float maxRadius)
		{
			if (!_isInitialized) return -1;

			EnsureJobCompleted();

			var bestIndex = -1;
			var bestDistSq = maxRadius * maxRadius;
			for (var i = 0; i < _positions.Length; i++)
			{
				// Skip already-grabbed or originally-pinned vertices
				if (_grabbedVertices.Contains(i)) continue;
				if (_originalInverseMasses[i] == 0f) continue;

				var distSq = math.lengthsq(_positions[i] - worldPosition);
				if (distSq < bestDistSq)
				{
					bestDistSq = distSq;
					bestIndex = i;
				}
			}
			return bestIndex;
		}

		/// <summary>
		/// Grabs a vertex: pins it to the given world position.
		/// Returns false if the vertex is already grabbed or originally pinned.
		/// </summary>
		public bool GrabVertex(int index, float3 position)
		{
			if (!_isInitialized || index < 0 || index >= _positions.Length) return false;
			if (_originalInverseMasses[index] == 0f) return false; // originally pinned

			EnsureJobCompleted();

			_grabbedVertices.Add(index);
			_inverseMasses[index] = 0f;
			_positions[index] = position;
			_predictedPositions[index] = position;
			_velocities[index] = float3.zero;
			_isSleeping = false;
			_sleepCounter = 0;
			return true;
		}

		/// <summary>
		/// Updates the world position of an already-grabbed vertex.
		/// </summary>
		public void UpdateGrabPosition(int index, float3 position)
		{
			if (!_isInitialized || index < 0 || index >= _positions.Length) return;
			if (!_grabbedVertices.Contains(index)) return;

			EnsureJobCompleted();

			_positions[index] = position;
			_predictedPositions[index] = position;
			_velocities[index] = float3.zero;
			_isSleeping = false;
			_sleepCounter = 0;
		}

		/// <summary>
		/// Releases a grabbed vertex, restoring its original mass.
		/// </summary>
		public void ReleaseVertex(int index)
		{
			if (!_isInitialized || index < 0 || index >= _positions.Length) return;
			if (!_grabbedVertices.Remove(index)) return;

			EnsureJobCompleted();

			_inverseMasses[index] = _originalInverseMasses[index];
			_isSleeping = false;
			_sleepCounter = 0;
		}

		/// <summary>
		/// Releases all grabbed vertices.
		/// </summary>
		public void ReleaseAllGrabs()
		{
			if (!_isInitialized) return;

			EnsureJobCompleted();

			foreach (var index in _grabbedVertices)
				_inverseMasses[index] = _originalInverseMasses[index];
			_grabbedVertices.Clear();
			_isSleeping = false;
			_sleepCounter = 0;
		}

		public bool IsVertexGrabbed(int index) => _grabbedVertices.Contains(index);

		#endregion

		public void ForceSleep()
		{
			if (!_isInitialized) return;

			_clothJobHandle.Complete();

			for (var i = 0; i < _velocities.Length; i++)
				_velocities[i] = float3.zero;
			_isSleeping = true;
			_sleepCounter = SleepFramesRequired;
		}

		public void UpdateColliders(ClothCollider[] currentColliders)
		{
			if (!_isInitialized) return;
			if (currentColliders == null) return;

			var hasMeshColliders = false;
			for (var i = 0; i < currentColliders.Length; i++)
			{
				if (currentColliders[i].Type == ColliderType.Mesh)
				{
					hasMeshColliders = true;
					break;
				}
			}

			if (!HasCollidersChanged(currentColliders, hasMeshColliders))
				return;

			_clothJobHandle.Complete();
			_hasMeshColliders = hasMeshColliders;

			if (currentColliders.Length > _colliders.Length)
			{
				_colliders.Dispose();
				_colliders = new NativeArray<ClothCollider>(currentColliders.Length, Allocator.Persistent);
			}

			// Wake cloth if any collider moved since last update
			if (_isSleeping)
			{
				_isSleeping = false;
				_sleepCounter = 0;
			}

			NativeArray<ClothCollider>.Copy(currentColliders, _colliders, currentColliders.Length);
			_colliderCount = currentColliders.Length;
		}

		private bool HasCollidersChanged(ClothCollider[] newColliders, bool hasMeshColliders)
		{
			if (newColliders.Length != _colliderCount) return true;
			if (hasMeshColliders != _hasMeshColliders) return true;

			for (var i = 0; i < _colliderCount; i++)
			{
				var prev = _colliders[i];
				var curr = newColliders[i];
				if (math.lengthsq(prev.Position - curr.Position) > 1e-6f ||
					math.lengthsq(prev.Scale - curr.Scale) > 1e-6f ||
					!prev.Rotation.Equals(curr.Rotation))
				{
					return true;
				}
			}
			return false;
		}

		public void SetMeshData(float3[] vertices, int[] triangles)
		{
			_clothJobHandle.Complete();

			if (_meshVertices.IsCreated) _meshVertices.Dispose();
			if (_meshTriangles.IsCreated) _meshTriangles.Dispose();

			if (vertices != null && vertices.Length > 0)
			{
				_meshVertices = new NativeArray<float3>(vertices, Allocator.Persistent);
				_meshTriangles = new NativeArray<int>(triangles, Allocator.Persistent);
			}
			else
			{
				_meshVertices = new NativeArray<float3>(1, Allocator.Persistent);
				_meshTriangles = new NativeArray<int>(3, Allocator.Persistent);
			}
		}

		private void LateUpdate()
		{
			if (!_isInitialized) return;

			float dt = Time.deltaTime;
			if (dt <= 0f || _isSleeping || Paused) return;

			_clothJobHandle.Complete();
			RunSubSteps(dt);
			UpdateSleepState();
		}

		private void RunSubSteps(float dt)
		{
			var subSteps = math.max(1, SubSteps);
			var subDt = dt / subSteps;
			var subDamping = math.pow(Damping, 1f / subSteps);

			var handle = default(JobHandle);
			for (var s = 0; s < subSteps; s++)
			{
				handle = ScheduleIntegration(subDt, subDamping, handle);
				handle = ScheduleConstraintsAndCollisions(handle);
				handle = ScheduleVelocityUpdate(handle, subDt);
			}
			handle.Complete();
		}

		private JobHandle ScheduleIntegration(float subDt, float subDamping, JobHandle dependency)
		{
			var job = new IntegrateJob
			{
				Positions = _positions,
				PredictedPositions = _predictedPositions,
				Velocities = _velocities,
				InverseMasses = _inverseMasses,
				Gravity = Gravity,
				DeltaTime = subDt,
				Damping = subDamping
			};
			return job.Schedule(_positions.Length, 64, dependency);
		}

		private JobHandle ScheduleConstraintsAndCollisions(JobHandle handle)
		{
			for (var i = 0; i < SolverIterations; i++)
			{
				handle = ScheduleDistanceConstraints(handle);
				if (i == 0)
					handle = ScheduleBendingConstraints(handle);
				if (ShouldResolveIterationCollisions(i))
					handle = ScheduleCollisionResolution(handle);
			}
			handle = ScheduleCollisionAndFriction(handle);
			return handle;
		}

		private bool ShouldResolveIterationCollisions(int iterationIndex)
		{
			if (!_hasMeshColliders)
				return true;

			return (iterationIndex + 1) % MeshCollisionIterationStride == 0;
		}

		private JobHandle ScheduleDistanceConstraints(JobHandle handle)
		{
			var job = new SolveDistanceConstraintsJob
			{
				PredictedPositions = _predictedPositions,
				InverseMasses = _inverseMasses,
				Constraints = _constraints
			};
			return job.Schedule(handle);
		}

		private JobHandle ScheduleBendingConstraints(JobHandle handle)
		{
			if (_bendingConstraints.Length <= 0) return handle;

			var job = new SolveBendingConstraintsJob
			{
				PredictedPositions = _predictedPositions,
				InverseMasses = _inverseMasses,
				Constraints = _bendingConstraints
			};
			return job.Schedule(handle);
		}

		private JobHandle ScheduleCollisionAndFriction(JobHandle handle)
		{
			var copyJob = new CopyPositionsJob
			{
				Source = _predictedPositions,
				Destination = _preCollisionPositions
			};
			handle = copyJob.Schedule(_positions.Length, 64, handle);
			handle = ScheduleCollisionResolution(handle);

			var frictionJob = new ApplyFrictionJob
			{
				PredictedPositions = _predictedPositions,
				PreCollisionPositions = _preCollisionPositions,
				OriginalPositions = _positions,
				CurrentContactNormals = _currentContactNormals,
				ContactNormals = _contactNormals,
				Friction = Friction
			};
			return frictionJob.Schedule(_positions.Length, 64, handle);
		}

		private JobHandle ScheduleCollisionResolution(JobHandle handle)
		{
			var collisionJob = new SolveCollisionsJob
			{
				PredictedPositions = _predictedPositions,
				PreCollisionPositions = _preCollisionPositions,
				CurrentContactNormals = _currentContactNormals,
				Colliders = _colliders,
				ColliderCount = _colliderCount,
				MeshVertices = _meshVertices,
				MeshTriangles = _meshTriangles,
				ParticleRadius = ParticleRadius,
				CollisionSurfaceOffset = CollisionSurfaceOffset
			};
			return collisionJob.Schedule(_positions.Length, 64, handle);
		}

		private JobHandle ScheduleVelocityUpdate(JobHandle handle, float subDt)
		{
			var job = new UpdateVelocitiesJob
			{
				Positions = _positions,
				PredictedPositions = _predictedPositions,
				PreCollisionPositions = _preCollisionPositions,
				Velocities = _velocities,
				InverseMasses = _inverseMasses,
				CurrentContactNormals = _currentContactNormals,
				ContactNormals = _contactNormals,
				DeltaTime = subDt,
				VelocityDecay = VelocityDecay,
				VelocitySnapThreshold = SleepThreshold * 0.5f
			};
			return job.Schedule(_positions.Length, 64, handle);
		}

		private void UpdateSleepState()
		{
			var maxVelSq = 0f;
			var hasRestingContact = false;
			for (var i = 0; i < _velocities.Length; i++)
			{
				var vSq = math.lengthsq(_velocities[i]);
				if (vSq > maxVelSq) maxVelSq = vSq;

				if (!hasRestingContact && _contactNormals.IsCreated &&
					math.lengthsq(_contactNormals[i]) > RestingContactNormalThresholdSq)
				{
					hasRestingContact = true;
				}
			}

			var sleepThresholdSq = SleepThreshold * SleepThreshold;
			var restingContactSleepThresholdSq = sleepThresholdSq * 4f;

			if (maxVelSq < sleepThresholdSq || (hasRestingContact && maxVelSq < restingContactSleepThresholdSq))
			{
				_sleepCounter++;
				if (_sleepCounter >= SleepFramesRequired)
				{
					_isSleeping = true;
					for (var i = 0; i < _velocities.Length; i++)
						_velocities[i] = float3.zero;
				}
			}
			else
			{
				_sleepCounter = 0;
			}
		}

		private void OnDestroy()
		{
			_clothJobHandle.Complete();
			DisposeIfCreated(ref _positions);
			DisposeIfCreated(ref _predictedPositions);
			DisposeIfCreated(ref _preCollisionPositions);
			DisposeIfCreated(ref _contactNormals);
			DisposeIfCreated(ref _currentContactNormals);
			DisposeIfCreated(ref _velocities);
			DisposeIfCreated(ref _inverseMasses);
			DisposeIfCreated(ref _initialPositions);
			DisposeIfCreated(ref _constraints);
			DisposeIfCreated(ref _bendingConstraints);
			DisposeIfCreated(ref _colliders);
			DisposeIfCreated(ref _meshVertices);
			DisposeIfCreated(ref _meshTriangles);
			DisposeIfCreated(ref _originalInverseMasses);
		}

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (!_isInitialized || !_positions.IsCreated) return;

			if (DrawVertices)
			{
				DrawVerticesGizmo();
			}

			if (DrawConstraints)
			{
				DrawConstraintsGizmo();
			}
		}

		private void DrawVerticesGizmo()
		{
			var oldColor = Gizmos.color;

			for (var i = 0; i < _positions.Length; i++)
			{
				var pos = (Vector3)_positions[i];
				var isGrabbed = _grabbedVertices.Contains(i);
				var isPinned = _inverseMasses[i] == 0f && !isGrabbed;

				// Draw grabbed vertices
				if (isGrabbed)
				{
					Gizmos.color = GrabbedVertexColor;
					DrawVertex(pos);

					if (DrawVertexLabels)
					{
						Handles.Label(pos + Vector3.up * VertexSize * 2, $"G{i}");
					}
				}
				// Draw pinned and free vertices based on settings
				else if (isPinned && DrawPinnedVertices)
				{
					Gizmos.color = PinnedVertexColor;
					DrawVertex(pos);

					if (DrawVertexLabels)
					{
						Handles.Label(pos + Vector3.up * VertexSize * 2, $"P{i}");
					}
				}
				else if (!isPinned)
				{
					Gizmos.color = FreeVertexColor;
					DrawVertex(pos);

					if (DrawVertexLabels)
					{
						Handles.Label(pos + Vector3.up * VertexSize * 2, $"{i}");
					}
				}
			}

			Gizmos.color = oldColor;
		}

		private void DrawVertex(Vector3 position)
		{
			// Draw a small filled sphere for each vertex
			Gizmos.DrawSphere(position, VertexSize);
		}

		private void DrawConstraintsGizmo()
		{
			var oldColor = Gizmos.color;
			Gizmos.color = ConstraintColor;

			// Draw distance constraints
			if (_constraints.IsCreated)
			{
				for (var i = 0; i < _constraints.Length; i++)
				{
					var constraint = _constraints[i];
					var posA = (Vector3)_positions[constraint.IndexA];
					var posB = (Vector3)_positions[constraint.IndexB];
					Gizmos.DrawLine(posA, posB);
				}
			}

			// Draw bending constraints with different color
			if (_bendingConstraints.IsCreated && _bendingConstraints.Length > 0)
			{
				Gizmos.color = new Color(ConstraintColor.r, ConstraintColor.g * 0.5f, ConstraintColor.b, ConstraintColor.a);
				for (var i = 0; i < _bendingConstraints.Length; i++)
				{
					var constraint = _bendingConstraints[i];
					var posA = (Vector3)_positions[constraint.IndexA];
					var posB = (Vector3)_positions[constraint.IndexB];
					Gizmos.DrawLine(posA, posB);
				}
			}

			Gizmos.color = oldColor;
		}
#endif

		private static void DisposeIfCreated<T>(ref NativeArray<T> array) where T : struct
		{
			if (array.IsCreated) array.Dispose();
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct IntegrateJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<float3> Positions;
		public NativeArray<float3> PredictedPositions;
		public NativeArray<float3> Velocities;
		[ReadOnly] public NativeArray<float> InverseMasses;

		public float3 Gravity;
		public float DeltaTime;
		public float Damping;

		public void Execute(int i)
		{
			if (InverseMasses[i] == 0f) return; // Pinned

			Velocities[i] += Gravity * DeltaTime;
			Velocities[i] *= Damping;
			PredictedPositions[i] = Positions[i] + Velocities[i] * DeltaTime;
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct SolveDistanceConstraintsJob : IJob
	{
		public NativeArray<float3> PredictedPositions;
		[ReadOnly] public NativeArray<float> InverseMasses;
		[ReadOnly] public NativeArray<DistanceConstraint> Constraints;

		public void Execute()
		{
			for (var i = 0; i < Constraints.Length; i++)
			{
				var constraint = Constraints[i];
				var wA = InverseMasses[constraint.IndexA];
				var wB = InverseMasses[constraint.IndexB];
				var wSum = wA + wB;

				if (wSum == 0f) continue; // Both pinned

				var pA = PredictedPositions[constraint.IndexA];
				var pB = PredictedPositions[constraint.IndexB];

				var delta = pB - pA;
				var currentLength = math.length(delta);
				if (currentLength < 1e-4f) continue;

				var error = currentLength - constraint.RestLength;
				var correction = delta / currentLength * (error * constraint.Stiffness / wSum);

				PredictedPositions[constraint.IndexA] += correction * wA;
				PredictedPositions[constraint.IndexB] -= correction * wB;
			}
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct SolveBendingConstraintsJob : IJob
	{
		public NativeArray<float3> PredictedPositions;
		[ReadOnly] public NativeArray<float> InverseMasses;
		[ReadOnly] public NativeArray<BendingConstraint> Constraints;

		public void Execute()
		{
			for (var i = 0; i < Constraints.Length; i++)
			{
				var c = Constraints[i];
				var wA = InverseMasses[c.IndexA];
				var wB = InverseMasses[c.IndexB];
				var wSum = wA + wB;

				if (wSum == 0f) continue;

				var pA = PredictedPositions[c.IndexA];
				var pB = PredictedPositions[c.IndexB];

				var delta = pB - pA;
				var currentLength = math.length(delta);
				if (currentLength < 1e-4f) continue;

				var error = currentLength - c.RestLength;
				var correction = delta / currentLength * (error * c.Stiffness / wSum);

				PredictedPositions[c.IndexA] += correction * wA;
				PredictedPositions[c.IndexB] -= correction * wB;
			}
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct SolveCollisionsJob : IJobParallelFor
	{
		public NativeArray<float3> PredictedPositions;
		[ReadOnly] public NativeArray<float3> PreCollisionPositions;
		public NativeArray<float3> CurrentContactNormals;
		[ReadOnly] public NativeArray<ClothCollider> Colliders;
		[ReadOnly] public int ColliderCount;
		[ReadOnly] public NativeArray<float3> MeshVertices;
		[ReadOnly] public NativeArray<int> MeshTriangles;
		[ReadOnly] public float ParticleRadius;
		[ReadOnly] public float CollisionSurfaceOffset;
		private const float ContactBand = 0.003f;

		public void Execute(int i)
		{
			var p = PredictedPositions[i];
			var preCollisionP = PreCollisionPositions[i];
			var currentContactNormal = float3.zero;

			for (var c = 0; c < ColliderCount; c++)
			{
				var collider = Colliders[c];

				switch (collider.Type)
				{
					case ColliderType.Sphere:
						p = ResolveSphere(p, collider);
						break;
					case ColliderType.Box:
						p = ResolveBox(p, collider, ref currentContactNormal);
						break;
					case ColliderType.Capsule:
						p = ResolveCapsule(p, collider);
						break;
					case ColliderType.Mesh:
						p = ResolveMesh(p, preCollisionP, collider);
						break;
					case ColliderType.Plane:
						p = ResolvePlane(p, collider, ref currentContactNormal);
						break;
				}
			}

			if (CurrentContactNormals.IsCreated)
				CurrentContactNormals[i] = currentContactNormal;
			PredictedPositions[i] = p;
		}

		private float3 ResolveSphere(float3 p, in ClothCollider collider)
		{
			var delta = p - collider.Position;
			var distSq = math.lengthsq(delta);
			var radius = collider.Scale.x + ParticleRadius + CollisionSurfaceOffset;

			if (distSq < radius * radius && distSq > 1e-6f)
			{
				var dist = math.sqrt(distSq);
				p = collider.Position + delta / dist * radius;
			}
			return p;
		}

		private float3 ResolveBox(float3 p, in ClothCollider collider, ref float3 currentContactNormal)
		{
			var localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
			var extents = collider.Scale + ParticleRadius + CollisionSurfaceOffset;
			var absLocalP = math.abs(localP);

			if (absLocalP.x < extents.x &&
				absLocalP.y < extents.y &&
				absLocalP.z < extents.z)
			{
				float3 dists = extents - absLocalP;
				float3 localNormal;

				if (dists.x < dists.y && dists.x < dists.z)
				{
					localP.x = (localP.x >= 0f ? 1f : -1f) * extents.x;
					localNormal = new float3(localP.x >= 0f ? 1f : -1f, 0f, 0f);
				}
				else if (dists.y < dists.z)
				{
					localP.y = (localP.y >= 0f ? 1f : -1f) * extents.y;
					localNormal = new float3(0f, localP.y >= 0f ? 1f : -1f, 0f);
				}
				else
				{
					localP.z = (localP.z >= 0f ? 1f : -1f) * extents.z;
					localNormal = new float3(0f, 0f, localP.z >= 0f ? 1f : -1f);
				}

				currentContactNormal = math.mul(collider.Rotation, localNormal);
				p = collider.Position + math.mul(collider.Rotation, localP);
				return p;
			}

			if (absLocalP.y >= extents.y && absLocalP.y <= extents.y + ContactBand &&
				absLocalP.x <= extents.x && absLocalP.z <= extents.z)
				currentContactNormal = math.mul(collider.Rotation, new float3(0f, localP.y >= 0f ? 1f : -1f, 0f));
			else if (absLocalP.x >= extents.x && absLocalP.x <= extents.x + ContactBand &&
				absLocalP.y <= extents.y && absLocalP.z <= extents.z)
				currentContactNormal = math.mul(collider.Rotation, new float3(localP.x >= 0f ? 1f : -1f, 0f, 0f));
			else if (absLocalP.z >= extents.z && absLocalP.z <= extents.z + ContactBand &&
				absLocalP.x <= extents.x && absLocalP.y <= extents.y)
				currentContactNormal = math.mul(collider.Rotation, new float3(0f, 0f, localP.z >= 0f ? 1f : -1f));

			return p;
		}

		// Half-space plane collision — immune to tunneling regardless of particle speed.
		// collider.Scale stores the world-space plane normal (unit vector pointing toward valid side).
		private float3 ResolvePlane(float3 p, in ClothCollider collider, ref float3 currentContactNormal)
		{
			var normal = collider.Scale;
			var dist = math.dot(p - collider.Position, normal);
			var contactDistance = ParticleRadius + CollisionSurfaceOffset;
			if (dist <= contactDistance + ContactBand)
				currentContactNormal = normal;
			if (dist < contactDistance)
				p += normal * (contactDistance - dist);
			return p;
		}

		private float3 ResolveCapsule(float3 p, in ClothCollider collider)
		{
			var up = math.mul(collider.Rotation, new float3(0f, 1f, 0f));
			var pA = collider.Position + up * collider.Scale.y;
			var pB = collider.Position - up * collider.Scale.y;

			var ab = pB - pA;
			var ap = p - pA;
			var abSq = math.lengthsq(ab);

			var t = abSq > 1e-5f ? math.saturate(math.dot(ap, ab) / abSq) : 0f;
			var closest = pA + t * ab;

			var delta = p - closest;
			var distSq = math.lengthsq(delta);
			var radius = collider.Scale.x + ParticleRadius + CollisionSurfaceOffset;

			if (distSq < radius * radius && distSq > 1e-6f)
			{
				var dist = math.sqrt(distSq);
				p = closest + delta / dist * radius;
			}
			return p;
		}

		private float3 ResolveMesh(float3 p, float3 preCollisionP, in ClothCollider collider)
		{
			var localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
			localP /= collider.Scale;
			var previousLocalP = math.mul(math.inverse(collider.Rotation), preCollisionP - collider.Position);
			previousLocalP /= collider.Scale;

			var padding = 0.1f;
			// Keep XZ bounds and upper-Y check, but do NOT skip particles below BoundsMin.y —
			// they may have tunneled through the surface and still need to be resolved.
			if (localP.x < collider.BoundsMin.x - padding || localP.x > collider.BoundsMax.x + padding ||
				localP.y > collider.BoundsMax.y + padding ||
				localP.z < collider.BoundsMin.z - padding || localP.z > collider.BoundsMax.z + padding)
			{
				return p;
			}

			var localRadius = ParticleRadius + CollisionSurfaceOffset;

			for (var t = 0; t < collider.TriCount; t += 3)
			{
				var triIndex = collider.TriStart + t;
				var v0 = MeshVertices[MeshTriangles[triIndex]];
				var v1 = MeshVertices[MeshTriangles[triIndex + 1]];
				var v2 = MeshVertices[MeshTriangles[triIndex + 2]];

				// Use signed distance to triangle plane so tunneled particles (negative
				// signed distance) are caught even when far below the surface.
				var crossVec = math.cross(v1 - v0, v2 - v0);
				var crossLen = math.length(crossVec);
				if (crossLen < 1e-6f) continue; // Degenerate triangle — skip

				var triNormal = crossVec / crossLen;
				var signedDist = math.dot(localP - v0, triNormal);
				var previousSignedDist = math.dot(previousLocalP - v0, triNormal);

				// Particle is safely above the surface — no collision needed
				if (signedDist >= localRadius) continue;

				// Treat the mesh as one-sided: only resolve back-side penetration when the
				// particle actually crossed the triangle plane during this substep.
				if (signedDist < 0f && previousSignedDist <= 0f) continue;

				// Check that the horizontal projection onto the triangle plane falls
				// within (or close to) the triangle's footprint before resolving.
				var projectedP = localP - signedDist * triNormal;
				var closestInTri = ClosestPointOnTriangle(projectedP, v0, v1, v2);
				var projDistSq = math.lengthsq(projectedP - closestInTri);
				if (projDistSq > localRadius * localRadius) continue;

				// Push particle to the correct (front) side of the triangle surface.
				localP += triNormal * (localRadius - signedDist);
				p = collider.Position + math.mul(collider.Rotation, localP * collider.Scale);
				break;
			}

			return p;
		}

		// Math utility: Find closest point on a triangle in 3D space
		private float3 ClosestPointOnTriangle(float3 p, float3 a, float3 b, float3 c)
		{
			var ab = b - a;
			var ac = c - a;
			var ap = p - a;
			var d1 = math.dot(ab, ap);
			var d2 = math.dot(ac, ap);
			if (d1 <= 0.0f && d2 <= 0.0f)
				return a;

			var bp = p - b;
			var d3 = math.dot(ab, bp);
			var d4 = math.dot(ac, bp);
			if (d3 >= 0.0f && d4 <= d3)
				return b;
			var vc = d1 * d4 - d3 * d2;
			if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f)
			{
				var v = d1 / (d1 - d3);
				return a + v * ab;
			}
			var cp = p - c;
			var d5 = math.dot(ab, cp);
			var d6 = math.dot(ac, cp);
			if (d6 >= 0.0f && d5 <= d6)
				return c;
			var vb = d5 * d2 - d1 * d6;
			if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f)
			{
				var w = d2 / (d2 - d6); return a + w * ac;
			}
			var va = d3 * d6 - d5 * d4;
			if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
			{
				var w = (d4 - d3) / (d4 - d3 + (d5 - d6));
				return b + w * (c - b);
			}
			var denom = 1.0f / (va + vb + vc);
			var vn = vb * denom;
			var wn = vc * denom;
			return a + ab * vn + ac * wn;
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct UpdateVelocitiesJob : IJobParallelFor
	{
		private const float RestingContactNormalThresholdSq = 0.01f;
		private const float RestingContactVelocitySnapScale = 3f;

		public NativeArray<float3> Positions;
		[ReadOnly] public NativeArray<float3> PredictedPositions;
		[ReadOnly] public NativeArray<float3> PreCollisionPositions;
		public NativeArray<float3> Velocities;
		[ReadOnly] public NativeArray<float> InverseMasses;
		[ReadOnly] public NativeArray<float3> CurrentContactNormals;
		public NativeArray<float3> ContactNormals;
		public float DeltaTime;
		public float VelocityDecay;
		public float VelocitySnapThreshold;

		public void Execute(int i)
		{
			if (InverseMasses[i] == 0f)
				return; // Pinned

			var vel = (PredictedPositions[i] - Positions[i]) / DeltaTime;
			var currentContactNormal = CurrentContactNormals.IsCreated ? CurrentContactNormals[i] : float3.zero;
			var hasRestingContact = false;

			if (math.lengthsq(currentContactNormal) > RestingContactNormalThresholdSq)
			{
				// Current sub-step contact: preserve full contact normal even when there
				// was no penetration push, so resting support does not immediately lose friction.
				var normal = math.normalize(currentContactNormal);
				if (ContactNormals.IsCreated)
					ContactNormals[i] = normal;
				hasRestingContact = true;

				var normalVel = math.dot(vel, normal);
				if (normalVel > 0f)
					vel -= normalVel * normal;
			}
			else
			{
				var collisionPush = PredictedPositions[i] - PreCollisionPositions[i];
				if (math.lengthsq(collisionPush) > 1e-8f)
				{
					var normal = math.normalize(collisionPush);
					if (ContactNormals.IsCreated)
						ContactNormals[i] = normal;

					var normalVel = math.dot(vel, normal);
					if (normalVel > 0f)
						vel -= normalVel * normal;
				}
				else
				{
					var storedNormal = ContactNormals.IsCreated ? ContactNormals[i] : float3.zero;
					if (ContactNormals.IsCreated && math.lengthsq(storedNormal) > RestingContactNormalThresholdSq)
					{
						hasRestingContact = true;
						ContactNormals[i] = storedNormal * 0.5f;
					}
				}
			}

			vel *= math.exp(-VelocityDecay * DeltaTime);

			var velocitySnapThreshold = hasRestingContact ?
				VelocitySnapThreshold * RestingContactVelocitySnapScale :
				VelocitySnapThreshold;

			if (math.lengthsq(vel) < velocitySnapThreshold * velocitySnapThreshold)
				vel = float3.zero;

			Velocities[i] = vel;
			Positions[i] = PredictedPositions[i];
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct CopyPositionsJob : IJobParallelFor
	{
		[ReadOnly] public NativeArray<float3> Source;
		[WriteOnly] public NativeArray<float3> Destination;

		public void Execute(int i)
		{
			Destination[i] = Source[i];
		}
	}

	[BurstCompile(CompileSynchronously = true)]
	public struct ApplyFrictionJob : IJobParallelFor
	{
		private const float RestingContactNormalThresholdSq = 0.01f;
		private const float RestingContactStaticFrictionScale = 5f;
		private const float RestingContactTangentialScale = 0.25f;

		public NativeArray<float3> PredictedPositions;
		[ReadOnly] public NativeArray<float3> PreCollisionPositions;
		[ReadOnly] public NativeArray<float3> OriginalPositions;
		[ReadOnly] public NativeArray<float3> CurrentContactNormals;
		[ReadOnly] public NativeArray<float3> ContactNormals;
		public float Friction;

		public void Execute(int i)
		{
			var pre = PreCollisionPositions[i];
			var post = PredictedPositions[i];
			var collisionPush = post - pre;
			var currentContactNormal = CurrentContactNormals.IsCreated ? CurrentContactNormals[i] : float3.zero;
			var hasActiveCollision = math.lengthsq(collisionPush) > 1e-8f;

			float3 normal;
			float frictionStrength;
			var hasRestingContact = false;

			if (math.lengthsq(currentContactNormal) > RestingContactNormalThresholdSq)
			{
				normal = math.normalize(currentContactNormal);
				frictionStrength = 1f;
				hasRestingContact = true;
			}
			else if (hasActiveCollision)
			{
				// Active collision: use fresh normal at full strength
				normal = math.normalize(collisionPush);
				frictionStrength = 1f;
			}
			else
			{
				// No active collision — check stored contact normal for persistence
				var stored = ContactNormals.IsCreated ? ContactNormals[i] : float3.zero;
				var storedLenSq = math.lengthsq(stored);
				if (storedLenSq < RestingContactNormalThresholdSq) return; // no recent contact

				normal = math.normalize(stored);
				frictionStrength = math.sqrt(storedLenSq); // decayed magnitude
				hasRestingContact = true;
			}

			var totalMotion = post - OriginalPositions[i];
			var normalMotion = math.dot(totalMotion, normal) * normal;
			var tangentialMotion = totalMotion - normalMotion;

			var effectiveFriction = Friction * frictionStrength;
			var tangentialScale = math.saturate(1f - effectiveFriction);
			tangentialScale *= tangentialScale;

			var staticFrictionMotionThreshold = 0.002f * effectiveFriction;
			if (hasRestingContact)
				staticFrictionMotionThreshold *= RestingContactStaticFrictionScale;

			if (math.lengthsq(tangentialMotion) < staticFrictionMotionThreshold * staticFrictionMotionThreshold)
				tangentialMotion = float3.zero;
			else
			{
				tangentialMotion *= tangentialScale;
				if (hasRestingContact)
					tangentialMotion *= RestingContactTangentialScale;
			}

			PredictedPositions[i] = OriginalPositions[i] + normalMotion + tangentialMotion;
		}
	}
}