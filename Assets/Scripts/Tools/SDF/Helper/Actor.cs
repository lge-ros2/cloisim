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
		public class Actor : Base
		{
			[UE.Header("SDF Properties")]
			public bool isStatic = false;

			private UE.Quaternion _boneRotation = UE.Quaternion.identity;

			new void Awake()
			{
				base.Awake();
			}

			void Start()
			{
			}

			void LateUpdate()
			{
			}

			public UE.Quaternion BoneRotation
			{
				get => _boneRotation;
				set => _boneRotation = value;
			}
		}
	}
}