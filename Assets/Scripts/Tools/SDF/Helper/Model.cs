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

			[UE.Header("SDF Properties")]
			public bool isStatic = false;

			new void Awake()
			{
				base.Awake();
			}

			void Start()
			{
				if (isTopModel)
				{
					SetArticulationBody();

					if (isStatic)
					{
						// if parent model has static option, make it all static in child
						ConvertToStaticLink();
					}
				}
			}

			public Model GetThisInTopParent()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				return (Model)modelHelpers[modelHelpers.Length - 1];
			}

			public Link[] GetLinksInChildren()
			{
				return GetComponentsInChildren<Link>();
			}

			private void ConvertToStaticLink()
			{
				this.gameObject.isStatic = true;

				foreach (var childGameObject in GetComponentsInChildren<UE.Transform>())
				{
					childGameObject.gameObject.isStatic = true;
				}
			}
		}
	}
}