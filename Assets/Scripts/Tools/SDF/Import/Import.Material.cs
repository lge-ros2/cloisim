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

				if (targetObject == null)
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

					if (sdfMaterial != null)
					{
						var colorAmbientAndDiffuse = UE.Color.clear;

						if (sdfMaterial.ambient != null)
						{
							colorAmbientAndDiffuse += SDF2Unity.GetColor(sdfMaterial.ambient);
						}

						if (sdfMaterial.diffuse != null)
						{
							colorAmbientAndDiffuse += SDF2Unity.GetColor(sdfMaterial.diffuse);
						}

						// sharedMaterial.SetColor("_BaseColor", colorAmbientAndDiffuse);
						sharedMaterial.color = colorAmbientAndDiffuse;

						if (sdfMaterial.emissive != null)
						{
							sharedMaterial.SetColor("_EmissionColor", SDF2Unity.GetColor(sdfMaterial.emissive));
						}
						else
						{
							sharedMaterial.SetColor("_EmissionColor", UE.Color.black);
						}

						if (sdfMaterial.specular != null)
						{
							sharedMaterial.SetColor("_SpecColor", SDF2Unity.GetColor(sdfMaterial.specular));
						}
						else
						{
							sharedMaterial.SetColor("_SpecColor", UE.Color.black);
						}
					}

					renderer.sharedMaterial = sharedMaterial;

					// sharedMaterial.hideFlags |= UE.HideFlags.NotEditable;
				}
			}
		}
	}
}