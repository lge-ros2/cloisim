/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportActor(in Actor actor)
			{
				if (actor == null)
				{
					return;
				}

				var newActorObject = Implement.Actor.CreateSkin(actor.skin);
				if (newActorObject == null)
				{
					return;
				}

				newActorObject.name = actor.Name;
				newActorObject.tag = "Actor";

				SetParentObject(newActorObject, null);

				// Apply attributes
				var localPosition = SDF2Unity.GetPosition(actor.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(actor.Pose.Rot);
				// Debug.Log(newActorObject.name + "::" + localPosition + ", " + localRotation);

				var actorHelper = newActorObject.AddComponent<Helper.Actor>();
				actorHelper.isStatic = true;
				actorHelper.SetPose(localPosition, localRotation);

				foreach (var animation in actor.animations)
				{
					Implement.Actor.SetAnimation(newActorObject, animation);
				}

				actorHelper.SetScript(actor.script);

				var skinnedMeshRenderer = newActorObject.GetComponentInChildren<SkinnedMeshRenderer>();

				var capsuleCollider = newActorObject.AddComponent<CapsuleCollider>();

				var localBound = skinnedMeshRenderer.localBounds;
				const float sizeRatio = 0.85f;
				capsuleCollider.direction = 1;
				capsuleCollider.center = new Vector3(0, localBound.extents.y, 0);
				capsuleCollider.radius = localBound.center.magnitude * sizeRatio;
				capsuleCollider.height = (localBound.max.y - localBound.min.y) * sizeRatio;
			}
		}
	}
}