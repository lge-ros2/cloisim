/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override object ImportActor(in Actor actor)
			{
				if (actor == null)
				{
					return null;
				}

				var newActorObject = new UE.GameObject(actor.Name)
				{
					tag = "Actor"
				};
				Main.WorldRoot.SetChild(newActorObject);

				// Apply attributes
				var (localPosition, localRotation) = actor.RawPose.ToUnity();
				newActorObject.transform.localPosition = localPosition;
				newActorObject.transform.localRotation = localRotation;

				var actorHelper = newActorObject.AddComponent<Helper.Actor>();
				actorHelper.Pose = actor.RawPose;
				actorHelper.PoseRelativeTo = actor.PoseRelativeTo;

				var newSkinObject = Implement.Actor.CreateSkin(actor.SkinFilename);

				if (newSkinObject != null)
				{
					newActorObject.SetChild(newSkinObject);
					newSkinObject.transform.localScale = UE.Vector3.one * (float)actor.SkinScale;

					if (actor.Animations != null)
					{
						foreach (var animation in actor.Animations)
						{
							Implement.Actor.SetAnimation(newSkinObject, animation, actor.ScriptAutoStart, actor.ScriptLoop);
						}
					}

					actorHelper.SetScript(actor);
				}

				var capsuleCollider = newActorObject.AddComponent<UE.CapsuleCollider>();
				capsuleCollider.direction = 1;

				return newActorObject as object;
			}
		}
	}
}