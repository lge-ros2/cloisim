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
			private Model _rootModelInScope = null;

			private PoseControl _poseControl = null;

			private bool _isRootModel = false;

			[UE.Header("SDF Properties")]
			private SDF.Pose<double> _pose = null; // described in SDF file

			public Pose<double> Pose
			{
				get => _pose;
				set => _pose = value;
			}

			public Model RootModel => _rootModelInScope;

			public bool IsFirstChild => _isRootModel; // root model

			protected void Awake()
			{
				_isRootModel = this.gameObject.IsRootModel();
				_poseControl = new PoseControl(this.transform);
				Reset();

				UpdateRootModel();
			}

			protected void Start()
			{
				UpdateRootModel();
			}

			public void Reset()
			{
				ResetPose();
			}

			private void UpdateRootModel()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				if (modelHelpers.Length > 0)
				{
					_rootModelInScope = (Model)modelHelpers[modelHelpers.Length - 1];
				}
			}

			public void ClearPose()
			{
				if (_poseControl != null)
				{
					_poseControl.Clear();
				}
			}

			public void SetJointPoseTarget(
				in UE.Vector3 axis1xyz, in float targetAxis1,
				in UE.Vector3 axis2xyz, in float targetAxis2,
				in int targetFrame = 0)
			{
				if (_poseControl != null)
				{
					_poseControl.SetJointTarget(axis1xyz, targetAxis1, axis2xyz, targetAxis2, targetFrame);
				}
			}

			public void SetPose(in UE.Pose pose, in int targetFrame = 0)
			{
				SetPose(pose.position, pose.rotation, targetFrame);
			}

			public void SetPose(in UE.Vector3 position, in UE.Quaternion rotation, in int targetFrame = 0)
			{
				if (_poseControl != null)
				{
					_poseControl.Set(position, rotation, targetFrame);
				}
			}

			public void ResetPose()
			{
				if (_poseControl != null)
				{
					_poseControl.Reset();
				}
			}

			public UE.Pose GetPose(in int targetFrame = 0)
			{
				return (_poseControl != null) ? _poseControl.Get(targetFrame) : UE.Pose.identity;
			}

			public int GetPoseCount()
			{
				return (_poseControl != null) ? _poseControl.Count : 0;
			}
		}
	}
}
