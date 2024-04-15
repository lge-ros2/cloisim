/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportCollision(in SDF.Collision collision, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newCollisionObject = new UE.GameObject(collision.Name);
				newCollisionObject.tag = "Collision";

				SetParentObject(newCollisionObject, targetObject);

				var localPosition = SDF2Unity.Position(collision.Pose.Pos);
				var localRotation = SDF2Unity.Rotation(collision.Pose.Rot);

				var collisionHelper = newCollisionObject.AddComponent<Helper.Collision>();
				collisionHelper.SetPose(localPosition, localRotation);
				collisionHelper.ResetPose();

				return newCollisionObject as System.Object;
			}

			protected override void AfterImportCollision(in SDF.Collision collision, in System.Object targetObject)
			{
				var collisionObject = (targetObject as UE.GameObject);

				// Make collision region for Collision
				if (collisionObject.CompareTag("Collision"))
				{
					var geometryObject = (collisionObject.transform.childCount == 0) ? collisionObject : collisionObject.transform.GetChild(0).gameObject;
					Implement.Collision.Make(geometryObject);

					if (collision.GetGeometry().GetShapeType().Equals(typeof(Plane)))
					{
						collisionObject.layer = Implement.Collision.PlaneLayerIndex;
						var collider = collisionObject.GetComponentInChildren<UE.MeshCollider>();
						collider.convex = false;
					}

#if UNITY_EDITOR
					SceneVisibilityManager.instance.ToggleVisibility(collisionObject, true);
					SceneVisibilityManager.instance.DisablePicking(collisionObject, true);
#endif
				}

				// Due to making collision function, it should be called after make collision regioin
				collisionObject.transform.localPosition = SDF2Unity.Position(collision.Pose.Pos);
				collisionObject.transform.localRotation = SDF2Unity.Rotation(collision.Pose.Rot);

				Implement.Collision.SetSurfaceFriction(collision.GetSurface(), collisionObject);
			}
		}
	}
}