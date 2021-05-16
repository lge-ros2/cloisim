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

				if (IsFirstChild)
				{
					var meshColliders = gameObject.GetComponentsInChildren<UE.MeshCollider>();
					var combine = new UE.CombineInstance[meshColliders.Length];
					for (var i = 0; i < combine.Length; i++)
					{
						combine[i].mesh = meshColliders[i].sharedMesh;
						combine[i].transform = meshColliders[i].transform.localToWorldMatrix;
					}

					var combinedMesh = new UE.Mesh();
					combinedMesh.CombineMeshes(combine, true, true);
					combinedMesh.RecalculateBounds();
					combinedMesh.Optimize();
					// UE.Debug.Log(gameObject.name + ", " + combinedMesh.bounds.size + ", " + combinedMesh.bounds.extents+ ", " + combinedMesh.bounds.center);

					var cornerPoints = GetBoundCornerPointsByExtents(combinedMesh.bounds.extents);
					SetFootPrint(cornerPoints);
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
				if (IsFirstChild)
				{
					foreach (var plugin in GetComponentsInChildren<CLOiSimPlugin>())
					{
						plugin.StopThread();
					}
				}
			}
		}
	}
}