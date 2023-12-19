/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Base : UE.MonoBehaviour
		{
			private PoseControl poseControl = null;

			private bool isFirstChild = false;

			public bool IsFirstChild => isFirstChild; // root model

			protected void Awake()
			{
				isFirstChild = SDF2Unity.IsRootModel(this.gameObject);
				poseControl = new PoseControl(this.transform);
				Reset();
			}

			public void Reset()
			{
				ResetPose();
			}

			public void ClearPose()
			{
				if (poseControl != null)
				{
					poseControl.Clear();
				}
			}

			public void SetJointPoseTarget(in float targetAxis1, in float targetAxis2, in int targetFrame = 0)
			{
				if (poseControl != null)
				{
					poseControl.SetJointTarget(targetAxis1, targetAxis2, targetFrame);
				}
			}

			public void SetPose(in UE.Pose pose, in int targetFrame = 0)
			{
				SetPose(pose.position, pose.rotation, targetFrame);
			}

			public void SetPose(in UE.Vector3 position, in UE.Quaternion rotation, in int targetFrame = 0)
			{
				if (poseControl != null)
				{
					poseControl.Set(position, rotation, targetFrame);
				}
			}

			public void ResetPose()
			{
				if (poseControl != null)
				{
					poseControl.Reset();
				}
			}

			public UE.Pose GetPose(in int targetFrame = 0)
			{
				return (poseControl != null) ? poseControl.Get(targetFrame) : UE.Pose.identity;
			}

			public int GetPoseCount()
			{
				return (poseControl != null) ? poseControl.Count : 0;
			}
		}
	}
}
