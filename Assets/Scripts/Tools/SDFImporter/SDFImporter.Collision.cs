/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

public partial class SDFImporter : SDF.Importer
{
	protected override System.Object ImportCollision(in SDF.Collision collision, in System.Object parentObject)
	{
		var targetObject = (parentObject as GameObject);
		var newCollisionObject = new GameObject(collision.Name);
		SetParentObject(newCollisionObject, targetObject);

		newCollisionObject.tag = "Collision";

		return newCollisionObject as System.Object;
	}

	protected override void PostImportCollision(in SDF.Collision collision, in System.Object targetObject)
	{
		var collisionObject = (targetObject as GameObject);

		// Make collision region for Collision
		if (collisionObject.CompareTag("Collision"))
		{
			SDFImplement.Collision.Make(collisionObject);

#if UNITY_EDITOR
			SceneVisibilityManager.instance.ToggleVisibility(collisionObject, true);
			SceneVisibilityManager.instance.DisablePicking(collisionObject, true);
#endif
		}

		// Due to making collision function, it should be called after make collision regioin
		collisionObject.transform.localPosition = SDF2Unity.GetPosition(collision.Pose.Pos);
		collisionObject.transform.localRotation = SDF2Unity.GetRotation(collision.Pose.Rot);

		SDFImplement.Collision.SetPhysicalMaterial(collision.GetSurface(), collisionObject);
	}
}
