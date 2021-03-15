/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	public partial class Implement
	{
		public class Actor
		{
			public static UE.GameObject CreateSkin(in SDF.Actor.Skin skin)
			{
				return MeshLoader.CreateSkinObject(skin.filename);
			}

			public static void SetAnimation(in SDF.Actor.Animation animation, in UE.GameObject targetObject)
			{
				if (targetObject == null)
				{
					return;
				}

				var animationComponent = targetObject.GetComponent<UE.Animation>();
				if (animationComponent == null)
				{
					animationComponent = targetObject.AddComponent<UE.Animation>();
				}

				var animationClips = MeshLoader.LoadAnimations(animation.Name, animation.filename);
				foreach (var animationClip in animationClips)
				{
					animationComponent.AddClip(animationClip, animationClip.name);
				}
				// var animationObject = MeshLoader.LoadMeshObject(animation.filename);
				// animationObject.transform.SetParent(targetObject.transform);
				// animationComponent.AddClip()
			}

			public static void SetScript(in SDF.Actor.Script script, in UE.GameObject targetObject)
			{
			}
		}
	}
}