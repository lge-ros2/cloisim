/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent (typeof(MeshFilter))]
[RequireComponent (typeof(MeshRenderer))]
[RequireComponent (typeof(MeshCollider))]

public partial class SDFImplement
{
	public class Visual
	{
		public static void OptimizeMeshes(in GameObject targetObject)
		{
			var meshFilters = targetObject.GetComponentsInChildren<MeshFilter>();

			if (meshFilters.Length <= 1)
			{
				return;
			}

			var materialTable = new Dictionary<string, Material>();
			var meshfilterTable = new Dictionary<string, HashSet<MeshFilter>>();

			foreach (var meshFilter in meshFilters)
			{
				var meshRenderer = meshFilter.gameObject.GetComponent<MeshRenderer>();
				var material = meshRenderer.material;
				var materialName = material.name;

				if (!materialTable.ContainsKey(materialName))
				{
					materialTable.Add(materialName, material);
				}

				if (!meshfilterTable.ContainsKey(materialName))
				{
					var meshfilterSet = new HashSet<MeshFilter>();
					meshfilterSet.Add(meshFilter);
					meshfilterTable.Add(materialName, meshfilterSet);
				}
				else
				{
					if (meshfilterTable.TryGetValue(materialName, out var value))
					{
						value.Add(meshFilter);
					}
					else
					{
						Debug.Log("Error!!");
						Debug.Break();
					}
				}

				if (!meshFilter.gameObject.CompareTag("Visual"))
				{
					GameObject.Destroy(meshFilter.gameObject);
				}
			}

			foreach (var meshfilterSet in meshfilterTable)
			{
				Material material = null;
				if (materialTable.TryGetValue(meshfilterSet.Key, out var value))
				{
					material = value;
				}

				var mehsFilters = meshfilterSet.Value.ToArray();
				var mergedMesh = MergeMeshes(mehsFilters);

				var newName = meshfilterSet.Key.Replace("(Instance)", "Combined").Trim();
				var newVisualObject = new GameObject(newName);

				var meshFilter = newVisualObject.AddComponent<MeshFilter>();
				mergedMesh.name = newName;
				meshFilter.mesh = mergedMesh;

				var meshRenderer = newVisualObject.AddComponent<MeshRenderer>();
				meshRenderer.material = material;

				newVisualObject.transform.SetParent(targetObject.transform, false);
			}
		}

		private static Mesh MergeMeshes(in MeshFilter[] mehsFilters)
		{
			var combine = new CombineInstance[mehsFilters.Length];

			int combineIndex = 0;
			foreach (var meshFilter in mehsFilters)
			{
				var meshTransform = meshFilter.transform;
				var matrix = Matrix4x4.identity;
				matrix.SetTRS(meshTransform.localPosition, meshTransform.localRotation, meshTransform.localScale);
				combine[combineIndex].mesh = meshFilter.sharedMesh;
				combine[combineIndex].transform = matrix;
				combineIndex++;
			}

			var newCombinedMesh = new Mesh();
			newCombinedMesh.CombineMeshes(combine, true);
			newCombinedMesh.RecalculateBounds();
			newCombinedMesh.RecalculateNormals();
			newCombinedMesh.RecalculateTangents();
			newCombinedMesh.Optimize();

			return newCombinedMesh;
		}
	}
}
