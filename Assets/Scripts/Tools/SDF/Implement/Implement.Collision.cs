/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
#define ENABLE_MERGE_COLLIDER

using UE = UnityEngine;
using MCCookingOptions = UnityEngine.MeshColliderCookingOptions;
using MeshProcess;

namespace SDF
{
	namespace Implement
	{
		public class Collision
		{
			public static readonly int PlaneLayerIndex = UE.LayerMask.NameToLayer("Plane");

			private static readonly int NumOfLimitConvexMeshTriangles = 255;

			private static readonly bool UseVHACD = true; // Expreimental parameters

			private static VHACD.Parameters VHACDParams = new VHACD.Parameters()
			{
				m_resolution = 18000,
				// m_resolution = 800000, // max: 64,000,000
				m_concavity = 0.005,
				m_planeDownsampling = 3,
				m_convexhullDownsampling = 3,
				m_alpha = 0.1,
				m_beta = 0.05,
				m_pca = 0,
				m_mode = 0,
				m_maxNumVerticesPerCH = 128,
				m_minVolumePerCH = 0.0005,
				m_convexhullApproximation = 1,
				m_oclAcceleration = 0,
				m_maxConvexHulls = 256,
				m_projectHullVertices = true
			};

			private static readonly float ThresholdFrictionCombineMultiply = 0.01f;
			private static readonly float DynamicFrictionRatio = 0.95f;

			private static readonly MCCookingOptions CookingOptions =
					MCCookingOptions.EnableMeshCleaning |
					MCCookingOptions.CookForFasterSimulation |
					MCCookingOptions.WeldColocatedVertices |
					MCCookingOptions.UseFastMidphase;

			private static void KeepUnmergedMeshes(in UE.MeshFilter[] meshFilters)
			{
				foreach (var meshFilter in meshFilters)
				{
					var meshObject = meshFilter.gameObject;
					var meshCollider = meshObject.AddComponent<UE.MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					meshCollider.convex = false;
					meshCollider.cookingOptions = CookingOptions;
					meshCollider.hideFlags |= UE.HideFlags.NotEditable;
				}
			}

			private static void ApplyVHACD(in UE.GameObject targetObject, in UE.MeshFilter[] meshFilters)
			{
				var decomposer = targetObject.AddComponent<VHACD>();
				decomposer.m_parameters = VHACDParams;

				foreach (var meshFilter in meshFilters)
				{
					// Just skip if the number of vertices in the mesh is less than the limit of convex mesh triangles
					if (meshFilter.sharedMesh.vertexCount >= NumOfLimitConvexMeshTriangles)
					{
						// #if ENABLE_MERGE_COLLIDER
						// 						UE.Debug.LogFormat("Apply VHACD({0}), EnableMergeCollider will be ignored.", targetObject.name);
						// #else
						// 						UE.Debug.LogFormat("Apply VHACD({0})", targetObject.name);
						// #endif
						var colliderMeshes = decomposer.GenerateConvexMeshes(meshFilter.sharedMesh);

						var index = 0;
						foreach (var collider in colliderMeshes)
						{
							var currentMeshCollider = targetObject.AddComponent<UE.MeshCollider>();
							collider.name = "VHACD_" + meshFilter.name + "_" + (index++);
							// UE.Debug.Log(collider.name);
							currentMeshCollider.sharedMesh = collider;
							currentMeshCollider.convex = false;
							currentMeshCollider.cookingOptions = CookingOptions;
							currentMeshCollider.hideFlags |= UE.HideFlags.NotEditable;
						}
					}
					else
					{
						var meshCollider = targetObject.AddComponent<UE.MeshCollider>();
						meshCollider.sharedMesh = meshFilter.sharedMesh;
						meshCollider.convex = false;
						meshCollider.cookingOptions = CookingOptions;
						meshCollider.hideFlags |= UE.HideFlags.NotEditable;
					}
					UE.GameObject.Destroy(meshFilter.gameObject);
				}

				UE.Component.Destroy(decomposer);
			}

#if ENABLE_MERGE_COLLIDER
			private static void MergeCollider(in UE.GameObject targetObject)
			{
				var geometryWorldToLocalMatrix = targetObject.transform.worldToLocalMatrix;
				var meshColliders = targetObject.GetComponentsInChildren<UE.MeshCollider>();
				var mergedMesh = SDF2Unity.MergeMeshes(meshColliders, geometryWorldToLocalMatrix);

				for (var index = 0; index < meshColliders.Length; index++)
				{
					UE.GameObject.Destroy(meshColliders[index]);
				}

				var transformObjects = targetObject.GetComponentsInChildren<UE.Transform>();
				for (var index = 1; index < transformObjects.Length; index++)
				{
					UE.GameObject.Destroy(transformObjects[index].gameObject);
				}

				var mergedMeshCollider = targetObject.AddComponent<UE.MeshCollider>();
				mergedMeshCollider.sharedMesh = mergedMesh;
				mergedMeshCollider.convex = false;
				mergedMeshCollider.cookingOptions = CookingOptions;
				mergedMeshCollider.hideFlags |= UE.HideFlags.NotEditable;
			}
#endif

			public static void Make(UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();

				if (targetObject.GetComponent<UE.Collider>() == null)
				{
					var modelHelper = targetObject.GetComponentInParent<SDF.Helper.Model>();
					// UE.Debug.Log(modelHelper);

					// Skip for Primitive Mesh or static model
					if (UseVHACD && targetObject.name != "Primitive Mesh" && modelHelper.isStatic == false)
					{
						ApplyVHACD(targetObject, meshFilters);
					}
					else
					{
						KeepUnmergedMeshes(meshFilters);

#if ENABLE_MERGE_COLLIDER
						MergeCollider(targetObject);
#endif
					}
				}

				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.GetComponent<UE.MeshRenderer>();
					if (meshRenderer != null)
					{
						UE.GameObject.Destroy(meshRenderer);
					}
					UE.GameObject.Destroy(meshFilter);
				}
			}

			public static void SetSurfaceFriction(in SDF.Surface surface, in UE.GameObject targetObject)
			{
				var material = new UE.PhysicMaterial();

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
							material.frictionCombine = ((float)surface.friction.ode.mu2 <= ThresholdFrictionCombineMultiply) ? UE.PhysicMaterialCombine.Multiply : UE.PhysicMaterialCombine.Average;
						}
					}
					else
					{
						material.frictionCombine = UE.PhysicMaterialCombine.Average;
					}

					material.bounceCombine = UE.PhysicMaterialCombine.Average;
				}
				else
				{
					// Stone Material
					material.name = "Stone";
					material.dynamicFriction = 0.6f;
					material.staticFriction = 0.6f;
					material.bounciness = 0.0f;
					material.frictionCombine = UE.PhysicMaterialCombine.Average;
					material.bounceCombine = UE.PhysicMaterialCombine.Average;

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