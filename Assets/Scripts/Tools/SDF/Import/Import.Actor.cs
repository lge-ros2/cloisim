/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportActor(in Actor actor)
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
				actorHelper.SetPose(localPosition, localRotation);
				actorHelper.ResetPose();

				newActorObject.transform.localScale = UE.Vector3.one * (float)actor.skin.scale;

				var script = actor.script;
				if (actor.animations != null)
				{
					foreach (var animation in actor.animations)
					{
						Implement.Actor.SetAnimation(newActorObject, animation, script.auto_start, script.loop);
					}
				}

				actorHelper.SetScript(script);

				var capsuleCollider = newActorObject.AddComponent<UE.CapsuleCollider>();
				capsuleCollider.direction = 1;

				return newActorObject as System.Object;
			}
		}
	}
}