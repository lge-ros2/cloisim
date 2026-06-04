/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using Unity.Mathematics;
using System.Collections.Generic;

namespace CLOiSim.Cloth
{
    [RequireComponent(typeof(BurstCloth))]
    public class ColliderBridge : MonoBehaviour
    {
        [Header("Scene Colliders")]
        [Tooltip("Add GameObjects with SphereCollider, BoxCollider, CapsuleCollider, or MeshCollider here.")]
        public Transform[] SceneColliders;

        private BurstCloth _simulator;
        private ClothCollider[] _clothColliders;
        private bool _initialized = false;

        // Caches to hold mesh data
        private struct MeshCache
        {
            public int TriStart;
            public int TriCount;
            public float3 BoundsMin;
            public float3 BoundsMax;
        }
        private MeshCache[] _meshCaches;

        void Start()
        {
            if (_initialized) return;

            _simulator = GetComponent<BurstCloth>();
            _clothColliders = new ClothCollider[SceneColliders != null ? SceneColliders.Length : 0];

            BakeMeshColliders();
            _initialized = true;
        }

        public void Initialize(BurstCloth simulator, Transform[] colliders)
        {
            _simulator = simulator;
            SceneColliders = colliders;
            _clothColliders = new ClothCollider[colliders != null ? colliders.Length : 0];
            _meshCaches = new MeshCache[colliders != null ? colliders.Length : 0];
            BakeMeshColliders();
            _initialized = true;
        }

        public void UpdateSceneColliders(Transform[] colliders)
        {
            if (_simulator == null) return;

            SceneColliders = colliders;
            _clothColliders = new ClothCollider[colliders != null ? colliders.Length : 0];
            _meshCaches = new MeshCache[colliders != null ? colliders.Length : 0];
            BakeMeshColliders();
        }

        private void BakeMeshColliders()
        {
            if (SceneColliders == null) return;

            List<float3> allVertices = new List<float3>();
            List<int> allTriangles = new List<int>();
            _meshCaches = new MeshCache[SceneColliders.Length];

            for (int i = 0; i < SceneColliders.Length; i++)
            {
                if (SceneColliders[i] != null && SceneColliders[i].TryGetComponent(out MeshCollider meshCol) && meshCol.sharedMesh != null)
                {
                    Mesh mesh = meshCol.sharedMesh;
                    Vector3[] verts = mesh.vertices;
                    int[] tris = mesh.triangles;

                    _meshCaches[i] = new MeshCache
                    {
                        TriStart = allTriangles.Count,
                        TriCount = tris.Length,
                        BoundsMin = mesh.bounds.min,
                        BoundsMax = mesh.bounds.max
                    };

                    int vertexOffset = allVertices.Count;
                    foreach (var v in verts) allVertices.Add(v);
                    foreach (var t in tris) allTriangles.Add(t + vertexOffset);
                }
            }

            if (allVertices.Count > 0)
            {
                _simulator.SetMeshData(allVertices.ToArray(), allTriangles.ToArray());
            }
        }

        void Update()
        {
            if (_simulator == null || SceneColliders == null || SceneColliders.Length == 0) return;

            if (_clothColliders.Length != SceneColliders.Length)
            {
                _clothColliders = new ClothCollider[SceneColliders.Length];
            }

            for (int i = 0; i < SceneColliders.Length; i++)
            {
                Transform t = SceneColliders[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                if (t.TryGetComponent(out SphereCollider sphere))
                {
                    _clothColliders[i] = new ClothCollider
                    {
                        Type = ColliderType.Sphere,
                        Position = t.TransformPoint(sphere.center),
                        Rotation = t.rotation,
                        Scale = new float3(sphere.radius * t.lossyScale.x, 0, 0) // Only x is used for radius
                    };
                }
                else if (t.TryGetComponent(out BoxCollider box))
                {
                    var halfExtX = box.size.x * 0.5f * Mathf.Abs(t.lossyScale.x);
                    var halfExtY = box.size.y * 0.5f * Mathf.Abs(t.lossyScale.y);
                    var halfExtZ = box.size.z * 0.5f * Mathf.Abs(t.lossyScale.z);
                    var maxHalfExt = Mathf.Max(halfExtX, Mathf.Max(halfExtY, halfExtZ));
                    var minHalfExt = Mathf.Min(halfExtX, Mathf.Min(halfExtY, halfExtZ));
                    var midHalfExt = (halfExtX + halfExtY + halfExtZ) - maxHalfExt - minHalfExt;

                    // A box that is very flat in one dimension (e.g. a floor or wall panel) is
                    // treated as a one-sided half-space plane.  This prevents cloth particles from
                    // tunneling through thin geometry at high velocities, because the half-space
                    // test is valid regardless of how far the particle has moved per sub-step.
                    if (minHalfExt < maxHalfExt * 0.1f && midHalfExt > 0.5f)
                    {
                        // Pick the world-space normal along the thinnest axis.
                        float3 normal;
                        if (halfExtX <= halfExtY && halfExtX <= halfExtZ)
                            normal = (float3)t.right;
                        else if (halfExtZ <= halfExtX && halfExtZ <= halfExtY)
                            normal = (float3)t.forward;
                        else
                            normal = (float3)t.up;

                        _clothColliders[i] = new ClothCollider
                        {
                            Type = ColliderType.Plane,
                            Position = t.TransformPoint(box.center),
                            Scale = normal, // unit normal stored in Scale; other fields unused
                        };
                    }
                    else
                    {
                        _clothColliders[i] = new ClothCollider
                        {
                            Type = ColliderType.Box,
                            Position = t.TransformPoint(box.center),
                            Rotation = t.rotation,
                            Scale = (float3)box.size * 0.5f * (float3)t.lossyScale // Half-extents
                        };
                    }
                }
                else if (t.TryGetComponent(out CapsuleCollider capsule))
                {
                    float radius = capsule.radius * math.max(t.lossyScale.x, t.lossyScale.z);
                    float halfHeight = math.max(0f, (capsule.height * t.lossyScale.y * 0.5f) - radius);

                    _clothColliders[i] = new ClothCollider
                    {
                        Type = ColliderType.Capsule,
                        Position = t.TransformPoint(capsule.center),
                        Rotation = t.rotation,
                        Scale = new float3(radius, halfHeight, 0)
                    };
                }
                else if (t.TryGetComponent(out MeshCollider meshCollider))
                {
                    _clothColliders[i] = new ClothCollider
                    {
                        Type = ColliderType.Mesh,
                        Position = t.position,
                        Rotation = t.rotation,
                        Scale = t.lossyScale,
                        TriStart = _meshCaches[i].TriStart,
                        TriCount = _meshCaches[i].TriCount,
                        BoundsMin = _meshCaches[i].BoundsMin,
                        BoundsMax = _meshCaches[i].BoundsMax
                    };
                }
            }

            _simulator.UpdateColliders(_clothColliders);
        }
    }
}