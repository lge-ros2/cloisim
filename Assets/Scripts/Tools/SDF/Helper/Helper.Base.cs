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
			private PoseControl _poseControl = null;

			protected void Awake()
			{
				_poseControl = new PoseControl(this.transform);
			}

			public void Reset()
			{
				if (_poseControl != null)
				{
					_poseControl.Reset();
				}
			}

			public void SetArticulationBody()
			{
				if (_poseControl != null)
				{
					_poseControl.SetArticulationBody();
				}
			}

			public void SetPose(in UE.Vector3 position, in UE.Quaternion rotation)
			{
				AddPose(position, rotation);
				Reset();
			}

			public void AddPose(in UE.Vector3 position, in UE.Quaternion rotation)
			{
				if (_poseControl != null)
				{
					_poseControl.Add(position, rotation);
				}
			}

			public UE.Pose GetPose(in int targetFrame = 0)
			{
				return _poseControl.Get(targetFrame);
			}
		}
	}
}
