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
			protected override System.Object ImportCollision(in SDFormat.Collision collision, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newCollisionObject = new UE.GameObject(collision.Name);
				newCollisionObject.tag = "Collision";

				targetObject.SetChild(newCollisionObject);

				var localPosition = collision.RawPose.ToUnityPosition();
				var localRotation = collision.RawPose.ToUnityRotation();

				var collisionHelper = newCollisionObject.AddComponent<Helper.Collision>();
				collisionHelper.Pose = collision.RawPose;
				collisionHelper.PoseRelativeTo = collision.PoseRelativeTo;

				return newCollisionObject as System.Object;
			}

			protected override void AfterImportCollision(in SDFormat.Collision collision, in System.Object targetObject)
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
					if (geom != null && geom.Type != SDFormat.GeometryType.Plane)
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

						if (geom != null && geom.Type == SDFormat.GeometryType.Plane)
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
				collisionObject.transform.localPosition = collision.RawPose.ToUnityPosition();
				collisionObject.transform.localRotation = collision.RawPose.ToUnityRotation();

				collisionObject.SetSurfaceFriction(collision.SurfaceInfo);
			}

			private bool EnhanceCollisionPerformance(
				in SDFormat.Geometry geom,
				UE.GameObject targetObject)
			{
				switch (geom.Type)
				{
					case SDFormat.GeometryType.Box:
					{
						var scale = SDF2Unity.Scale(geom.BoxShape.Size);
						var boxCollider = targetObject.AddComponent<UE.BoxCollider>();
						boxCollider.size = scale;
						return true;
					}
					case SDFormat.GeometryType.Sphere:
					{
						var sphereCollider = targetObject.AddComponent<UE.SphereCollider>();
						sphereCollider.radius = (float)geom.SphereShape.Radius;
						return true;
					}
					case SDFormat.GeometryType.Capsule:
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