/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private UE.GameObject _rootModels = null;
			private UE.GameObject _rootLights = null;
			private UE.GameObject _rootRoads = null;

			public void SetRootModels(in UE.GameObject root)
			{
				_rootModels = root;
			}

			public void SetRootLights(in UE.GameObject root)
			{
				_rootLights = root;
			}

			public void SetRootRoads(in UE.GameObject root)
			{
				_rootRoads = root;
			}

			private void SetParentObject(UE.GameObject childObject, UE.GameObject parentObject)
			{
				childObject.transform.position = UE.Vector3.zero;
				childObject.transform.rotation = UE.Quaternion.identity;

				var targetParentTransform = (parentObject == null) ? _rootModels.transform : parentObject.transform;
				childObject.transform.SetParent(targetParentTransform, false);

				childObject.transform.localScale = UE.Vector3.one;
				childObject.transform.localPosition = UE.Vector3.zero;
				childObject.transform.localRotation = UE.Quaternion.identity;
			}
		}
	}
}