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
					var relativePath = GetGameObjectPath(transform, rootBone.parent);

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

			public static UE.GameObject CreateSkin(in SDF.Actor.Skin skin)
			{
				return MeshLoader.CreateSkinObject(skin.filename);
			}

			public static void SetAnimation(in UE.GameObject targetObject, in SDF.Actor.Animation animation)
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

				var animationClips = MeshLoader.LoadAnimations(animation.filename, relativePaths, (float)animation.scale);
				foreach (var animationClip in animationClips)
				{
					// UE.Debug.Log("animation clip name: " + animationClip.name);
					animationComponent.AddClip(animationClip, animationClip.name);
					animationComponent.clip = animationClip;
				}

				animationComponent.wrapMode = UE.WrapMode.Loop;
				animationComponent.animatePhysics = false;
				animationComponent.playAutomatically = true;
				animationComponent.Play();
			}
		}
	}
}
