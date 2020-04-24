/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

public partial class SDFImporter : SDF.Importer
{
	protected override void ImportGeometry(in SDF.Geometry geometry, in System.Object parentObject)
	{
		if (geometry == null || geometry.IsEmpty)
			return;

		var targetObject = (parentObject as GameObject);

		Type t = geometry.GetShapeType();
		SDF.ShapeType shape = geometry.GetShape();

		if (t == typeof(SDF.Mesh))
		{
			var mesh = shape as SDF.Mesh;
			SDFImplement.Geometry.SetMesh(mesh, targetObject);
		}
		else if (t.IsSubclassOf(typeof(SDF.ShapeType)))
		{
			SDFImplement.Geometry.SetMesh(shape, targetObject);
		}
		else
		{
			Debug.LogErrorFormat("[{0}] Not support type({1}) for geometry", geometry.Name, geometry.Type);
			return;
		}

		targetObject.SetActive(true);

#if UNITY_EDITOR
		SceneVisibilityManager.instance.DisablePicking(targetObject, true);
#endif
	}
}