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
			public static void OptimizeMeshes(in UE.GameObject targetObject)
			{
				var meshFilters = targetObject.GetComponentsInChildren<UE.MeshFilter>();

				if (meshFilters.Length <= 1)
				{
					return;
				}

				var materialTable = new Dictionary<string, UE.Material>();
				var meshfilterTable = new Dictionary<string, HashSet<UE.MeshFilter>>();

				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.gameObject.GetComponent<UE.MeshRenderer>();
					var material = meshRenderer.material;
					var materialName = material.name;

					if (!materialTable.ContainsKey(materialName))
					{
						materialTable.Add(materialName, material);
					}

					if (!meshfilterTable.ContainsKey(materialName))
					{
						var meshfilterSet = new HashSet<UE.MeshFilter>();
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
						UE.GameObject.Destroy(meshFilter.gameObject);
					}
				}

				foreach (var meshfilterSet in meshfilterTable)
				{
					UE.Material material = null;
					if (materialTable.TryGetValue(meshfilterSet.Key, out var value))
					{
						material = value;
					}

					var mehsFilters = meshfilterSet.Value.ToArray();
					var mergedMesh = SDF2Unity.MergeMeshes(mehsFilters);

					var newName = meshfilterSet.Key.Replace("(Instance)", "Combined").Trim();
					var newVisualObject = new UE.GameObject(newName);

					var meshFilter = newVisualObject.AddComponent<UE.MeshFilter>();
					mergedMesh.name = newName;
					meshFilter.sharedMesh = mergedMesh;

					var meshRenderer = newVisualObject.AddComponent<UE.MeshRenderer>();
					meshRenderer.material = material;

					newVisualObject.transform.SetParent(targetObject.transform, false);
				}
			}
		}
	}
}