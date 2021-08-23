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
			private UE.GameObject _rootObjectModels = null;
			private UE.GameObject _rootObjectLights = null;

			public void SetRootModels(in UE.GameObject root)
			{
				_rootObjectModels = root;
			}

			public void SetRootLights(in UE.GameObject root)
			{
				_rootObjectLights = root;
			}

			private void SetParentObject(UE.GameObject childObject, UE.GameObject parentObject)
			{
				childObject.transform.position = UE.Vector3.zero;
				childObject.transform.rotation = UE.Quaternion.identity;

				var targetParentTransform = (parentObject == null) ? _rootObjectModels.transform : parentObject.transform;
				childObject.transform.SetParent(targetParentTransform, false);

				childObject.transform.localScale = UE.Vector3.one;
				childObject.transform.localPosition = UE.Vector3.zero;
				childObject.transform.localRotation = UE.Quaternion.identity;
			}
		}
	}
}