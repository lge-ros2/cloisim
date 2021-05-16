/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Base: UE.MonoBehaviour
		{
			private PoseControl poseControl = null;
			private bool isFirstChild = false;

			private UE.Vector3 velocity = UE.Vector3.zero;
			private UE.Vector3 position = UE.Vector3.zero;
			private List<UE.Vector3> footprint = new List<UE.Vector3>();

			public bool IsFirstChild => isFirstChild;
			public UE.Vector3 Velocity => velocity;
			public UE.Vector3 Position => position;
			public List<UE.Vector3> FootPrints => footprint;

			protected void Awake()
			{
				isFirstChild = SDF2Unity.IsRootModel(this.gameObject);
				poseControl = new PoseControl(this.transform);
			}

			public void Reset()
			{
				if (poseControl != null)
				{
					poseControl.Reset();
				}
			}

			public void SetPose(in UE.Vector3 position, in UE.Quaternion rotation)
			{
				AddPose(position, rotation);
				Reset();
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
