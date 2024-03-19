/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.Rendering;
using UE = UnityEngine;

public partial class SDF2Unity
{
	public static Mesh MergeMeshes(in MeshFilter[] meshFilters)
	{
		var combine = new CombineInstance[meshFilters.Length];
		var totalVertexCount = 0;
		for (var combineIndex = 0; combineIndex < meshFilters.Length; combineIndex++)
		{
			var meshFilter = meshFilters[combineIndex];
			combine[combineIndex].mesh = meshFilter.sharedMesh;
			totalVertexCount += meshFilter.sharedMesh.vertexCount;
			combine[combineIndex].transform = meshFilter.transform.localToWorldMatrix;
			// Debug.LogFormat("{0}, {1}: {2}", meshFilter.name, meshFilter.transform.name, combine[combineIndex].transform);
		}

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, true, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}

	public static Mesh MergeMeshes(in MeshCollider[] meshColliders, in Matrix4x4 geometryWorldToLocalMatrix)
	{
		var combine = new CombineInstance[meshColliders.Length];
		var totalVertexCount = 0;
		for (var index = 0; index < meshColliders.Length; index++)
		{
			var meshCollider = meshColliders[index];
			combine[index].mesh = meshCollider.sharedMesh;
			totalVertexCount += combine[index].mesh.vertexCount;
			var meshColliderTransform = meshCollider.transform;
			combine[index].transform = geometryWorldToLocalMatrix * meshColliderTransform.localToWorldMatrix;
		}

		var newCombinedMesh = new Mesh();
		newCombinedMesh.name = "Merged";
		newCombinedMesh.indexFormat = (totalVertexCount >= UInt16.MaxValue) ? IndexFormat.UInt32 : IndexFormat.UInt16;
		newCombinedMesh.CombineMeshes(combine, false, true);
		newCombinedMesh.RecalculateNormals();
		newCombinedMesh.RecalculateTangents();
		newCombinedMesh.RecalculateBounds();
		newCombinedMesh.RecalculateUVDistributionMetrics();
		newCombinedMesh.Optimize();

		return newCombinedMesh;
	}
}