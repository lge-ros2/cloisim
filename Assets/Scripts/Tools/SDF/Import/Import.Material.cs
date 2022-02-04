/*
 * Copyright (c) 2020 LG Electronics Inc.
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
			protected override void ImportMaterial(in SDF.Material sdfMaterial, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);

				if (targetObject == null || sdfMaterial == null)
				{
					return;
				}

				foreach (var renderer in targetObject.GetComponentsInChildren<UE.Renderer>(true))
				{
					var sharedMaterial = renderer.sharedMaterial;

					if (sharedMaterial == null)
					{
						UE.Debug.Log(targetObject + ": sharedMaterial is null");
						continue;
					}

					if (sdfMaterial.ambient != null)
					{
						// UE.Debug.Log(sharedMaterial.name + ": ambient is not support. " + 	SDF2Unity.GetColor(sdfMaterial.ambient));
					}

					if (sdfMaterial.diffuse != null)
					{
						var diffuseColor = SDF2Unity.GetColor(sdfMaterial.diffuse);
						sharedMaterial.SetColor("_BaseColor", diffuseColor);

						if (diffuseColor.a < 1)
						{
							SDF2Unity.SetMaterialTransparent(sharedMaterial);
						}
						else
						{
							SDF2Unity.SetMaterialOpaque(sharedMaterial);
						}
					}

					if (sdfMaterial.emissive != null)
					{
						sharedMaterial.SetColor("_EmissionColor", SDF2Unity.GetColor(sdfMaterial.emissive));
					}

					if (sdfMaterial.specular != null)
					{
						sharedMaterial.SetColor("_SpecColor", SDF2Unity.GetColor(sdfMaterial.specular));
					}

					if (sdfMaterial.script != null)
					{
						if (sdfMaterial.script.name.ToLower().Contains("tree"))
						{
							SDF2Unity.SetMaterialSpeedTree(sharedMaterial);
						}
					}
				}
			}
		}
	}
}