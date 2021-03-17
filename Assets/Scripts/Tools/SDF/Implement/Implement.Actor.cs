/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDF
{
	public partial class Implement
	{
		public class Actor
		{
			private static string GetGameObjectPath(UE.Transform transform, UE.Transform rootTransform = null)
			{
				var path = transform.name;
				while (transform != rootTransform)
				{
					transform = transform.parent;
					path = transform.name + "/" + path;
				}
				return path;
			}

			private static Dictionary<string, string> GetBoneHierachy(in UE.Transform rootBone)
			{
				var relativePaths = new Dictionary<string, string>();

				foreach (var transform in rootBone.GetComponentsInChildren<UE.Transform>())
				{
					var relativePath = GetGameObjectPath(transform, rootBone);

					try
					{
						// UE.Debug.Log(transform.name + " :: " + relativePath);
						relativePaths.Add(transform.name, relativePath);
					}
					catch
					{
						UE.Debug.Log("Failed to add " + transform.name);
					}
				}

				return relativePaths;
			}

			public static UE.GameObject CreateSkin(in SDF.Actor.Skin skin, out UE.Quaternion boneRotation)
			{
				return MeshLoader.CreateSkinObject(skin.filename, out boneRotation);
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

				var skinnedMeshRenderer = targetObject.GetComponentInChildren<UE.SkinnedMeshRenderer>();

				var relativePaths = GetBoneHierachy(skinnedMeshRenderer.rootBone);

				var actorHelper = targetObject.GetComponent<SDF.Helper.Actor>();
				var boneRotation = actorHelper.BoneRotation;

 				var animationClips = MeshLoader.LoadAnimations(animation.filename, relativePaths, boneRotation);
				foreach (var animationClip in animationClips)
				{
					UE.Debug.Log("animation clip name: " + animationClip.name);

					// if (animation.interpolate_x)
					{
						// animationClip.EnsureQuaternionContinuity();
					}
					animationComponent.AddClip(animationClip, animationClip.name);//, 1, 100, true);
					animationComponent.clip = animationClip;
				}

				animationComponent.wrapMode = UE.WrapMode.Loop;
				animationComponent.animatePhysics = false;
				animationComponent.playAutomatically = true;
				animationComponent.Play();
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