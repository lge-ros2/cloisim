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
					animationComponent.clip = animationClip;
				}
			}

			public static void SetScript(in SDF.Actor.Script script, in UE.GameObject targetObject)
			{
				//
				// script
				//
				//  <script>
				//   <loop>true</loop>
				//   <delay_start>0.000000</delay_start>
				//   <auto_start>true</auto_start>
				//   <trajectory id="0" type="walking">
				//     <waypoint>
				//       <time>0.000000</time>
				//       <pose>0.000000 1.000000 0.000000 0.000000 0.000000 0.000000</pose>
				//     </waypoint>
			}
		}
	}
}