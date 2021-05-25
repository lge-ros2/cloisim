/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using NavMeshBuilder = UnityEngine.AI.NavMeshBuilder;

// Build and update a localized navmesh from the sources marked by NavMeshSourceTag
[DefaultExecutionOrder(-102)]
public class WorldNavMeshBuilder : MonoBehaviour
{
	public class NavMeshTrack
	{
		// The center of the build
		private Transform m_Tracked;

		// The size of the build bounds
		public Vector3 m_BoundSize = Vector3.zero;

		private Vector3 Center => m_Tracked.position;

		public NavMeshTrack(in Transform transform, in Vector3 boundSize)
		{
			m_Tracked = transform;
			m_BoundSize = boundSize;
		}

		private Vector3 Quantize(in Vector3 v, in Vector3 quant)
		{
			var x = quant.x * Mathf.Floor(v.x / quant.x);
			var y = quant.y * Mathf.Floor(v.y / quant.y);
			var z = quant.z * Mathf.Floor(v.z / quant.z);
			return new Vector3(x, y, z);
		}

		public Bounds QuantizedBounds()
		{
			// Quantize the bounds to update only when theres a 10% change in size
			return new Bounds(Quantize(Center, 0.1f * m_BoundSize), m_BoundSize);
		}

		public void Draw()
		{
			Gizmos.color = Color.yellow;
			var bounds = QuantizedBounds();
			Gizmos.DrawWireCube(bounds.center, bounds.size);

			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(Center, m_BoundSize);
		}
	}

	private List<NavMeshTrack> m_NavMeshTracks = new List<NavMeshTrack>();

	private NavMeshData m_NavMesh;
	private AsyncOperation m_Operation;
	private NavMeshDataInstance m_Instance;
	private List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();

	IEnumerator Start()
	{
		while (true)
		{
			UpdateNavMesh(true);
			yield return m_Operation;
		}
	}

	void OnEnable()
	{
		Debug.Log("enable " + typeof(WorldNavMeshBuilder));
		// Construct and add navmesh
		m_NavMesh = new NavMeshData();
		m_Instance = NavMesh.AddNavMeshData(m_NavMesh);

		UpdateNavMesh(false);
	}

	void OnDisable()
	{
		// Unload navmesh and clear handle
		m_Instance.Remove();
	}

	void UpdateNavMesh(bool asyncUpdate = false)
	{
		NavMeshSourceTag.Collect(ref m_Sources);
		var defaultBuildSettings = NavMesh.GetSettingsByID(0);

		foreach (var navMeshTrack in m_NavMeshTracks)
		{
			var bounds = navMeshTrack.QuantizedBounds();

			if (asyncUpdate)
			{
				m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, defaultBuildSettings, m_Sources, bounds);
			}
			else
			{
				NavMeshBuilder.UpdateNavMeshData(m_NavMesh, defaultBuildSettings, m_Sources, bounds);
			}
		}
	}


	void OnDrawGizmosSelected()
	{
		if (m_NavMesh)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(m_NavMesh.sourceBounds.center, m_NavMesh.sourceBounds.size);
		}

		foreach (var navMeshTrack in m_NavMeshTracks)
		{
			navMeshTrack.Draw();
		}
	}
}
