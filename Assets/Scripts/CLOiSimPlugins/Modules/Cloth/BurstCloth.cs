/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
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

    /// <summary>
    /// High-performance cloth simulator utilizing Position-Based Dynamics (PBD) and Burst.
    /// </summary>
    public class BurstCloth : MonoBehaviour
    {
        [Header("Simulation Settings")]
        public int SolverIterations = 5;
        public float3 Gravity = new float3(0, -9.81f, 0);
        public float Damping = 0.98f;
        public float ParticleRadius = 0.005f;

        // Native collections for Burst Jobs
        private NativeArray<float3> _positions;
        private NativeArray<float3> _predictedPositions;
        private NativeArray<float3> _velocities;
        private NativeArray<float> _inverseMasses;
        private NativeArray<DistanceConstraint> _constraints;
        private NativeArray<ClothCollider> _colliders;
        private NativeArray<float3> _meshVertices;
        private NativeArray<int> _meshTriangles;
        private NativeArray<float3> _initialPositions;

        private JobHandle _clothJobHandle;
        private bool _isInitialized = false;
        private int _colliderCount = 0;

        // Call this to setup the cloth topology
        public void Initialize(float3[] vertices, float[] masses, DistanceConstraint[] structuralConstraints)
        {
            int count = vertices.Length;
            _positions = new(count, Allocator.Persistent);
            _predictedPositions = new(count, Allocator.Persistent);
            _velocities = new(count, Allocator.Persistent);
            _inverseMasses = new(count, Allocator.Persistent);
            _initialPositions = new(count, Allocator.Persistent);
            _constraints = new(structuralConstraints.Length, Allocator.Persistent);

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
            }
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
            if (dt <= 0f) return;

            // Wait for previous frame jobs just in case an error occurred mid-frame
            _clothJobHandle.Complete();

            // 1. Integrate forces (Gravity)
            var integrateJob = new IntegrateJob
            {
                Positions = _positions,
                PredictedPositions = _predictedPositions,
                Velocities = _velocities,
                InverseMasses = _inverseMasses,
                Gravity = Gravity,
                DeltaTime = dt,
                Damping = Damping
            };
            JobHandle integrateHandle = integrateJob.Schedule(_positions.Length, 64);

            // 2. Solve Distance Constraints & Collisions iteratively
            JobHandle solverHandle = integrateHandle;
            for (int i = 0; i < SolverIterations; i++)
            {
                var constraintJob = new SolveDistanceConstraintsJob
                {
                    PredictedPositions = _predictedPositions,
                    InverseMasses = _inverseMasses,
                    Constraints = _constraints
                };
                // Note: For extreme performance, constraints should be graph-colored to use IJobParallelFor.
                // Using IJob (single thread) here prevents race conditions on shared vertices.
                solverHandle = constraintJob.Schedule(solverHandle);

                var collisionJob = new SolveCollisionsJob
                {
                    PredictedPositions = _predictedPositions,
                    Colliders = _colliders,
                    ColliderCount = _colliderCount,
                    MeshVertices = _meshVertices,
                    MeshTriangles = _meshTriangles,
                    ParticleRadius = ParticleRadius
                };
                solverHandle = collisionJob.Schedule(_positions.Length, 64, solverHandle);
            }

            // 3. Update Velocities and Final Positions
            var updateJob = new UpdateVelocitiesJob
            {
                Positions = _positions,
                PredictedPositions = _predictedPositions,
                Velocities = _velocities,
                InverseMasses = _inverseMasses,
                DeltaTime = dt
            };
            _clothJobHandle = updateJob.Schedule(_positions.Length, 64, solverHandle);

            // Wait for jobs to finish before reading positions
            // In a fully optimized system, you wait at the *start* of the next frame.
            _clothJobHandle.Complete();

            // TODO: Apply _positions to your Mesh or LineRenderer here.
        }

        private void OnDestroy()
        {
            _clothJobHandle.Complete();
            if (_positions.IsCreated) _positions.Dispose();
            if (_predictedPositions.IsCreated) _predictedPositions.Dispose();
            if (_velocities.IsCreated) _velocities.Dispose();
            if (_inverseMasses.IsCreated) _inverseMasses.Dispose();
            if (_initialPositions.IsCreated) _initialPositions.Dispose();
            if (_constraints.IsCreated) _constraints.Dispose();
            if (_colliders.IsCreated) _colliders.Dispose();
            if (_meshVertices.IsCreated) _meshVertices.Dispose();
            if (_meshTriangles.IsCreated) _meshTriangles.Dispose();
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
            float particleRadius = ParticleRadius;

            for (int c = 0; c < ColliderCount; c++)
            {
                var collider = Colliders[c];

                if (collider.Type == ColliderType.Sphere)
                {
                    float3 delta = p - collider.Position;
                    float distSq = math.lengthsq(delta);
                    float radius = collider.Scale.x + particleRadius;

                    if (distSq < radius * radius && distSq > 1e-6f)
                    {
                        float dist = math.sqrt(distSq);
                        p = collider.Position + (delta / dist) * radius;
                    }
                }
                else if (collider.Type == ColliderType.Box)
                {
                    // Transform point to box local space
                    float3 localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
                    float3 extents = collider.Scale + particleRadius; // Half-size + padding

                    // Check if inside the box
                    if (math.abs(localP.x) < extents.x &&
                        math.abs(localP.y) < extents.y &&
                        math.abs(localP.z) < extents.z)
                    {
                        // Find closest face to push out
                        float3 dists = extents - math.abs(localP);

                        if (dists.x < dists.y && dists.x < dists.z)
                            localP.x = (localP.x >= 0f ? 1f : -1f) * extents.x;
                        else if (dists.y < dists.z)
                            localP.y = (localP.y >= 0f ? 1f : -1f) * extents.y;
                        else
                            localP.z = (localP.z >= 0f ? 1f : -1f) * extents.z;

                        // Transform back to world space
                        p = collider.Position + math.mul(collider.Rotation, localP);
                    }
                }
                else if (collider.Type == ColliderType.Capsule)
                {
                    // Local Y-axis defines the capsule's height segment
                    float3 up = math.mul(collider.Rotation, new float3(0f, 1f, 0f));
                    // Scale.y is the half-height of the cylinder part, Scale.x is the radius
                    float3 pA = collider.Position + up * collider.Scale.y;
                    float3 pB = collider.Position - up * collider.Scale.y;

                    float3 ab = pB - pA;
                    float3 ap = p - pA;
                    float abSq = math.lengthsq(ab);

                    // Find the closest point on the line segment AB to point P
                    float t = abSq > 1e-5f ? math.saturate(math.dot(ap, ab) / abSq) : 0f;
                    float3 closest = pA + t * ab;

                    float3 delta = p - closest;
                    float distSq = math.lengthsq(delta);
                    float radius = collider.Scale.x + particleRadius;

                    if (distSq < radius * radius && distSq > 1e-6f)
                    {
                        float dist = math.sqrt(distSq);
                        p = closest + (delta / dist) * radius;
                    }
                }
                else if (collider.Type == ColliderType.Mesh)
                {
                    // Transform point to mesh local space
                    float3 localP = math.mul(math.inverse(collider.Rotation), p - collider.Position);
                    localP /= collider.Scale;

                    // AABB Early Out check (padding added for cloth particle radius)
                    float padding = 0.1f;
                    if (localP.x < collider.BoundsMin.x - padding || localP.x > collider.BoundsMax.x + padding ||
                        localP.y < collider.BoundsMin.y - padding || localP.y > collider.BoundsMax.y + padding ||
                        localP.z < collider.BoundsMin.z - padding || localP.z > collider.BoundsMax.z + padding)
                    {
                        continue;
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

                    float localRadius = particleRadius;
                    if (hit && minDistSq < localRadius * localRadius && minDistSq > 1e-8f)
                    {
                        float dist = math.sqrt(minDistSq);
                        float3 dir = (localP - bestLocalPush) / dist;
                        localP = bestLocalPush + dir * localRadius;

                        // Transform back to world space
                        p = collider.Position + math.mul(collider.Rotation, localP * collider.Scale);
                    }
                }
            }

            PredictedPositions[i] = p;
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
        public NativeArray<float3> Velocities;
        [ReadOnly] public NativeArray<float> InverseMasses;
        public float DeltaTime;

        public void Execute(int i)
        {
            if (InverseMasses[i] == 0f) return; // Pinned

            // Compute velocity from the PBD correction
            Velocities[i] = (PredictedPositions[i] - Positions[i]) / DeltaTime;
            Positions[i] = PredictedPositions[i];
        }
    }
}