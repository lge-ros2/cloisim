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
				actorHelper.SetPose(localPosition, localRotation);

				newActorObject.transform.localScale = Vector3.one * (float)actor.skin.scale;

				if (actor.animations != null)
				{
					var script = actor.script;
					foreach (var animation in actor.animations)
					{
						Implement.Actor.SetAnimation(newActorObject, animation, script.auto_start, script.loop);
					}
				}

				actorHelper.SetScript(actor.script);

				var capsuleCollider = newActorObject.AddComponent<CapsuleCollider>();

				var skinnedMeshRenderer = newActorObject.GetComponentInChildren<SkinnedMeshRenderer>();
				var localBound = skinnedMeshRenderer.localBounds;
				const float sizeRatio = 0.8f;
				capsuleCollider.direction = 1;
				capsuleCollider.radius = Mathf.Min(localBound.extents.x, localBound.extents.y) * sizeRatio;
  				capsuleCollider.center = new Vector3(0, localBound.extents.z + capsuleCollider.radius, 0);
				capsuleCollider.height = localBound.size.z + capsuleCollider.radius * 2;
			}
		}
	}
}