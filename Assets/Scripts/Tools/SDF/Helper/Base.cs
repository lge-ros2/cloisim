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

			protected UE.Vector3 velocity = UE.Vector3.zero;
			protected UE.Vector3 position = UE.Vector3.zero;
			protected List<UE.Vector3> footprint = new List<UE.Vector3>();

			public bool IsFirstChild => isFirstChild;

			public UE.Vector3 Velocity
			{
				get => velocity;
				set => velocity = value;
			}

			public UE.Vector3 Position
			{
				get => position;
				set => position = value;
			}

			public List<UE.Vector3> FootPrints => footprint;

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

				velocity = UE.Vector3.zero;
				position = transform.position;
			}

			protected void SetFootPrint(in UE.Vector3[] cornerPoints)
			{
				foreach (var cornerPoint in cornerPoints)
				{
					// UE.Debug.Log(cornerPoint.ToString("F6"));
					footprint.Add(cornerPoint);
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

			public static UE.Vector3[] GetBoundCornerPointsByExtents(in UE.Vector3 extents)
			{
				var cornerPoints = new UE.Vector3[] {
							extents,
							extents,
							extents,
							extents,
							extents * -1,
							extents * -1,
							extents * -1,
							extents * -1
						};

				cornerPoints[1].x *= -1;

				cornerPoints[2].x *= -1;
				cornerPoints[2].z *= -1;

				cornerPoints[3].z *= -1;

				cornerPoints[5].x *= -1;

				cornerPoints[6].x *= -1;
				cornerPoints[6].z *= -1;

				cornerPoints[7].z *= -1;

				return cornerPoints;
			}
		}
	}
}
