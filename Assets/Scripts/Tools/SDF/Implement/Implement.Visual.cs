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
		public class Visual
		{
			private static void OptimizeMesh(in UE.Transform target)
			{
				var meshFilters = target.GetComponentsInChildren<UE.MeshFilter>();

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
					if (meshFilterList.Length > 0)
					{
						var targetParent = meshFilterList[0].transform.parent;
						var mergedMesh = SDF2Unity.MergeMeshes(meshFilterList);

						var newName = meshFilterSet.Key.Replace("(Instance)", "Combined").Trim();
						var newVisualGeometryObject = new UE.GameObject(newName);

						var meshFilter = newVisualGeometryObject.AddComponent<UE.MeshFilter>();
						mergedMesh.name = newName;
						meshFilter.sharedMesh = mergedMesh;

						var meshRenderer = newVisualGeometryObject.AddComponent<UE.MeshRenderer>();
						meshRenderer.material = material;

						newVisualGeometryObject.transform.SetParent(targetParent, true);
					}
				}
			}

			public static void OptimizeMeshes(in UE.Transform targetTransform)
			{
				for (var i = 0; i< targetTransform.childCount; i++)
				{
					var child = targetTransform.GetChild(i);
					var optimizationTarget
						= (child.GetComponent<UE.MeshFilter>() == null)
							? child : targetTransform;

					OptimizeMesh(optimizationTarget);
				}
			}
		}
	}
}