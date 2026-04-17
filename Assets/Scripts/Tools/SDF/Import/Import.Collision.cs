/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportCollision(in Collision collision, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newCollisionObject = new UE.GameObject(collision.Name)
				{
					tag = "Collision"
				};

				targetObject.SetChild(newCollisionObject);

				var collisionHelper = newCollisionObject.AddComponent<Helper.Collision>();
				collisionHelper.Pose = collision.RawPose;
				collisionHelper.PoseRelativeTo = collision.PoseRelativeTo;

				return newCollisionObject as System.Object;
			}

			protected override void AfterImportCollision(in Collision collision, in System.Object targetObject)
			{
				var collisionObject = (targetObject as UE.GameObject);

				// Make collision region for Collision
				if (collisionObject.CompareTag("Collision"))
				{
					var geometryObject = (collisionObject.transform.childCount == 0) ? collisionObject : collisionObject.transform.GetChild(0).gameObject;

					var geom = collision.Geom;
					var enhanced = false;

					// Try native colliders first (Box, Sphere, Capsule) to avoid
					// creating MeshColliders that would be immediately discarded.
					if (geom != null && geom.Type != GeometryType.Plane)
					{
						enhanced = EnhanceCollisionPerformance(geom, geometryObject);
					}

					if (enhanced)
					{
						Implement.Collision.RemoveRenderers(geometryObject);
					}
					else
					{
						geometryObject.MakeCollision();

						if (geom != null && geom.Type == GeometryType.Plane)
						{
							collisionObject.layer = Implement.Collision.PlaneLayerIndex;
							var existingMeshCollider = geometryObject.GetComponent<UE.MeshCollider>();
							if (existingMeshCollider != null)
							{
								existingMeshCollider.convex = false;
							}
						}
					}

#if UNITY_EDITOR
					SceneVisibilityManager.instance.ToggleVisibility(collisionObject, true);
					SceneVisibilityManager.instance.DisablePicking(collisionObject, true);
#endif
				}

				// Due to making collision function, it should be called after make collision regioin
				var (position, rotation) = collision.RawPose.ToUnity();
				collisionObject.transform.localPosition = position;
				collisionObject.transform.localRotation = rotation;

				collisionObject.SetSurfaceFriction(collision.SurfaceInfo);
			}

			private bool EnhanceCollisionPerformance(
				in Geometry geom,
				UE.GameObject targetObject)
			{
				switch (geom.Type)
				{
					case GeometryType.Box:
					{
						var scale = SDF2Unity.Scale(geom.BoxShape.Size);
						var boxCollider = targetObject.AddComponent<UE.BoxCollider>();
						boxCollider.size = scale;
						return true;
					}
					case GeometryType.Sphere:
					{
						var sphereCollider = targetObject.AddComponent<UE.SphereCollider>();
						sphereCollider.radius = (float)geom.SphereShape.Radius;
						return true;
					}
					case GeometryType.Capsule:
					{
						var capsuleCollider = targetObject.AddComponent<UE.CapsuleCollider>();
						capsuleCollider.radius = (float)geom.CapsuleShape.Radius;
						capsuleCollider.height = (float)geom.CapsuleShape.Length;
						return true;
					}
				}

				return false;
			}
		}
	}
}