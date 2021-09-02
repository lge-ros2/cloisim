/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	public partial class Implement
	{
		public class Visual
		{
			public static void OptimizeMeshes(in UE.Transform targetTransform)
			{
				var meshFilters = targetTransform.GetComponentsInChildren<UE.MeshFilter>();

				if (meshFilters.Length <= 1)
				{
					return;
				}

				var materialTable = new Dictionary<string, UE.Material>();
				var meshFilterTable = new Dictionary<string, HashSet<UE.MeshFilter>>();

				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.gameObject.GetComponent<UE.MeshRenderer>();
					var material = meshRenderer.material;
					var materialName = material.name;

					if (!materialTable.ContainsKey(materialName))
					{
						materialTable.Add(materialName, material);
					}

					if (!meshFilterTable.ContainsKey(materialName))
					{
						var meshFilterSet = new HashSet<UE.MeshFilter>();
						meshFilterSet.Add(meshFilter);
						meshFilterTable.Add(materialName, meshFilterSet);
					}
					else
					{
						if (meshFilterTable.TryGetValue(materialName, out var value))
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
						UE.GameObject.Destroy(meshFilter.gameObject);
					}
				}

				foreach (var meshFilterSet in meshFilterTable)
				{
					if (!materialTable.TryGetValue(meshFilterSet.Key, out UE.Material material))
					{
						material = null;
					}

					var meshFilterList = meshFilterSet.Value.ToArray();
					var mergedMesh = SDF2Unity.MergeMeshes(meshFilterList);

					var newName = meshFilterSet.Key.Replace("(Instance)", "Combined").Trim();
					var newVisualGeometryObject = new UE.GameObject(newName);

					var meshFilter = newVisualGeometryObject.AddComponent<UE.MeshFilter>();
					mergedMesh.name = newName;
					meshFilter.sharedMesh = mergedMesh;

					var meshRenderer = newVisualGeometryObject.AddComponent<UE.MeshRenderer>();
					meshRenderer.material = material;

					newVisualGeometryObject.transform.SetParent(targetTransform, true);
				}
			}
		}
	}
}