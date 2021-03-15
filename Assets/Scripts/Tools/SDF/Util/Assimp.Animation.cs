/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEngine;

public partial class MeshLoader
{
	public static List<AnimationClip> LoadAnimations(in string name, in string filePath)
	{
		var animationClipList = new List<AnimationClip>();
		var scene = GetScene(filePath, out var meshRotation);
		if (scene == null)
		{
			return null;
		}

		if (scene.HasAnimations)
		{
			// Debug.Log("Total Animations = " + scene.AnimationCount);

			foreach (var animation in scene.Animations)
			{
				var clip = new AnimationClip();
				clip.name = animation.Name;
				clip.legacy = true;
				Debug.Log("Tick/Sec=" + animation.TicksPerSecond + ", DurationInTicks=" + animation.DurationInTicks);

				if (animation.HasMeshAnimations)
				{
					Debug.LogWarningFormat("Mesh Animation({0}) is not support yet!");
					Debug.LogWarning("MeshAnimationChannelCount=" + animation.MeshAnimationChannelCount);
					Debug.LogWarning("MeshMorphAnimationChannelCount=" + animation.MeshMorphAnimationChannelCount);
				}
				else if (animation.HasNodeAnimations)
				{
					foreach (var node in animation.NodeAnimationChannels)
					{
						Debug.Log(node.NodeName + ", " + node.PositionKeys);
					}
				}
				// clip.isLooping
				// clip.SetCurve()
				// clip.AddEvent()

				animationClipList.Add(clip);
			}
		}
		return animationClipList;
	}
}