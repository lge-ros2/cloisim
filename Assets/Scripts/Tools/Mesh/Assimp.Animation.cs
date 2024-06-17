/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

public static partial class MeshLoader
{
	public class KeyFramesPosition
	{
		private List<Keyframe> X;
		private List<Keyframe> Y;
		private List<Keyframe> Z;

		public KeyFramesPosition()
		{
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z)
		{
			// convert to unity coordinates
			var newPos = new Vector3(x, y, z);

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

		public KeyFramesScale()
		{
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z)
		{
			// convert to unity coordinates
			var newScale = new Vector3(x, y, z);

			X.Add(new Keyframe(time, Mathf.Abs(newScale.x)));
			Y.Add(new Keyframe(time, Mathf.Abs(newScale.y)));
			Z.Add(new Keyframe(time, Mathf.Abs(newScale.z)));
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

		public KeyFramesRotation()
		{
			X = new List<Keyframe>();
			Y = new List<Keyframe>();
			Z = new List<Keyframe>();
			W = new List<Keyframe>();
		}

		public void Add(in float time, in float x, in float y, in float z, in float w)
		{
			var rotationKey = new Quaternion(x, y, z, w);

			X.Add(new Keyframe(time, rotationKey.x));
			Y.Add(new Keyframe(time, rotationKey.y));
			Z.Add(new Keyframe(time, rotationKey.z));
			W.Add(new Keyframe(time, rotationKey.w));
		}

		public Keyframe[] GetKeyFramesRotX() => X.ToArray();
		public Keyframe[] GetKeyFramesRotY() => Y.ToArray();
		public Keyframe[] GetKeyFramesRotZ() => Z.ToArray();
		public Keyframe[] GetKeyFramesRotW() => W.ToArray();

		public AnimationCurve GetAnimationCurveRotX() => new AnimationCurve(GetKeyFramesRotX());
		public AnimationCurve GetAnimationCurveRotY() => new AnimationCurve(GetKeyFramesRotY());
		public AnimationCurve GetAnimationCurveRotZ() => new AnimationCurve(GetKeyFramesRotZ());
		public AnimationCurve GetAnimationCurveRotW() => new AnimationCurve(GetKeyFramesRotW());
	}

	public static AnimationClip LoadAnimation(in string animationName, in string filePath, in Dictionary<string, string> relativePaths, in float scale = 1)
	{
		var scene = GetScene(filePath);
		if (scene == null)
		{
			return null;
		}

		var clip = new AnimationClip();
		clip.name = animationName;

		if (scene.HasAnimations)
		{
			// Debug.Log("Total Animations = " + scene.AnimationCount);
			var lastAnimationTime = 0f;
			const float AnimationSpeedTimeRatio = 0.8f;
			foreach (var animation in scene.Animations)
			{
				clip.legacy = true;
				clip.wrapMode = WrapMode.Loop;
				clip.frameRate = 30;
				// Debug.Log("Tick/Sec=" + animation.TicksPerSecond + ", DurationInTicks=" + animation.DurationInTicks);
				// Debug.Log("framrate=" + clip.frameRate + ", islooping:" + clip.isLooping + ", localbound:" + clip.localBounds);

				if (animation.HasMeshAnimations)
				{
					Debug.LogWarningFormat("Mesh Animation({0}) is not support yet!");
					Debug.LogWarning("MeshAnimationChannelCount=" + animation.MeshAnimationChannelCount);
					Debug.LogWarning("MeshMorphAnimationChannelCount=" + animation.MeshMorphAnimationChannelCount);
				}
				else if (animation.HasNodeAnimations)
				{
					var isRoot = true;
					var lastMaxAnimationTime = 0f;
					// Debug.Log(animationName + ", Total NodeAnimationChannels = " + animation.NodeAnimationChannels.Count);
					foreach (var node in animation.NodeAnimationChannels)
					{
						var relativeName = relativePaths[node.NodeName];
						// Debug.Log(relativeName + ", " + node.PreState + ", " + node.PostState);

						// Debug.Log("PositionKeyCount=" + node.PositionKeyCount);
						var keyFramesPos = new KeyFramesPosition();
						var lastPostionKeyTime = 0f;
						foreach (var positionKey in node.PositionKeys)
						{
							var vectorKey = positionKey.Value;
							lastPostionKeyTime = lastAnimationTime + ((float)positionKey.Time * AnimationSpeedTimeRatio);
							keyFramesPos.Add(lastPostionKeyTime, vectorKey.X, vectorKey.Y, vectorKey.Z);
							// Debug.Log("["+node.NodeName+"] PositionKey: " + lastPostionKeyTime + "..." + (float)positionKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z);
						}

						var curveX = keyFramesPos.GetAnimationCurvePosX();
						var curveY = keyFramesPos.GetAnimationCurvePosY();
						var curveZ = keyFramesPos.GetAnimationCurvePosZ();

						if (!isRoot)
						{
							clip.SetCurve(relativeName, typeof(Transform), "localPosition.x", curveX);
						}
						clip.SetCurve(relativeName, typeof(Transform), "localPosition.y", curveY);
						clip.SetCurve(relativeName, typeof(Transform), "localPosition.z", curveZ);

						var keyFramesRot = new KeyFramesRotation();
						// Debug.Log("RotationKeyCount=" + node.RotationKeyCount);
						var lastRotationKeyTime = 0f;
						foreach (var rotationKey in node.RotationKeys)
						{
							var vectorKey = rotationKey.Value;
							lastRotationKeyTime = lastAnimationTime + ((float)rotationKey.Time * AnimationSpeedTimeRatio);
							keyFramesRot.Add(lastRotationKeyTime, vectorKey.X, vectorKey.Y, vectorKey.Z, vectorKey.W);
							// Debug.Log("["+node.NodeName+"] RotationKey: " + lastRotationKeyTime + "..." +  (float)rotationKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z + ", " + vectorKey.W);
						}

						var curveRotX = keyFramesRot.GetAnimationCurveRotX();
						var curveRotY = keyFramesRot.GetAnimationCurveRotY();
						var curveRotZ = keyFramesRot.GetAnimationCurveRotZ();
						var curveRotW = keyFramesRot.GetAnimationCurveRotW();

						clip.SetCurve(relativeName, typeof(Transform), "localRotation.x", curveRotX);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.y", curveRotY);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.z", curveRotZ);
						clip.SetCurve(relativeName, typeof(Transform), "localRotation.w", curveRotW);

						var keyFramesScale = new KeyFramesScale();
						// Debug.Log("ScalingKeyCount=" + node.ScalingKeyCount);
						var lastScalingKeyTime = 0f;
						foreach (var scalingKey in node.ScalingKeys)
						{
							var vectorKey = scalingKey.Value * scale;
							lastScalingKeyTime = lastAnimationTime + ((float)scalingKey.Time * AnimationSpeedTimeRatio);
							keyFramesScale.Add(lastScalingKeyTime, vectorKey.X, vectorKey.Y, vectorKey.Z);
							// Debug.Log("["+node.NodeName+"] ScalingKey: " + lastScalingKeyTime + "..." + (float)scalingKey.Time + ", " + vectorKey.X + ", " + vectorKey.Y + ", " + vectorKey.Z);
						}

						var curveScaleX = keyFramesScale.GetAnimationCurveScaleX();
						var curveScaleY = keyFramesScale.GetAnimationCurveScaleY();
						var curveScaleZ = keyFramesScale.GetAnimationCurveScaleZ();

						clip.SetCurve(relativeName, typeof(Transform), "localScale.x", curveScaleX);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.y", curveScaleY);
						clip.SetCurve(relativeName, typeof(Transform), "localScale.z", curveScaleZ);

						isRoot = false;

						lastMaxAnimationTime = Mathf.Max(lastPostionKeyTime, Mathf.Max(lastRotationKeyTime, lastScalingKeyTime));
					}

					lastAnimationTime = lastMaxAnimationTime;
				}
			}
		}

		return clip;
	}
}