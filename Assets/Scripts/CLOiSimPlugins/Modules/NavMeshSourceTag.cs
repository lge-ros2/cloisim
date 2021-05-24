/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;

// Tagging component for use with the LocalNavMeshBuilder
// Supports mesh-filter and terrain - can be extended to physics and/or primitives
[DefaultExecutionOrder(-200)]
public class NavMeshSourceTag : MonoBehaviour
{
	// Global containers for all active mesh/terrain tags
	public static List<MeshFilter> m_meshFilters = new List<MeshFilter>();

	void OnEnable()
	{
		var meshFilters = GetComponentsInChildren<MeshFilter>();
		if (meshFilters != null)
		{
			foreach (var meshFilter in meshFilters)
			{
				m_meshFilters.Add(meshFilter);
			}
		}
	}

	void OnDisable()
	{
		var meshFilters = GetComponentsInChildren<MeshFilter>();
		if (meshFilters != null)
		{
			foreach (var meshFilter in meshFilters)
			{
				m_meshFilters.Remove(meshFilter);
			}
		}
	}

	// Collect all the navmesh build sources for enabled objects tagged by this component
	public static void Collect(ref List<NavMeshBuildSource> sources)
	{
		sources.Clear();

		for (var i = 0; i < m_meshFilters.Count; ++i)
		{
			var mf = m_meshFilters[i];
			if (mf == null)
			{
				 continue;
			}

			var mesh = mf.sharedMesh;
			if (mesh != null)
			{
				var navMeshBuildSrc = new NavMeshBuildSource();
				navMeshBuildSrc.shape = NavMeshBuildSourceShape.Mesh;
				navMeshBuildSrc.sourceObject = mesh;
				navMeshBuildSrc.transform = mf.transform.localToWorldMatrix;
				navMeshBuildSrc.area = 0;
				sources.Add(navMeshBuildSrc);
			}
		}
	}
}