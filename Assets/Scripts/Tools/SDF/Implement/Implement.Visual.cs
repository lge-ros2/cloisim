/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Implement
	{
		public static class Visual
		{
			private static void OptimizeMesh(this UE.Transform target)
			{
				var meshFilters = target.GetComponentsInChildren<UE.MeshFilter>();
				if (meshFilters.Length <= 1)
				{
					Debug.LogWarning("No need to optimize -> " + target.name);
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
						var meshFilterSet = new HashSet<UE.MeshFilter>(){
							meshFilter
						};
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
							Debug.LogError("Error on meshFilterTable.TryGetValue()!!");
							Debug.Break();
						}
					}
				}

				foreach (var meshFilterSet in meshFilterTable)
				{
					if (!materialTable.TryGetValue(meshFilterSet.Key, out UE.Material material))
					{
						material = null;
					}

					var meshFilterList = meshFilterSet.Value.ToArray();
					if (meshFilterList.Length > 0)
					{
						var targetParent = meshFilterList[0].transform.parent;
						var mergedMesh = SDF2Unity.MergeMeshes(meshFilterList);

						var newName = meshFilterSet.Key.Replace("(Instance)", "(Combined Mesh)").Trim();
						var newVisualGeometryObject = new UE.GameObject(newName);

						var newMeshFilter = newVisualGeometryObject.AddComponent<UE.MeshFilter>();
						mergedMesh.name = newName;
						newMeshFilter.sharedMesh = mergedMesh;

						var meshRenderer = newVisualGeometryObject.AddComponent<UE.MeshRenderer>();
						meshRenderer.material = material;

						newVisualGeometryObject.transform.SetParent(targetParent, true);

						foreach (var meshFilter in meshFilters)
						{
							if (!meshFilter.gameObject.CompareTag("Visual"))
							{
								UE.GameObject.Destroy(meshFilter.gameObject);
							}
						}
					}
				}
			}

			public static void OptimizeMeshes(this UE.Transform targetTransform)
			{
				for (var i = 0; i < targetTransform.childCount; i++)
				{
					var child = targetTransform.GetChild(i);
					child.OptimizeMesh();
				}
			}

			public static void RemoveColliders(this UE.GameObject targetObject)
			{
				var colliders = targetObject.GetComponentsInChildren<UE.Collider>();
				foreach (var collider in colliders)
				{
					UE.Debug.LogWarning($"{collider.name} Collider should not exit. There was collider");
					UE.GameObject.Destroy(collider);
				}
			}

			public static void AddRenderes(this UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();
				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.gameObject.GetComponent<UE.MeshRenderer>();
					if (meshRenderer == null)
					{
						meshRenderer = meshFilter.gameObject.AddComponent<UE.MeshRenderer>();
						meshRenderer.materials = new UE.Material[] { SDF2Unity.Material.Create(meshFilter.name + "_material") };
						meshRenderer.allowOcclusionWhenDynamic = true;
						meshRenderer.receiveShadows = true;
					}
				}
			}
		}
	}
}