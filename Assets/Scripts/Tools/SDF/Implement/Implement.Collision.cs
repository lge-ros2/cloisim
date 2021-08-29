/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using MCCookingOptions = UnityEngine.MeshColliderCookingOptions;

namespace SDF
{
	public partial class Implement
	{
		public class Collision
		{
			public static readonly int PlaneLayerIndex = UE.LayerMask.NameToLayer("Plane");

			private static readonly bool EnableMergeCollider = true;

			private static readonly float ThresholdFrictionCombineMultiply = 0.01f;
			private static readonly float DynamicFrictionRatio = 0.90f;

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

			public static void Make(UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();

				if (targetObject.GetComponent<UE.Collider>() == null)
				{
					KeepUnmergedMeshes(meshFilters);

					if (EnableMergeCollider)
					{
						var geometryWorldToLocalMatrix = targetObject.transform.worldToLocalMatrix;
						var meshColliders = targetObject.GetComponentsInChildren<UE.MeshCollider>();
						var mergedMesh = SDF2Unity.MergeMeshes(meshColliders, geometryWorldToLocalMatrix);

						var transformObjects = targetObject.GetComponentsInChildren<UE.Transform>();
						for (var index = 1; index < transformObjects.Length; index++)
						{
							UE.GameObject.Destroy(transformObjects[index].gameObject);
						}

						for (var index = 0; index < meshColliders.Length; index++)
						{
							UE.GameObject.Destroy(meshColliders[index]);
						}

						var combinedMeshCollider = targetObject.AddComponent<UE.MeshCollider>();
						combinedMeshCollider.sharedMesh = mergedMesh;
						combinedMeshCollider.convex = false;
						combinedMeshCollider.cookingOptions = CookingOptions;
						combinedMeshCollider.hideFlags |= UE.HideFlags.NotEditable;
					}
				}

				for (var i = 0; i < meshFilters.Length; i++)
				{
					var meshRenderer = meshFilters[i].GetComponent<UE.MeshRenderer>();
					if (meshRenderer != null)
					{
						UE.GameObject.Destroy(meshRenderer);
					}
					UE.GameObject.Destroy(meshFilters[i]);
				}
			}

			public static void SetSurfaceFriction(in SDF.Surface surface, in UE.GameObject targetObject)
			{
				var material = new UE.PhysicMaterial();

				if (surface != null)
				{
					material.name = "SDF Surface Friction";

					material.bounciness = (surface.bounce == null)? 0:(float)surface.bounce.restitution_coefficient;

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