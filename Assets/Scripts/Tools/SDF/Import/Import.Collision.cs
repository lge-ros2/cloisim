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

				var localPosition = SDF2Unity.Position(collision.Pose?.Pos);
				var localRotation = SDF2Unity.Rotation(collision.Pose?.Rot);

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

					var shape = collision.GetGeometry().GetShape();
					var shapeType = shape.GetType();

					var existingMeshCollider = geometryObject.GetComponent<UE.MeshCollider>();

					if (shapeType.Equals(typeof(Plane)))
					{
						collisionObject.layer = Implement.Collision.PlaneLayerIndex;
						existingMeshCollider.convex = false;
					}
					else
					{
						if (EnhanceCollisionPerformance(shapeType, shape, existingMeshCollider))
						{
							var meshColliders = geometryObject.GetComponentsInChildren<UE.MeshCollider>();
							for (var index = 0; index < meshColliders.Length; index++)
							{
								UE.GameObject.Destroy(meshColliders[index]);
							}
						}
					}

#if UNITY_EDITOR
					SceneVisibilityManager.instance.ToggleVisibility(collisionObject, true);
					SceneVisibilityManager.instance.DisablePicking(collisionObject, true);
#endif
				}

				// Due to making collision function, it should be called after make collision regioin
				collisionObject.transform.localPosition = SDF2Unity.Position(collision.Pose?.Pos);
				collisionObject.transform.localRotation = SDF2Unity.Rotation(collision.Pose?.Rot);

				Implement.Collision.SetSurfaceFriction(collision.GetSurface(), collisionObject);
			}

			private bool EnhanceCollisionPerformance(
				in System.Type shapeType,
				in ShapeType shape,
				UE.MeshCollider meshCollider)
			{
				if (shapeType.Equals(typeof(Box)))
				{
					var box = shape as SDF.Box;
					var scale = SDF2Unity.Scale(box.size);

					var boxCollider = meshCollider.gameObject.AddComponent<UE.BoxCollider>();
					boxCollider.size = scale;
					return true;
				}
				else if (shapeType.Equals(typeof(Sphere)))
				{
					var sphere = shape as SDF.Sphere;

					var sphereCollider = meshCollider.gameObject.AddComponent<UE.SphereCollider>();
					sphereCollider.radius = (float)sphere.radius;
					return true;
				}
				else if (shapeType.Equals(typeof(Capsule)))
				{
					var capsule = shape as SDF.Capsule;

					var capsuleCollider = meshCollider.gameObject.AddComponent<UE.CapsuleCollider>();
					capsuleCollider.radius = (float)capsule.radius;
					capsuleCollider.height = (float)capsule.length;
					return true;
				}

				return false;
			}
		}
	}
}