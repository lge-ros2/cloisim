/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System;
using UnityEngine;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Burst;

namespace CLOiSim.Cloth
{
    public enum ColliderType
	{
		Sphere, Box, Capsule, Mesh
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

        /// <summary>
        /// When true, the simulation is paused (e.g. during gizmo manipulation).
        /// </summary>
        [NonSerialized]
        public bool Paused = false;
        public float ParticleRadius = 0.01f;

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

        private JobHandle _clothJobHandle;
        private bool _isInitialized = false;
        private int _colliderCount = 0;
        private bool _isSleeping = false;
        private int _sleepCounter = 0;
        private const int SleepFramesRequired = 10;

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
                _inverseMasses[i] = masses[i] > 0f ? 1f / masses[i] : 0f; // 0 inverse mass = pinned/kinematic
            }

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

            for (var i = 0; i < _positions.Length; i++)
            {
                _positions[i] = _initialPositions[i];
                _predictedPositions[i] = _initialPositions[i];
                _velocities[i] = float3.zero;
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
            }
            _isSleeping = false;
            _sleepCounter = 0;
        }

        public void UpdateColliders(ClothCollider[] currentColliders)
        {
            if (!_isInitialized) return;

            _clothJobHandle.Complete();

            if (currentColliders.Length > _colliders.Length)
            {
                _colliders.Dispose();
                _colliders = new NativeArray<ClothCollider>(currentColliders.Length, Allocator.Persistent);
            }

            NativeArray<ClothCollider>.Copy(currentColliders, _colliders, currentColliders.Length);
            _colliderCount = currentColliders.Length;
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

            for (var s = 0; s < subSteps; s++)
            {
                var handle = ScheduleIntegration(subDt, subDamping);
                handle = ScheduleConstraintsAndCollisions(handle);
                handle = ScheduleVelocityUpdate(handle, subDt);
                handle.Complete();
            }
        }

        private JobHandle ScheduleIntegration(float subDt, float subDamping)
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
            return job.Schedule(_positions.Length, 64);
        }

        private JobHandle ScheduleConstraintsAndCollisions(JobHandle handle)
        {
            for (var i = 0; i < SolverIterations; i++)
            {
                handle = ScheduleDistanceConstraints(handle);
                handle = ScheduleBendingConstraints(handle);
                handle = ScheduleCollisionAndFriction(handle);
            }
            return handle;
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

            var collisionJob = new SolveCollisionsJob
            {
                PredictedPositions = _predictedPositions,
                Colliders = _colliders,
                ColliderCount = _colliderCount,
                MeshVertices = _meshVertices,
                MeshTriangles = _meshTriangles,
                ParticleRadius = ParticleRadius
            };
            handle = collisionJob.Schedule(_positions.Length, 64, handle);

            var frictionJob = new ApplyFrictionJob
            {
                PredictedPositions = _predictedPositions,
                PreCollisionPositions = _preCollisionPositions,
                OriginalPositions = _positions,
                Friction = Friction
            };
            return frictionJob.Schedule(_positions.Length, 64, handle);
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
                DeltaTime = subDt,
                SleepThreshold = SleepThreshold
            };
            return job.Schedule(_positions.Length, 64, handle);
        }

        private void UpdateSleepState()
        {
            var maxVelSq = 0f;
            for (var i = 0; i < _velocities.Length; i++)
            {
                var vSq = math.lengthsq(_velocities[i]);
                if (vSq > maxVelSq) maxVelSq = vSq;
            }

            if (maxVelSq < SleepThreshold * SleepThreshold)
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
            DisposeIfCreated(ref _velocities);
            DisposeIfCreated(ref _inverseMasses);
            DisposeIfCreated(ref _initialPositions);
            DisposeIfCreated(ref _constraints);
            DisposeIfCreated(ref _bendingConstraints);
            DisposeIfCreated(ref _colliders);
            DisposeIfCreated(ref _meshVertices);
            DisposeIfCreated(ref _meshTriangles);
        }

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
            for (int i = 0; i < Constraints.Length; i++)
            {
                var constraint = Constraints[i];
                float wA = InverseMasses[constraint.IndexA];
                float wB = InverseMasses[constraint.IndexB];
                float wSum = wA + wB;

                if (wSum == 0f) continue; // Both pinned

                float3 pA = PredictedPositions[constraint.IndexA];
                float3 pB = PredictedPositions[constraint.IndexB];

                float3 delta = pB - pA;
                float currentLength = math.length(delta);
                if (currentLength < 1e-4f) continue;

                float error = currentLength - constraint.RestLength;
                float3 correction = (delta / currentLength) * (error * constraint.Stiffness / wSum);

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
            for (int i = 0; i < Constraints.Length; i++)
            {
                var c = Constraints[i];
                float wA = InverseMasses[c.IndexA];
                float wB = InverseMasses[c.IndexB];
                float wSum = wA + wB;

                if (wSum == 0f) continue;

                float3 pA = PredictedPositions[c.IndexA];
                float3 pB = PredictedPositions[c.IndexB];

                float3 delta = pB - pA;
                float currentLength = math.length(delta);
                if (currentLength < 1e-4f) continue;

                float error = currentLength - c.RestLength;
                float3 correction = (delta / currentLength) * (error * c.Stiffness / wSum);

                PredictedPositions[c.IndexA] += correction * wA;
                PredictedPositions[c.IndexB] -= correction * wB;
            }
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct SolveCollisionsJob : IJobParallelFor
    {
        public NativeArray<float3> PredictedPositions;
        [ReadOnly] public NativeArray<ClothCollider> Colliders;
        [ReadOnly] public int ColliderCount;
        [ReadOnly] public NativeArray<float3> MeshVertices;
        [ReadOnly] public NativeArray<int> MeshTriangles;
        [ReadOnly] public float ParticleRadius;

        public void Execute(int i)
        {
            float3 p = PredictedPositions[i];

            for (int c = 0; c < ColliderCount; c++)
            {
                var collider = Colliders[c];

                switch (collider.Type)
                {
                    case ColliderType.Sphere:
                        p = ResolveSphere(p, collider);
                        break;
                    case ColliderType.Box:
                        p = ResolveBox(p, collider);
                        break;
                    case ColliderType.Capsule:
                        p = ResolveCapsule(p, collider);
                        break;
                    case ColliderType.Mesh:
                        p = ResolveMesh(p, collider);
                        break;
                }
            }

            PredictedPositions[i] = p;
        }

        private float3 ResolveSphere(float3 p, in ClothCollider collider)
        {
            float3 delta = p - collider.Position;
            float distSq = math.lengthsq(delta);
            float radius = collider.Scale.x + ParticleRadius;

            if (distSq < radius * radius && distSq > 1e-6f)
            {
                float dist = math.sqrt(distSq);
                p = collider.Position + (delta / dist) * radius;
            }
            return p;
        }

        private float3 ResolveBox(float3 p, in ClothCollider collider)
        {
            float3 localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
            float3 extents = collider.Scale + ParticleRadius;

            if (math.abs(localP.x) < extents.x &&
                math.abs(localP.y) < extents.y &&
                math.abs(localP.z) < extents.z)
            {
                float3 dists = extents - math.abs(localP);

                if (dists.x < dists.y && dists.x < dists.z)
                    localP.x = (localP.x >= 0f ? 1f : -1f) * extents.x;
                else if (dists.y < dists.z)
                    localP.y = (localP.y >= 0f ? 1f : -1f) * extents.y;
                else
                    localP.z = (localP.z >= 0f ? 1f : -1f) * extents.z;

                p = collider.Position + math.mul(collider.Rotation, localP);
            }
            return p;
        }

        private float3 ResolveCapsule(float3 p, in ClothCollider collider)
        {
            float3 up = math.mul(collider.Rotation, new float3(0f, 1f, 0f));
            float3 pA = collider.Position + up * collider.Scale.y;
            float3 pB = collider.Position - up * collider.Scale.y;

            float3 ab = pB - pA;
            float3 ap = p - pA;
            float abSq = math.lengthsq(ab);

            float t = abSq > 1e-5f ? math.saturate(math.dot(ap, ab) / abSq) : 0f;
            float3 closest = pA + t * ab;

            float3 delta = p - closest;
            float distSq = math.lengthsq(delta);
            float radius = collider.Scale.x + ParticleRadius;

            if (distSq < radius * radius && distSq > 1e-6f)
            {
                float dist = math.sqrt(distSq);
                p = closest + (delta / dist) * radius;
            }
            return p;
        }

        private float3 ResolveMesh(float3 p, in ClothCollider collider)
        {
            float3 localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
            localP /= collider.Scale;

            float padding = 0.1f;
            if (localP.x < collider.BoundsMin.x - padding || localP.x > collider.BoundsMax.x + padding ||
                localP.y < collider.BoundsMin.y - padding || localP.y > collider.BoundsMax.y + padding ||
                localP.z < collider.BoundsMin.z - padding || localP.z > collider.BoundsMax.z + padding)
            {
                return p;
            }

            float minDistSq = float.MaxValue;
            float3 bestLocalPush = localP;
            bool hit = false;

            for (int t = 0; t < collider.TriCount; t += 3)
            {
                int triIndex = collider.TriStart + t;
                float3 v0 = MeshVertices[MeshTriangles[triIndex]];
                float3 v1 = MeshVertices[MeshTriangles[triIndex + 1]];
                float3 v2 = MeshVertices[MeshTriangles[triIndex + 2]];

                float3 closest = ClosestPointOnTriangle(localP, v0, v1, v2);
                float distSq = math.lengthsq(localP - closest);

                if (distSq < minDistSq)
                {
                    minDistSq = distSq;
                    bestLocalPush = closest;
                    hit = true;
                }
            }

            float localRadius = ParticleRadius;
            if (hit && minDistSq < localRadius * localRadius && minDistSq > 1e-8f)
            {
                float dist = math.sqrt(minDistSq);
                float3 dir = (localP - bestLocalPush) / dist;
                localP = bestLocalPush + dir * localRadius;
                p = collider.Position + math.mul(collider.Rotation, localP * collider.Scale);
            }
            return p;
        }

        // Math utility: Find closest point on a triangle in 3D space
        private float3 ClosestPointOnTriangle(float3 p, float3 a, float3 b, float3 c)
        {
            float3 ab = b - a; float3 ac = c - a; float3 ap = p - a;
            float d1 = math.dot(ab, ap); float d2 = math.dot(ac, ap);
            if (d1 <= 0.0f && d2 <= 0.0f) return a;
            float3 bp = p - b; float d3 = math.dot(ab, bp); float d4 = math.dot(ac, bp);
            if (d3 >= 0.0f && d4 <= d3) return b;
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0.0f && d1 >= 0.0f && d3 <= 0.0f) { float v = d1 / (d1 - d3); return a + v * ab; }
            float3 cp = p - c; float d5 = math.dot(ab, cp); float d6 = math.dot(ac, cp);
            if (d6 >= 0.0f && d5 <= d6) return c;
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0.0f && d2 >= 0.0f && d6 <= 0.0f) { float w = d2 / (d2 - d6); return a + w * ac; }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0.0f && (d4 - d3) >= 0.0f && (d5 - d6) >= 0.0f)
            {
                float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return b + w * (c - b);
            }
            float denom = 1.0f / (va + vb + vc);
            float vn = vb * denom; float wn = vc * denom;
            return a + ab * vn + ac * wn;
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public struct UpdateVelocitiesJob : IJobParallelFor
    {
        public NativeArray<float3> Positions;
        [ReadOnly] public NativeArray<float3> PredictedPositions;
        [ReadOnly] public NativeArray<float3> PreCollisionPositions;
        public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<float> InverseMasses;
        public float DeltaTime;
        public float SleepThreshold;

        public void Execute(int i)
        {
            if (InverseMasses[i] == 0f) return; // Pinned

            var vel = (PredictedPositions[i] - Positions[i]) / DeltaTime;

            // If collision happened, remove velocity along the collision push direction (prevents bouncing)
            var collisionPush = PredictedPositions[i] - PreCollisionPositions[i];
            if (math.lengthsq(collisionPush) > 1e-8f)
            {
                var normal = math.normalize(collisionPush);
                var normalVel = math.dot(vel, normal);
                // Only remove outward velocity (away from surface), keep inward to allow settling
                if (normalVel > 0f)
                    vel -= normalVel * normal;
            }

            // Sleep: zero out tiny velocities to let the cloth settle
            if (math.lengthsq(vel) < SleepThreshold * SleepThreshold)
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
        public NativeArray<float3> PredictedPositions;
        [ReadOnly] public NativeArray<float3> PreCollisionPositions;
        [ReadOnly] public NativeArray<float3> OriginalPositions;
        public float Friction;

        public void Execute(int i)
        {
            var pre = PreCollisionPositions[i];
            var post = PredictedPositions[i];
            var collisionPush = post - pre;

            // No collision happened on this vertex
            if (math.lengthsq(collisionPush) < 1e-8f) return;

            // Collision normal = direction the collision pushed the vertex out
            var normal = math.normalize(collisionPush);

            // Total motion this substep: from start-of-substep position to post-collision
            var totalMotion = post - OriginalPositions[i];

            // Decompose into normal and tangential components
            var normalMotion = math.dot(totalMotion, normal) * normal;
            var tangentialMotion = totalMotion - normalMotion;

            // Apply friction: reduce tangential sliding
            PredictedPositions[i] = OriginalPositions[i] + normalMotion + tangentialMotion * (1f - Friction);
        }
    }
}