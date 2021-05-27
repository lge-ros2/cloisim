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
		public class Base: UE.MonoBehaviour
		{
			private PoseControl poseControl = null;

			private bool isFirstChild = false;

			public bool IsFirstChild => isFirstChild;

			protected void Awake()
			{
				isFirstChild = SDF2Unity.IsRootModel(this.gameObject);
				poseControl = new PoseControl(this.transform);
				Reset();
			}

			public void Reset()
			{
				if (poseControl != null)
				{
					poseControl.Reset();
				}
			}

			public void SetPose(in UE.Pose pose)
			{
				SetPose(pose.position, pose.rotation);
			}

			public void SetPose(in UE.Vector3 position, in UE.Quaternion rotation)
			{
				poseControl.ClearPose();
				AddPose(position, rotation);
				Reset();
			}

			public void AddPose(in UE.Pose pose)
			{
				AddPose(pose.position, pose.rotation);
			}

			public void AddPose(in UE.Vector3 position, in UE.Quaternion rotation)
			{
				if (poseControl != null)
				{
					poseControl.Add(position, rotation);
				}
			}

			public void AddPose(in UE.Vector3 position)
			{
				AddPose(position, UE.Quaternion.identity);
			}

			public void AddPose(in UE.Quaternion rotation)
			{
				AddPose(UE.Vector3.zero, rotation);
			}

			public UE.Pose GetPose(in int targetFrame = 0)
			{
				return poseControl.Get(targetFrame);
			}

			public int GetPoseCount()
			{
				return poseControl.Count;
			}
		}
	}
}
