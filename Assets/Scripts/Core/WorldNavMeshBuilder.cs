/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.AI;
using Unity.Jobs.LowLevel.Unsafe;
using System.Collections;
using System.Collections.Generic;

// Build and update a localized navmesh from the sources marked by NavMeshSourceTag
[DefaultExecutionOrder(-102)]
public class WorldNavMeshBuilder : MonoBehaviour
{
	private class NavMeshTrack
	{
		private Vector3 m_BoundSize = Vector3.zero; // The size of the build bounds

		private Vector3 m_BoundCenter; // The center of the build

		private NavMeshSourceTag m_NavMeshSourceTag;

		public string Name => m_NavMeshSourceTag.name;

		public NavMeshTrack(in NavMeshSourceTag navMeshSourceTag, in Vector3 boundCenter, in Vector3 boundSize)
		{
			m_NavMeshSourceTag = navMeshSourceTag;
			m_BoundCenter = boundCenter;
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
			return new Bounds(Quantize(m_BoundCenter, 0.1f * m_BoundSize), m_BoundSize);
		}

		public void Collect(ref List<NavMeshBuildSource> sources)
		{
			m_NavMeshSourceTag.Collect(ref sources);
		}

		public void Draw()
		{
			Gizmos.color = Color.yellow;
			var bounds = QuantizedBounds();
			Gizmos.DrawWireCube(bounds.center, bounds.size);

			Gizmos.color = Color.green;
			Gizmos.DrawWireCube(m_BoundCenter, m_BoundSize);
		}
	}

	public static int AgentTypeId = 0;

	private List<NavMeshTrack> m_NavMeshTracks = new List<NavMeshTrack>();

	private NavMeshData m_NavMesh;
	private AsyncOperation m_Operation;
	private NavMeshDataInstance m_Instance;
	private NavMeshBuildSettings m_defaultBuildSettings;
	private List<NavMeshBuildSource> m_Sources = new List<NavMeshBuildSource>();

	public void AddNavMeshZone(in string zone)
	{
		var alreadyAdded = false;
		for (var i = 0; i < m_NavMeshTracks.Count; i++)
		{
			if (zone.Equals(m_NavMeshTracks[i].Name))
			{
				alreadyAdded = true;
				break;
			}
		}

		if (!alreadyAdded)
		{
			var models = Main.WorldRoot.GetComponentsInChildren<SDF.Helper.Model>();

			foreach (var model in models)
			{
				if (model.name.Equals(zone))
				{
					if (model.isStatic)
					{
						AddNavMeshTracks(model.transform);
					}
					else
					{
						Debug.LogWarning("It is not static model, cannot generate NavMesh for " + model.name);
					}
					break;
				}
			}
		}
	}

	public void AddNavMeshTracks(in Transform transform)
	{
		var navMeshSourceTag = transform.gameObject.AddComponent<NavMeshSourceTag>();

		var bounds = new Bounds(transform.position, Vector3.zero);
		var renderers = transform.GetComponentsInChildren<Renderer>();
		for (var i = 0; i < renderers.Length; i++)
		{
			// Debug.Log(renderer.bounds.center + ", " + renderer.bounds.size);
			bounds.Encapsulate(renderers[i].bounds);
		}
		// Debug.Log("Final: " + bounds.center + ", " + bounds.size);

		var navMeshTrack = new NavMeshTrack(navMeshSourceTag, bounds.center, bounds.size);
		m_NavMeshTracks.Add(navMeshTrack);
	}

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
		// Construct and add navmesh
		m_NavMesh = new NavMeshData();
		m_Instance = NavMesh.AddNavMeshData(m_NavMesh);

		m_defaultBuildSettings = NavMesh.GetSettingsByID(AgentTypeId);
		// Debug.Log(m_defaultBuildSettings.tileSize + " | " + JobsUtility.JobWorkerCount + "| "+ m_defaultBuildSettings.maxJobWorkers + " | " + NavMesh.pathfindingIterationsPerFrame);
		// Debug.Log(m_defaultBuildSettings.voxelSize + " | " + m_defaultBuildSettings.minRegionArea);
		// Debug.Log(m_defaultBuildSettings.agentClimb + " | " + m_defaultBuildSettings.agentHeight);
		// Debug.Log(m_defaultBuildSettings.agentSlope + " | " + m_defaultBuildSettings.agentRadius);
		m_defaultBuildSettings.overrideTileSize = true;
		m_defaultBuildSettings.tileSize = 512;
		m_defaultBuildSettings.preserveTilesOutsideBounds = false;
		m_defaultBuildSettings.overrideVoxelSize = true;
		m_defaultBuildSettings.voxelSize /= 2f;
		m_defaultBuildSettings.minRegionArea = 1f;
		m_defaultBuildSettings.maxJobWorkers = (uint)JobsUtility.JobWorkerCount;
		NavMesh.pathfindingIterationsPerFrame = 5;

		UpdateNavMesh(false);
	}

	void OnDisable()
	{
		// Unload navmesh and clear handle
		m_Instance.Remove();
	}

	public void UpdateNavMesh(in bool asyncUpdate = false)
	{
		for (var i = 0; i < m_NavMeshTracks.Count; i++)
		{
			m_NavMeshTracks[i].Collect(ref m_Sources);
			var bounds = m_NavMeshTracks[i].QuantizedBounds();

			if (asyncUpdate)
			{
				m_Operation = NavMeshBuilder.UpdateNavMeshDataAsync(m_NavMesh, m_defaultBuildSettings, m_Sources, bounds);
			}
			else
			{
				NavMeshBuilder.UpdateNavMeshData(m_NavMesh, m_defaultBuildSettings, m_Sources, bounds);
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
