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
		}
	}
}