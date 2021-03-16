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
			var clipIndex = 0;
			foreach (var animation in scene.Animations)
			{
				clipIndex++;
				var clip = new AnimationClip();

				var clipName = animation.Name;
				if (string.IsNullOrEmpty(clipName))
				{
					clipName = "Take " + clipIndex.ToString().PadLeft(3, '0');
				}

				clip.name = clipName;
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
						Debug.Log(node.NodeName + ", " + node.PreState + ", " + node.PostState);

						var keyFramesX = new List<Keyframe>();
						var keyFramesY = new List<Keyframe>();
						var keyFramesZ = new List<Keyframe>();
						Debug.Log("PositionKeyCount=" + node.PositionKeyCount);
						foreach (var positionKey in node.PositionKeys)
						{
							var position = new Vector3(positionKey.Value.X, positionKey.Value.Y, positionKey.Value.Z);
							var keyFrameX = new Keyframe((float)positionKey.Time, position.x);
							var keyFrameY = new Keyframe((float)positionKey.Time, position.y);
							var keyFrameZ = new Keyframe((float)positionKey.Time, position.z);
							keyFramesX.Add(keyFrameX);
							keyFramesY.Add(keyFrameY);
							keyFramesZ.Add(keyFrameZ);
						}

						var curveX = new AnimationCurve(keyFramesX.ToArray());
						var curveY = new AnimationCurve(keyFramesY.ToArray());
						var curveZ = new AnimationCurve(keyFramesZ.ToArray());

						clip.SetCurve(node.NodeName, typeof(Transform), "localPosition.x", curveX);
						clip.SetCurve(node.NodeName, typeof(Transform), "localPosition.y", curveY);
						clip.SetCurve(node.NodeName, typeof(Transform), "localPosition.z", curveZ);


						var keyFramesRotX = new List<Keyframe>();
						var keyFramesRotY = new List<Keyframe>();
						var keyFramesRotZ = new List<Keyframe>();
						var keyFramesRotW = new List<Keyframe>();
						Debug.Log("RotationKeyCount=" + node.RotationKeyCount);
						foreach (var rotationKey in node.RotationKeys)
						{
							var rotation = new Quaternion(rotationKey.Value.X, rotationKey.Value.Y, rotationKey.Value.Z, rotationKey.Value.W);
							var keyFrameX = new Keyframe((float)rotationKey.Time, rotation.x);
							var keyFrameY = new Keyframe((float)rotationKey.Time, rotation.y);
							var keyFrameZ = new Keyframe((float)rotationKey.Time, rotation.z);
							var keyFrameW = new Keyframe((float)rotationKey.Time, rotation.w);
							keyFramesRotX.Add(keyFrameX);
							keyFramesRotY.Add(keyFrameY);
							keyFramesRotZ.Add(keyFrameZ);
							keyFramesRotW.Add(keyFrameW);
						}
						var curveRotX = new AnimationCurve(keyFramesRotX.ToArray());
						var curveRotY = new AnimationCurve(keyFramesRotY.ToArray());
						var curveRotZ = new AnimationCurve(keyFramesRotZ.ToArray());
						var curveRotW = new AnimationCurve(keyFramesRotW.ToArray());

						clip.SetCurve(node.NodeName, typeof(Transform), "localRotation.x", curveRotX);
						clip.SetCurve(node.NodeName, typeof(Transform), "localRotation.y", curveRotY);
						clip.SetCurve(node.NodeName, typeof(Transform), "localRotation.z", curveRotZ);
						clip.SetCurve(node.NodeName, typeof(Transform), "localRotation.w", curveRotZ);

						var keyFramesScaleX = new List<Keyframe>();
						var keyFramesScaleY = new List<Keyframe>();
						var keyFramesScaleZ = new List<Keyframe>();
						Debug.Log("ScalingKeyCount=" + node.ScalingKeyCount);
						foreach (var scalingKey in node.ScalingKeys)
						{
							var position = new Vector3(scalingKey.Value.X, scalingKey.Value.Y, scalingKey.Value.Z);
							var keyFrameScaleX = new Keyframe((float)scalingKey.Time, position.x);
							var keyFrameScaleY = new Keyframe((float)scalingKey.Time, position.y);
							var keyFrameScaleZ = new Keyframe((float)scalingKey.Time, position.z);
							keyFramesX.Add(keyFrameScaleX);
							keyFramesY.Add(keyFrameScaleY);
							keyFramesZ.Add(keyFrameScaleZ);
						}

						var curveScaleX = new AnimationCurve(keyFramesScaleX.ToArray());
						var curveScaleY = new AnimationCurve(keyFramesScaleY.ToArray());
						var curveScaleZ = new AnimationCurve(keyFramesScaleZ.ToArray());

						clip.SetCurve(node.NodeName, typeof(Transform), "localScale.x", curveScaleX);
						clip.SetCurve(node.NodeName, typeof(Transform), "localScale.y", curveScaleY);
						clip.SetCurve(node.NodeName, typeof(Transform), "localScale.z", curveScaleZ);

						// clip.SetCurve(node.NodeName, typeof(Transform), "localPosition.x", curve);
						clip.wrapMode = WrapMode.Loop;
					}
				}

				animationClipList.Add(clip);
			}
		}
		return animationClipList;
	}
}