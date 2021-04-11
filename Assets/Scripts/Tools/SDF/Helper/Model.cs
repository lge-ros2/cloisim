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
		public class Model : Base
		{
			public bool isTopModel;
			public bool hasRootArticulationBody;

			[UE.Header("SDF Properties")]
			public bool isStatic;

			new void Awake()
			{
				base.Awake();
			}

			void Start()
			{
				if (isTopModel)
				{
					if (isStatic)
					{
						// if parent model has static option, make it all static in child
						ConvertToStaticLink();
					}
				}
			}

			private void ConvertToStaticLink()
			{
				gameObject.isStatic = true;

				foreach (var childGameObject in GetComponentsInChildren<UE.Transform>())
				{
					childGameObject.gameObject.isStatic = true;
				}
			}
		}
	}
}