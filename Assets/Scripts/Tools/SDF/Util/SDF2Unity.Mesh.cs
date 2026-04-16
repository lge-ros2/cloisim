/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static partial class SDF2Unity
{
	public static Mesh MergeMeshes(this MeshFilter[] meshFilters)
	{
		if (meshFilters == null || meshFilters.Length == 0)
			return null;

		var combine = new List<CombineInstance>(meshFilters.Length);
		var totalVertexCount = 0;
		for (var combineIndex = 0; combineIndex < meshFilters.Length; combineIndex++)
		{
			var meshFilter = meshFilters[combineIndex];
			if (meshFilter == null || meshFilter.sharedMesh == null || meshFilter.sharedMesh.vertexCount == 0)
				continue;

			var mesh = meshFilter.sharedMesh;
			combine.Add(new CombineInstance
			{
				mesh = mesh,
				transform = meshFilter.transform.localToWorldMatrix
			});
			totalVertexCount += mesh.vertexCount;
		}

		if (combine.Count == 0)
			return null;

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine.ToArray(), true, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}

	public static Mesh MergeMeshes(this MeshCollider[] meshColliders, in Matrix4x4 geometryWorldToLocalMatrix)
	{
		if (meshColliders == null || meshColliders.Length == 0)
			return null;

		var combine = new List<CombineInstance>(meshColliders.Length);
		var totalVertexCount = 0;
		for (var index = 0; index < meshColliders.Length; index++)
		{
			var meshCollider = meshColliders[index];
			if (meshCollider == null || meshCollider.sharedMesh == null || meshCollider.sharedMesh.vertexCount == 0)
				continue;

			var mesh = meshCollider.sharedMesh;
			combine.Add(new CombineInstance
			{
				mesh = mesh,
				transform = geometryWorldToLocalMatrix * meshCollider.transform.localToWorldMatrix
			});
			totalVertexCount += mesh.vertexCount;
		}

		if (combine.Count == 0)
			return null;

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine.ToArray(), false, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}
}