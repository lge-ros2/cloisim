/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// using System.Collections.Generic;
// using System.Linq;
using UE = UnityEngine;
// using Debug = UnityEngine.Debug;

namespace SDF
{
	public partial class Implement
	{
		public class Actor
		{
			public static void SetSkin(in SDF.Actor.Skin skin, in UE.GameObject targetObject)
			{
				SDF2Unity.LoadSkinObject(targetObject, skin.filename);
			}

			public static void SetAnimation(in SDF.Actor.Animation animation, in UE.GameObject targetObject)
			{
				// var animationObject = SDF2Unity.LoadMeshObject(animation.filename);
				// animationObject.transform.SetParent(targetObject.transform);
			}

			public static void SetScript(in SDF.Actor.Script script, in UE.GameObject targetObject)
			{
			}
		}
	}
}