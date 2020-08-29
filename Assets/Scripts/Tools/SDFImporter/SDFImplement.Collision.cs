/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

[RequireComponent (typeof(MeshFilter))]
[RequireComponent (typeof(MeshRenderer))]
[RequireComponent (typeof(MeshCollider))]

public partial class SDFImplement
{
	public class Collision
	{
		private static bool enableMergeCollider = true;
		private static MeshColliderCookingOptions cookingOptions = MeshColliderCookingOptions.EnableMeshCleaning|MeshColliderCookingOptions.WeldColocatedVertices;

		private static Mesh MergeMeshes(in MeshFilter[] meshFilters)
		{
			var combine = new CombineInstance[meshFilters.Length];
			var combineIndex = 0;
			foreach (var meshFilter in meshFilters)
			{
				combine[combineIndex].mesh = meshFilter.sharedMesh;

				var meshTransformMatrix = new Matrix4x4();
				var meshTranslation = meshFilter.transform.localPosition;
				var meshRotation = meshFilter.transform.localRotation;

				// Debug.LogFormat("{0},{1}: {2}, {3}", meshFilter.name, meshFilter.transform.name, meshTranslation, meshRotation);
				var meshScale = meshFilter.transform.localScale;
				meshTransformMatrix.SetTRS(meshTranslation, meshRotation, meshScale);

				combine[combineIndex].transform = meshTransformMatrix;
				combineIndex++;

				if (meshFilter.TryGetComponent<MeshRenderer>(out var meshRenderer))
				{
					GameObject.Destroy(meshRenderer);
				}

				GameObject.Destroy(meshFilter);

				var meshObject = meshFilter.gameObject;
				if (meshObject.transform.parent.CompareTag("Collision"))
				{
					GameObject.Destroy(meshObject);
				}
			}

			var newCombinedMesh = new Mesh();
			newCombinedMesh.CombineMeshes(combine, true);
			newCombinedMesh.RecalculateTangents();
			newCombinedMesh.RecalculateBounds();
			newCombinedMesh.RecalculateNormals();
			newCombinedMesh.Optimize();

			return newCombinedMesh;
		}

		private static void KeepUnmergedMeshes(in MeshFilter[] meshFilters)
		{
			foreach (var meshFilter in meshFilters)
			{
				var meshObject = meshFilter.gameObject;
				var meshCollider = meshObject.AddComponent<MeshCollider>();

				meshCollider.sharedMesh = meshFilter.sharedMesh;
				meshCollider.convex = true;
				meshCollider.cookingOptions = cookingOptions;
				meshCollider.hideFlags |= HideFlags.NotEditable;

				if (meshObject.TryGetComponent<MeshRenderer>(out var meshRenderer))
				{
					GameObject.Destroy(meshRenderer);
				}

				GameObject.Destroy(meshFilter);
			}
		}

		public static void Make(GameObject targetObject)
		{
			var meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();

			if (enableMergeCollider)
			{
				var meshCollider = targetObject.AddComponent<MeshCollider>();

				var mergedMesh = MergeMeshes(meshFilters);

				mergedMesh.name = targetObject.name;

				meshCollider.sharedMesh = mergedMesh;
				meshCollider.convex = true;
				meshCollider.cookingOptions = cookingOptions;
				meshCollider.hideFlags |= HideFlags.NotEditable;
			}
			else
			{
				KeepUnmergedMeshes(meshFilters);
			}
		}

		public static void SetPhysicalMaterial(in SDF.Surface surface, in GameObject targetObject)
		{
			var material = new PhysicMaterial();

			if (surface != null)
			{
				material.name = "SDF Surface Friction";
				material.dynamicFriction = (float)surface.friction2;
				material.staticFriction = (float)surface.friction;
				material.frictionCombine = PhysicMaterialCombine.Average;
				material.bounciness = 0.000f;
				material.bounceCombine = PhysicMaterialCombine.Minimum;
			}
			else
			{
				// Rubber Material
				material.name = "Stone";
				material.dynamicFriction = 0.32f;
				material.staticFriction = 0.4f;
				material.frictionCombine = PhysicMaterialCombine.Average;
				material.bounciness = 0.00f;
				material.bounceCombine = PhysicMaterialCombine.Minimum;

				// Rubber Material
				// material.name = "Tire";
				// material.dynamicFriction = 0.7f;
				// material.staticFriction = 0.8f;
				// material.frictionCombine = PhysicMaterialCombine.Average;
				// material.bounciness = 0.01f;
				// material.bounceCombine = PhysicMaterialCombine.Minimum;

				// Metal Material
				// material.dynamicFriction = 1.0f;
				// material.staticFriction = 0.9f;
				// material.frictionCombine = PhysicMaterialCombine.Minimum;
				// material.bounciness = 0.1f;
				// material.bounceCombine = PhysicMaterialCombine.Minimum;
			}

			// Set physics materials
			foreach (var meshCollider in targetObject.GetComponentsInChildren<MeshCollider>())
			{
				meshCollider.material = material;
			}
		}
	}
}
