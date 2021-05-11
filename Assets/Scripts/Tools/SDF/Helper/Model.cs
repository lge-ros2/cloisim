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
				if (isStatic)
				{
					// if parent model has static option, make it all static in children
					ConvertToStaticLink();
				}
			}

			private void ConvertToStaticLink()
			{
				gameObject.isStatic = true;

				foreach (var childGameObject in GetComponentsInChildren<UE.Transform>())
				{
					childGameObject.gameObject.isStatic = true;
				}

				foreach (var childArticulationBody in GetComponentsInChildren<UE.ArticulationBody>())
				{
					if (childArticulationBody.isRoot)
					{
						childArticulationBody.immovable = true;
					}
				}
			}

			void OnDestroy()
			{
				if (isTopModel)
				{
					foreach (var plugin in GetComponentsInChildren<CLOiSimPlugin>())
					{
						plugin.Stop();
					}
				}
			}
		}
	}
}