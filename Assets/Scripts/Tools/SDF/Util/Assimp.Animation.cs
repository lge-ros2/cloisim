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
	private static Quaternion keyframeRotation = Quaternion.Euler(90, 0, 0);

	public class KeyFramesPosition
	{
		private List<Keyframe> X;
		private List<Keyframe> Y;
		private List<Keyframe> Z;

		private Vector3 _basePosition;
		private Quaternion _rotation;

		public KeyFramesPosition(in Vector3 basePosition)
		: this(basePosition, Quaternion.identity)
		{
		}

		public KeyFramesPosition(in Vector3 basePosition, in Quaternion rotation)
		{
			_basePosition = basePosition;
			_rotation = keyframeRotation;
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z)
		{
			// convert to unity coordinates
			var newPos = new Vector3(x, y, z);
			newPos = _rotation * newPos;

			X.Add(new Keyframe(time, newPos.x));
			Y.Add(new Keyframe(time, newPos.y));
			Z.Add(new Keyframe(time, newPos.z));
		}

		public Keyframe[] GetKeyFramesPosX() => X.ToArray();
		public Keyframe[] GetKeyFramesPosY() => Y.ToArray();
		public Keyframe[] GetKeyFramesPosZ() => Z.ToArray();

		public AnimationCurve GetAnimationCurvePosX() => new AnimationCurve(GetKeyFramesPosX());
		public AnimationCurve GetAnimationCurvePosY() => new AnimationCurve(GetKeyFramesPosY());
		public AnimationCurve GetAnimationCurvePosZ() => new AnimationCurve(GetKeyFramesPosZ());
	}

	public class KeyFramesScale
	{
		protected List<Keyframe> X;
		protected List<Keyframe> Y;
		protected List<Keyframe> Z;

		private Vector3 _baseScale;
		private Quaternion _rotation;

		public KeyFramesScale(in Vector3 baseScale)
			: this(baseScale, Quaternion.identity)
		{
		}

		public KeyFramesScale(in Vector3 baseScale, in Quaternion rotation)
		{
			_baseScale = baseScale;
			_rotation = keyframeRotation;
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z)
		{
			// convert to unity coordinates
			var newScale = new Vector3(x, y, z);
			newScale = _rotation * newScale;
			newScale.x = Mathf.Abs(newScale.x);
			newScale.y = Mathf.Abs(newScale.y);
			newScale.z = Mathf.Abs(newScale.z);
			// newScale.Scale(_baseScale);

			X.Add(new Keyframe(time, newScale.x));
			Y.Add(new Keyframe(time, newScale.y));
			Z.Add(new Keyframe(time, newScale.z));
		}

		public Keyframe[] GetKeyFramesScaleX() => X.ToArray();
		public Keyframe[] GetKeyFramesScaleY() => Y.ToArray();
		public Keyframe[] GetKeyFramesScaleZ() => Z.ToArray();

		public AnimationCurve GetAnimationCurveScaleX() => new AnimationCurve(GetKeyFramesScaleX());
		public AnimationCurve GetAnimationCurveScaleY() => new AnimationCurve(GetKeyFramesScaleY());
		public AnimationCurve GetAnimationCurveScaleZ() => new AnimationCurve(GetKeyFramesScaleZ());
	}

	public class KeyFramesRotation
	{
		public List<Keyframe> X;
		public List<Keyframe> Y;
		public List<Keyframe> Z;
		public List<Keyframe> W;
		private Quaternion _baseRotation;
		private Quaternion _rotation;

		public KeyFramesRotation(in Quaternion baseRotation, in Quaternion rotation)
		{
			_baseRotation = baseRotation;
			_rotation = keyframeRotation;
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
			W = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z, in float w)
		{
			var rotationKey = new Quaternion(x, y, z, w);
			rotationKey = _rotation  * rotationKey;
			// rotationKey = _baseRotation;

			X.Add(new Keyframe(time, rotationKey.x));
			Y.Add(new Keyframe(time, rotationKey.y));
			Z.Add(new Keyframe(time, rotationKey.z));
			W.Add(new Keyframe(time, rotationKey.w));
		}

		public Keyframe[] GetKeyFramesPosX() => X.ToArray();
		public Keyframe[] GetKeyFramesPosY() => Y.ToArray();
		public Keyframe[] GetKeyFramesPosZ() => Z.ToArray();
		public Keyframe[] GetKeyFramesPosW() => W.ToArray();
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

	public static List<AnimationClip> LoadAnimations(in string filePath, in Dictionary<string, Tuple<string, Transform>> relativePaths, in Quaternion boneRotation)
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
				clip.name = GetAnimationClipName(++clipIndex, animation.Name);
				clip.legacy = true;
				clip.wrapMode = WrapMode.Loop;
				Debug.Log("Tick/Sec=" + animation.TicksPerSecond + ", DurationInTicks=" + animation.DurationInTicks);
				clip.frameRate = 30;
				Debug.Log("framrate=" + clip.frameRate + ", islooping:" + clip.isLooping + ", localbound:" + clip.localBounds);

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
						var relativeName = relativePaths[node.NodeName].Item1;
						var relativeTransform = relativePaths[node.NodeName].Item2;
						Debug.Log(relativeName + ", " + node.PreState + ", " + node.PostState);

						Debug.Log("PositionKeyCount=" + node.PositionKeyCount);
						var keyFramesPos = new KeyFramesPosition(relativeTransform.localPosition);
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

						var keyFramesRot = new KeyFramesRotation(relativeTransform.localRotation, boneRotation);
						Debug.Log("RotationKeyCount=" + node.RotationKeyCount);
						foreach (var rotationKey in node.RotationKeys)
						{
							var vectorKey = rotationKey.Value;
							keyFramesRot.Add((float)(float)rotationKey.Time, vectorKey.X, vectorKey.Y, vectorKey.Z, vectorKey.W);
							Debug.Log("["+node.NodeName+"]RotationKey: " + (float)rotationKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z + ", " + vectorKey.W);
						}

						var curveRotX = new AnimationCurve(keyFramesRot.GetKeyFramesPosX());
						var curveRotY = new AnimationCurve(keyFramesRot.GetKeyFramesPosY());
						var curveRotZ = new AnimationCurve(keyFramesRot.GetKeyFramesPosZ());
						var curveRotW = new AnimationCurve(keyFramesRot.GetKeyFramesPosW());

						clip.SetCurve(relativeName, typeof(Transform), "localRotation.x", curveRotX);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.y", curveRotY);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.z", curveRotZ);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.w", curveRotW);

						var keyFramesScale = new KeyFramesScale(relativeTransform.localScale);
						Debug.Log("ScalingKeyCount=" + node.ScalingKeyCount);
						foreach (var scalingKey in node.ScalingKeys)
						{
							var vectorKey = scalingKey.Value;
							keyFramesScale.Add((float)scalingKey.Time, vectorKey.X, vectorKey.Y, vectorKey.Z);
							Debug.Log("["+node.NodeName+"]ScalingKey: " + (float)scalingKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z);
						}

						var curveScaleX = new AnimationCurve(keyFramesScale.GetKeyFramesScaleX());
						var curveScaleY = new AnimationCurve(keyFramesScale.GetKeyFramesScaleY());
						var curveScaleZ = new AnimationCurve(keyFramesScale.GetKeyFramesScaleZ());
						clip.SetCurve(relativeName, typeof(Transform), "localScale.x", curveScaleX);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.y", curveScaleY);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.z", curveScaleZ);

						keyframeRotation = Quaternion.Euler(0, 0, 0);
					}
				}

				Debug.Log("Length : " + clip.legacy);
				animationClipList.Add(clip);
			}
		}
		return animationClipList;
	}
}