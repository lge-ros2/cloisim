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

			private static readonly MCCookingOptions CookingOptions =
					MCCookingOptions.EnableMeshCleaning |
					MCCookingOptions.CookForFasterSimulation |
					MCCookingOptions.WeldColocatedVertices |
					MCCookingOptions.UseFastMidphase;

			private static UE.Mesh MergeMeshes(in UE.MeshFilter[] meshFilters)
			{
				var meshTransformMatrix = new UE.Matrix4x4();

				var combine = new UE.CombineInstance[meshFilters.Length];
				var combineIndex = 0;
				foreach (var meshFilter in meshFilters)
				{
					combine[combineIndex].mesh = meshFilter.sharedMesh;

					var meshTranslation = meshFilter.transform.localPosition;
					var meshRotation = meshFilter.transform.localRotation;
					var meshScale = meshFilter.transform.localScale;
					// Debug.LogFormat("{0},{1}: {2}, {3}", meshFilter.name, meshFilter.transform.name, meshTranslation, meshRotation);

					meshTransformMatrix.SetTRS(meshTranslation, meshRotation, meshScale);

					combine[combineIndex].transform = meshTransformMatrix;
					combineIndex++;
				}

				var newCombinedMesh = new UE.Mesh();
				newCombinedMesh.CombineMeshes(combine, true);
				newCombinedMesh.Optimize();
				newCombinedMesh.RecalculateTangents();
				newCombinedMesh.RecalculateBounds();
				newCombinedMesh.RecalculateNormals();

				return newCombinedMesh;
			}

			private static void KeepUnmergedMeshes(in UE.MeshFilter[] meshFilters)
			{
				foreach (var meshFilter in meshFilters)
				{
					var meshObject = meshFilter.gameObject;
					var meshCollider = meshObject.AddComponent<UE.MeshCollider>();

					meshCollider.sharedMesh = meshFilter.sharedMesh;
					meshCollider.convex = true;
					meshCollider.cookingOptions = CookingOptions;
					meshCollider.hideFlags |= UE.HideFlags.NotEditable;

					if (meshObject.TryGetComponent<UE.MeshRenderer>(out var meshRenderer))
					{
						UE.GameObject.Destroy(meshRenderer);
					}

					UE.GameObject.Destroy(meshFilter);
				}
			}

			public static void Make(UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();

				if (EnableMergeCollider)
				{
					var mergedMesh = MergeMeshes(meshFilters);
					mergedMesh.name = targetObject.name;

					// remove all child objects after merge the meshes for colloision
					if (targetObject.transform.childCount > 0)
					{
						foreach (var meshFilter in meshFilters)
						{
							// UE.Debug.Log(childGeometry.gameObject.name);
							UE.GameObject.Destroy(meshFilter.gameObject);
						}
					}
					else
					{
						if (targetObject.TryGetComponent<UE.MeshRenderer>(out var meshRenderer))
						{
							UE.GameObject.Destroy(meshRenderer);
						}

						if (targetObject.TryGetComponent<UE.MeshFilter>(out var meshFilter))
						{
							UE.GameObject.Destroy(meshFilter);
						}
					}

					var meshCollider = targetObject.AddComponent<UE.MeshCollider>();
					meshCollider.sharedMesh = mergedMesh;
					meshCollider.convex = true;
					meshCollider.cookingOptions = CookingOptions;
					meshCollider.hideFlags |= UE.HideFlags.NotEditable;
				}
				else
				{
					KeepUnmergedMeshes(meshFilters);
				}
			}

			public static void SetPhysicalMaterial(in SDF.Surface surface, in UE.GameObject targetObject)
			{
				var material = new UE.PhysicMaterial();

				if (surface != null)
				{
					material.name = "SDF Surface Friction";
					if (surface.friction != null)
					{
						if (surface.friction.ode != null)
						{
							material.staticFriction = (float)surface.friction.ode.mu;
							material.dynamicFriction = (float)surface.friction.ode.mu * 0.70f;
						}
					}

					material.bounciness = (surface.bounce == null)? 0:(float)surface.bounce.restitution_coefficient;
					material.frictionCombine = UE.PhysicMaterialCombine.Average;
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
				foreach (var meshCollider in targetObject.GetComponentsInChildren<UE.MeshCollider>())
				{
					meshCollider.material = material;
				}
			}
		}
	}
}