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

			private static void KeepUnmergedMeshes(ref UE.MeshFilter[] meshFilters)
			{
				foreach (var meshFilter in meshFilters)
				{
					var meshObject = meshFilter.gameObject;
					var meshCollider = meshObject.AddComponent<UE.MeshCollider>();
					meshCollider.sharedMesh = meshFilter.sharedMesh;
					meshCollider.convex = false;
					meshCollider.cookingOptions = CookingOptions;
					// meshCollider.hideFlags |= UE.HideFlags.NotEditable;
				}
			}

			private static void MergeCollider(this UE.GameObject targetObject)
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