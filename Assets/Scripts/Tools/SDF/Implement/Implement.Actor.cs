/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	public partial class Implement
	{
		public class Actor
		{
			public static UE.GameObject CreateSkin(in SDF.Actor.Skin skin)
			{
				return MeshLoader.CreateSkinObject(skin.filename);
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