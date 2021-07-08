/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using UEAI = UnityEngine.AI;

namespace SDF
{
	namespace Helper
	{
		public class Model : Base
		{
			public bool hasRootArticulationBody;

			[UE.Header("SDF Properties")]
			public bool isStatic;

			void Start()
			{
				if (isStatic)
				{
					// if parent model has static option, make it all static in children
					ConvertToStaticLink();
				}
				else
				{
					if (hasRootArticulationBody)
					{
						var navMeshObstacle = gameObject.AddComponent<UEAI.NavMeshObstacle>();
						navMeshObstacle.carving = true;
						navMeshObstacle.carveOnlyStationary = false;

						var bounds = new UE.Bounds();
						var renderers = transform.GetComponentsInChildren<UE.Renderer>();
						for (var i = 0; i < renderers.Length; i++)
						{
							bounds.size = UE.Vector3.Max(bounds.size, renderers[i].bounds.size);
						}
						bounds.size = transform.rotation * bounds.size;
						navMeshObstacle.carvingMoveThreshold = 0.2f;
						navMeshObstacle.size = bounds.size * 0.7f;
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

				foreach (var childArticulationBody in GetComponentsInChildren<UE.ArticulationBody>())
				{
					if (childArticulationBody.isRoot)
					{
						childArticulationBody.immovable = true;
					}
				}
			}

			void LateUpdate()
			{
				SetPose(transform.localPosition, transform.localRotation, 1);
			}
		}
	}
}