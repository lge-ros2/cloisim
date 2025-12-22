/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
#define ENABLE_MERGE_COLLIDER

using UE = UnityEngine;
using MCCookingOptions = UnityEngine.MeshColliderCookingOptions;

namespace SDF
{
	namespace Implement
	{
		public static class Collision
		{
			public static readonly int PlaneLayerIndex = UE.LayerMask.NameToLayer("Plane");

			private static readonly bool UseVHACD = true; // Experimental parameters

			private static readonly float ThresholdFrictionCombineMultiply = 0.01f;
			private static readonly float DynamicFrictionRatio = 0.95f;

			public static readonly MCCookingOptions CookingOptions =
					MCCookingOptions.EnableMeshCleaning |
					// MCCookingOptions.InflateConvexMesh|
					MCCookingOptions.CookForFasterSimulation |
					MCCookingOptions.WeldColocatedVertices |
					MCCookingOptions.UseFastMidphase;

			private static readonly float DegenerateTriangleArea = 1e-14f;

			/// <summary>
			/// Remove degenerate triangles (zero/near-zero area) from a mesh
			/// to prevent PhysX "cleaning the mesh failed" warnings.
			/// </summary>
			private static UE.Mesh CleanMesh(UE.Mesh source)
			{
				if (source == null)
					return source;

				var srcVertices = source.vertices;
				var srcTriangles = source.triangles;

				if (srcTriangles.Length < 3)
					return null;

				var cleanIndices = new System.Collections.Generic.List<int>(srcTriangles.Length);

				for (var i = 0; i < srcTriangles.Length; i += 3)
				{
					var v0 = srcVertices[srcTriangles[i]];
					var v1 = srcVertices[srcTriangles[i + 1]];
					var v2 = srcVertices[srcTriangles[i + 2]];

					// Check for degenerate triangle (zero area via cross product magnitude)
					var cross = UE.Vector3.Cross(v1 - v0, v2 - v0);
					if (cross.sqrMagnitude <= DegenerateTriangleArea)
						continue;

					cleanIndices.Add(srcTriangles[i]);
					cleanIndices.Add(srcTriangles[i + 1]);
					cleanIndices.Add(srcTriangles[i + 2]);
				}

				if (cleanIndices.Count == srcTriangles.Length)
					return source; // no degenerate triangles found

				var removedCount = (srcTriangles.Length - cleanIndices.Count) / 3;
				UE.Debug.LogWarning($"CleanMesh({source.name}): removed {removedCount} degenerate triangle(s)");

				if (cleanIndices.Count == 0)
				{
					UE.Debug.LogWarning($"CleanMesh({source.name}): all triangles are degenerate, skipping");
					return null;
				}

				var cleanMesh = new UE.Mesh();
				cleanMesh.name = source.name;
				cleanMesh.indexFormat = source.indexFormat;
				cleanMesh.vertices = srcVertices;
				cleanMesh.normals = source.normals;
				cleanMesh.uv = source.uv;
				cleanMesh.triangles = cleanIndices.ToArray();
				cleanMesh.RecalculateBounds();

				return cleanMesh;
			}

			private static void KeepUnmergedMeshes(ref UE.MeshFilter[] meshFilters)
			{
				foreach (var meshFilter in meshFilters)
				{
					var cleanedMesh = CleanMesh(meshFilter.sharedMesh);
					if (cleanedMesh == null)
						continue;

					var meshObject = meshFilter.gameObject;
					var meshCollider = meshObject.AddComponent<UE.MeshCollider>();
					meshCollider.sharedMesh = cleanedMesh;
					meshCollider.convex = false;
					meshCollider.cookingOptions = CookingOptions;
				}
			}

			private static void MergeCollider(this UE.GameObject targetObject)
			{
				var geometryWorldToLocalMatrix = targetObject.transform.worldToLocalMatrix;
				var meshColliders = targetObject.GetComponentsInChildren<UE.MeshCollider>();

				if (meshColliders.Length == 0)
					return;

				var mergedMesh = meshColliders.MergeMeshes(geometryWorldToLocalMatrix);

				for (var index = 0; index < meshColliders.Length; index++)
				{
					UE.GameObject.Destroy(meshColliders[index]);
				}

				var transformObjects = targetObject.GetComponentsInChildren<UE.Transform>();
				for (var index = 1; index < transformObjects.Length; index++)
				{
					UE.GameObject.Destroy(transformObjects[index].gameObject);
				}

				var cleanedMergedMesh = CleanMesh(mergedMesh);
				if (cleanedMergedMesh != null)
				{
					var mergedMeshCollider = targetObject.AddComponent<UE.MeshCollider>();
					mergedMeshCollider.sharedMesh = cleanedMergedMesh;
					mergedMeshCollider.convex = false;
					mergedMeshCollider.cookingOptions = CookingOptions;
					mergedMeshCollider.hideFlags |= UE.HideFlags.NotEditable;
				}
			}

			public static void MakeCollision(this UE.GameObject targetObject)
			{
				var modelHelper = targetObject.GetComponentInParent<SDF.Helper.Model>();
				// UE.Debug.Log(modelHelper.name + " MakeCollision");

				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();

				// Skip for Primitive Mesh or static model
				if (UseVHACD &&
					targetObject.name != "Primitive Mesh" &&
					modelHelper.isStatic == false)
				{
					VHACD.Apply(meshFilters);
				}
				else
				{
					if (targetObject.GetComponent<UE.Collider>() == null)
					{
						KeepUnmergedMeshes(ref meshFilters);

#if ENABLE_MERGE_COLLIDER
						targetObject.MergeCollider();
#endif
					}
				}

				RemoveRenderers(ref meshFilters);
			}

			private static void RemoveRenderers(ref UE.MeshFilter[] meshFilters)
			{
				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.GetComponent<UE.MeshRenderer>();
					if (meshRenderer != null)
					{
						// UE.Debug.LogWarning($"{meshFilter.name} MeshRenderer should not exist");
						UE.GameObject.Destroy(meshRenderer);
					}
					UE.GameObject.Destroy(meshFilter);
				}
			}

			public static void SetSurfaceFriction(this UE.GameObject targetObject, in SDF.Surface surface)
			{
				var material = new UE.PhysicsMaterial();

				if (surface != null)
				{
					material.name = "SDF Surface Friction";

					material.bounciness = (surface.bounce == null) ? 0 : (float)surface.bounce.restitution_coefficient;

					if (surface.friction != null)
					{
						if (surface.friction.ode != null)
						{
							material.staticFriction = (float)surface.friction.ode.mu;
							material.dynamicFriction = (float)surface.friction.ode.mu * DynamicFrictionRatio;
							material.frictionCombine = ((float)surface.friction.ode.mu2 <= ThresholdFrictionCombineMultiply) ? UE.PhysicsMaterialCombine.Multiply : UE.PhysicsMaterialCombine.Average;
						}
					}
					else
					{
						material.frictionCombine = UE.PhysicsMaterialCombine.Average;
					}

					material.bounceCombine = UE.PhysicsMaterialCombine.Average;
				}
				else
				{
					// Stone Material
					material.name = "Stone";
					material.dynamicFriction = 0.6f;
					material.staticFriction = 0.6f;
					material.bounciness = 0.0f;
					material.frictionCombine = UE.PhysicsMaterialCombine.Average;
					material.bounceCombine = UE.PhysicsMaterialCombine.Average;

#if false
					// Rubber Material
					material.name = "Rubber";
					material.dynamicFriction = 0.8f;
					material.staticFriction = 0.9f;
					material.bounciness = 0.8f;
					material.frictionCombine = UE.PhysicMaterialCombine.Maximum;
					material.bounceCombine = UE.PhysicMaterialCombine.Average;

					// Ice Material
					material.name = "Ice";
					material.dynamicFriction = 0.05f;
					material.staticFriction = 0.1f;
					material.bounciness = 0.05f;
					material.frictionCombine = UE.PhysicMaterialCombine.Multiply;
					material.bounceCombine = UE.PhysicMaterialCombine.Multiply;

					// Wood Material
					material.name = "Wood";
					material.dynamicFriction = 0.475f;
					material.staticFriction = 0.475f;
					material.bounciness = 0f;
					material.frictionCombine = UE.PhysicMaterialCombine.Average;
					material.bounceCombine = UE.PhysicMaterialCombine.Average;

					// Metal Material
					material.name = "Metal";
					material.dynamicFriction = 0.15f;
					material.staticFriction = 0.2f;
					material.bounciness = 0f;
					material.frictionCombine = UE.PhysicMaterialCombine.Minimum;
					material.bounceCombine = UE.PhysicMaterialCombine.Average;

					// Mud Material
					material.name = "Mud";
					material.dynamicFriction = 1f;
					material.staticFriction = 0.9f;
					material.bounciness = 0f;
					material.frictionCombine = UE.PhysicMaterialCombine.Minimum;
					material.bounceCombine = UE.PhysicMaterialCombine.Minimum;
#endif

					material.name = "(default) " + material.name;
				}

				// Set physics materials
				foreach (var meshCollider in targetObject.GetComponentsInChildren<UE.Collider>())
				{
					meshCollider.material = material;
				}
			}
		}
	}
}