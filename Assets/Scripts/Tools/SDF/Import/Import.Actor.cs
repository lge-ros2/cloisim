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

				var newActorObject = new UE.GameObject(actor.Name);
				newActorObject.tag = "Actor";
				Util.RootModels.SetChild(newActorObject);

				// Apply attributes
				var localPosition = actor.Pose?.Pos.ToUnity() ?? UE.Vector3.zero;
				var localRotation = actor.Pose?.Rot.ToUnity() ?? UE.Quaternion.identity;
				// Debug.Log(newActorObject.name + "::" + localPosition + ", " + localRotation);

				var actorHelper = newActorObject.AddComponent<Helper.Actor>();
				actorHelper.Pose = actor?.Pose;

				var newSkinObject = Implement.Actor.CreateSkin(actor.skin);

				if (newSkinObject != null)
				{
					newActorObject.SetChild(newSkinObject);
					newSkinObject.transform.localScale = UE.Vector3.one * (float)actor.skin.scale;

					var script = actor.script;
					if (actor.animations != null)
					{
						foreach (var animation in actor.animations)
						{
							Implement.Actor.SetAnimation(newSkinObject, animation, script.auto_start, script.loop);
						}
					}

					actorHelper.SetScript(script);
				}

				var capsuleCollider = newActorObject.AddComponent<UE.CapsuleCollider>();
				capsuleCollider.direction = 1;

				return newActorObject as System.Object;
			}
		}
	}
}