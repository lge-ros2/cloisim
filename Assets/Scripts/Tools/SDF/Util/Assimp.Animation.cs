/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UnityEditor;
using UnityEngine;

public partial class MeshLoader
{
	public class KeyFramesPosition
	{
		protected List<Keyframe> X;
		protected List<Keyframe> Y;
		protected List<Keyframe> Z;

		private Quaternion _rotation;

		public KeyFramesPosition()
		: this(Quaternion.identity)
		{
		}

		public KeyFramesPosition(in Quaternion rotation)
		{
			_rotation = rotation;
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z)
		{
			var newPos = SDF2Unity.GetPosition(x, y, z);
			X.Add(new Keyframe(time, x));
			Y.Add(new Keyframe(time, y));
			Z.Add(new Keyframe(time, z));
		}

		public Keyframe[] GetkeyFramesPosX() => X.ToArray();
		public Keyframe[] GetkeyFramesPosY() => Y.ToArray();
		public Keyframe[] GetkeyFramesPosZ() => Z.ToArray();

		public AnimationCurve GetAnimationCurvePosX() => new AnimationCurve(GetkeyFramesPosX());
		public AnimationCurve GetAnimationCurvePosY() => new AnimationCurve(GetkeyFramesPosY());
		public AnimationCurve GetAnimationCurvePosZ() => new AnimationCurve(GetkeyFramesPosZ());
	}

	public class KeyFramesScale : KeyFramesPosition
	{
		public KeyFramesScale()
			: base()
		{
		}

		public Keyframe[] GetkeyFramesScaleX() => GetkeyFramesPosX();
		public Keyframe[] GetkeyFramesScaleY() => GetkeyFramesPosY();
		public Keyframe[] GetkeyFramesScaleZ() => GetkeyFramesPosZ();

		public AnimationCurve GetAnimationCurveScaleX() => new AnimationCurve(GetkeyFramesScaleX());
		public AnimationCurve GetAnimationCurveScaleY() => new AnimationCurve(GetkeyFramesScaleY());
		public AnimationCurve GetAnimationCurveScaleZ() => new AnimationCurve(GetkeyFramesScaleZ());
	}

	public class KeyFramesRotation
	{
		public List<Keyframe> X;
		public List<Keyframe> Y;
		public List<Keyframe> Z;
		public List<Keyframe> W;
		private Quaternion _rotation;


		public KeyFramesRotation(in Quaternion rotation)
		{
			_rotation = rotation;
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
			W = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z, in float w)
		{
			var rotationKey = new Quaternion(x, y, z, w);
			// rotationKey *= _rotation;
			X.Add(new Keyframe(time, x));
			Y.Add(new Keyframe(time, y));
			Z.Add(new Keyframe(time, z));
			Z.Add(new Keyframe(time, w));
		}

		public Keyframe[] GetkeyFramesPosX() => X.ToArray();
		public Keyframe[] GetkeyFramesPosY() => Y.ToArray();
		public Keyframe[] GetkeyFramesPosZ() => Z.ToArray();
		public Keyframe[] GetkeyFramesPosW() => W.ToArray();
	}

	private static string GetAnimationClipName(in int clipIndex, in string name)
	{
		var clipName = name;
		if (string.IsNullOrEmpty(clipName))
		{
			clipName = "Take " + clipIndex.ToString().PadLeft(4, '0');
		}

		return clipName;
	}

	public static List<AnimationClip> LoadAnimations(in string filePath, in Dictionary<string, string> relativePaths, in Quaternion boneRotation)
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
				var clip = new AnimationClip();
				clip.name = GetAnimationClipName(clipIndex++, animation.Name);
				clip.legacy = true;
				clip.wrapMode = WrapMode.Loop;
				Debug.Log("Tick/Sec=" + animation.TicksPerSecond + ", DurationInTicks=" + animation.DurationInTicks);
				clip.frameRate = 30;
				Debug.Log("framrate=" + clip.frameRate + ", islooping:" + clip.isLooping);

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
						var relativeName = relativePaths[node.NodeName];

						Debug.Log(relativeName + ", " + node.PreState + ", " + node.PostState);

						var keyFramesPos = new KeyFramesPosition();
						Debug.Log("PositionKeyCount=" + node.PositionKeyCount);
						foreach (var positionKey in node.PositionKeys)
						{
							var vectorKey = positionKey.Value;
							keyFramesPos.Add((float)positionKey.Time, vectorKey.X, vectorKey.Y, vectorKey.Z);
							Debug.Log("["+node.NodeName+"]PositionKey: " + (float)positionKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z);
						}

						var curveX = keyFramesPos.GetAnimationCurvePosX();
						var curveY = keyFramesPos.GetAnimationCurvePosY();
						var curveZ = keyFramesPos.GetAnimationCurvePosZ();
						// curveX.preWrapMode = WrapMode.Once;
						// curveX.postWrapMode = WrapMode.Once;
						// curveY.preWrapMode = WrapMode.Once;
						// curveY.postWrapMode = WrapMode.Once;
						// curveZ.preWrapMode = WrapMode.Once;
						// curveZ.postWrapMode = WrapMode.Once;
						clip.SetCurve(relativeName, typeof(Transform), "localPosition.x", curveX);
						clip.SetCurve(relativeName, typeof(Transform), "localPosition.y", curveY);
						clip.SetCurve(relativeName, typeof(Transform), "localPosition.z", curveZ);

						var keyFramesRot = new KeyFramesRotation(boneRotation);
						Debug.Log("RotationKeyCount=" + node.RotationKeyCount);
						foreach (var rotationKey in node.RotationKeys)
						{
							var vectorKey = rotationKey.Value;
							keyFramesRot.Add((float)(float)rotationKey.Time, vectorKey.X, vectorKey.Y, vectorKey.Z, vectorKey.W);
							Debug.Log("["+node.NodeName+"]RotationKey: " + (float)rotationKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z + ", " + vectorKey.W);
						}
						var curveRotX = new AnimationCurve(keyFramesRot.GetkeyFramesPosX());
						var curveRotY = new AnimationCurve(keyFramesRot.GetkeyFramesPosY());
						var curveRotZ = new AnimationCurve(keyFramesRot.GetkeyFramesPosZ());
						var curveRotW = new AnimationCurve(keyFramesRot.GetkeyFramesPosW());
						// curveRotX.preWrapMode = WrapMode.Once;
						// curveRotX.postWrapMode = WrapMode.Once;
						// curveRotY.preWrapMode = WrapMode.Once;
						// curveRotY.postWrapMode = WrapMode.Once;
						// curveRotZ.preWrapMode = WrapMode.Once;
						// curveRotZ.postWrapMode = WrapMode.Once;
						// curveRotW.preWrapMode = WrapMode.Once;
						// curveRotW.postWrapMode = WrapMode.Once;
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.x", curveRotX);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.y", curveRotY);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.z", curveRotZ);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.w", curveRotW);

						var keyFramesScale = new KeyFramesScale();
						Debug.Log("ScalingKeyCount=" + node.ScalingKeyCount);
						foreach (var scalingKey in node.ScalingKeys)
						{
							var vectorKey = scalingKey.Value;
							keyFramesScale.Add((float)scalingKey.Time, vectorKey.X, vectorKey.Y, vectorKey.Z);
							Debug.Log("["+node.NodeName+"]ScalingKey: " + (float)scalingKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z);
						}

						var curveScaleX = new AnimationCurve(keyFramesScale.GetkeyFramesScaleX());
						var curveScaleY = new AnimationCurve(keyFramesScale.GetkeyFramesScaleY());
						var curveScaleZ = new AnimationCurve(keyFramesScale.GetkeyFramesScaleZ());
						// curveScaleX.preWrapMode = WrapMode.Once;
						// curveScaleX.postWrapMode = WrapMode.Once;
						// curveScaleY.preWrapMode = WrapMode.Once;
						// curveScaleY.postWrapMode = WrapMode.Once;
						// curveScaleZ.preWrapMode = WrapMode.Once;
						// curveScaleZ.postWrapMode = WrapMode.Once;
						clip.SetCurve(relativeName, typeof(Transform), "localScale.x", curveScaleX);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.y", curveScaleY);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.z", curveScaleZ);

						// clip.wrapMode = WrapMode.Loop;
					}
				}

				Debug.Log("Length : " + clip.legacy);
				animationClipList.Add(clip);
			}
		}
		return animationClipList;
	}
}