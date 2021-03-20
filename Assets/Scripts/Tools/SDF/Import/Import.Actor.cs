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
			protected override System.Object ImportActor(in SDF.Actor actor)
			{
				if (actor == null)
				{
					return null;
				}

				var newActorObject = Implement.Actor.CreateSkin(actor.skin);
				if (newActorObject == null)
				{
					return null;
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
				capsuleCollider.center = localBound.extents;
				capsuleCollider.radius = localBound.extents.magnitude;
				capsuleCollider.height = localBound.max.y - localBound.min.y;
				capsuleCollider.direction = 1;

				return newActorObject as System.Object;
			}
		}
	}
}