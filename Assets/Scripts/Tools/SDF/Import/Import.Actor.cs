/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// using System;
using UnityEngine;
// using UE = UnityEngine;

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
					Implement.Actor.SetAnimation(animation, newActorObject);
				}

				Implement.Actor.SetScript(actor.script, newActorObject);

				return newActorObject as System.Object;
			}
		}
	}
}