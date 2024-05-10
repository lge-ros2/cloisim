/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System;
using UE = UnityEngine;

namespace SDF
{
	namespace Implement
	{
		public partial class Material
		{
			public static class Ogre
			{
				private static string FindFile(in List<string> uris, in string targetFileName)
				{
					var ext = Path.GetExtension(targetFileName);

					foreach (var currentDir in uris)
					{
						var subdirectoryEntries = Directory.GetDirectories(currentDir);
						var subDirectories = new List<string>();
						subDirectories.AddRange(subdirectoryEntries);
						subDirectories.Insert(0, currentDir);
						foreach (var subdirectory in subDirectories)
						{
							var fileEntries = Directory.GetFiles(subdirectory, "*" + ext);
							foreach (var fileName in fileEntries)
							{
								if (fileName.EndsWith(targetFileName))
									return fileName;
							}
						}
					}

					return string.Empty;
				}


				private static void ApplyVertexColour(in Dictionary<string, string> passProperties, UE.Material material)
				{
					if (passProperties.ContainsKey("diffuse"))
					{
						var diffuse = passProperties["diffuse"].Trim();
						SDF2Unity.Material.SetBaseColor(material, SDF2Unity.Color(diffuse));
					}

					if (passProperties.ContainsKey("emissive"))
					{
						var emissive = passProperties["emissive"].Trim();
						SDF2Unity.Material.SetEmission(material, SDF2Unity.Color(emissive));
					}

					if (passProperties.ContainsKey("specular"))
					{
						var specular = passProperties["specular"].Trim();

						var tmp = specular.Split(' ', StringSplitOptions.RemoveEmptyEntries);
						if (tmp.Length == 5)
						{
							var shininess = Convert.ToSingle(tmp[4]);
							// ObsoleteProperties in Simple lit
							material.SetFloat("_Shininess", shininess);

							specular = string.Join(" ", tmp, 0, 4);
						}
						else if (tmp.Length == 4)
						{
							var alpha = Convert.ToSingle(tmp[3]);
							if (alpha > 1)
							{
								material.SetFloat("_Shininess", alpha);
								var r = Convert.ToSingle(tmp[0]);
								var g = Convert.ToSingle(tmp[1]);
								var b = Convert.ToSingle(tmp[2]);
								tmp[3] = Convert.ToString((r + g + b) / 3f);
							}

							specular = string.Join(" ", tmp, 0, 4);
						}

						SDF2Unity.Material.SetSpecular(material, SDF2Unity.Color(specular));
					}

					if (passProperties.ContainsKey("scene_blend"))
					{
						var sceneBlend = passProperties["scene_blend"].Trim();
						if (sceneBlend == "alpha_blend")
						{
							SDF2Unity.Material.SetTransparent(material);
						}
					}
				}

				private static void ApplyTexture(
					in string path,
					in Dictionary<string, string> props,
					 UE.Material material)
				{
					var texture = MeshLoader.GetTexture(path);
					if (texture != null)
					{
						if (props.ContainsKey("filtering"))
						{
							var textureFiltering = props["filtering"];
							// to make upper in First character
							switch (textureFiltering)
							{
								case "bilinear":
									texture.filterMode = UE.FilterMode.Bilinear;
									break;
								case "trilinear":
								case "anisotropic":
									texture.filterMode = UE.FilterMode.Trilinear;
									break;
								case "none":
								default:
									texture.filterMode = UE.FilterMode.Point;
									break;
							}
						}

						if (props.ContainsKey("max_anisotropy"))
						{
							var textureAnisotropy = props["max_anisotropy"];
							texture.anisoLevel = Convert.ToInt32(textureAnisotropy);
						}

						if (props.ContainsKey("scale"))
						{
							var scaleSet = props["scale"];
							var tileScale = SDF2Unity.Scale(scaleSet);

							// UE.Debug.Log(tileScale);

							// TODO: Check texture tile scaling
							tileScale.x = 1 / tileScale.x;
							tileScale.y = 1 / tileScale.y;

							material.SetTextureScale("_BaseMap", tileScale);
						}

						material.SetTexture("_BaseMap", texture);
					}
					else
					{
						UE.Debug.LogWarning($"Wrong texture path: {path}");
					}
				}

				private static void ApplyTextureUnits(
					in Dictionary<string, OgreMaterial.TextureUnit> textrueUnits,
					UE.Material material,
					in List<string> uris)
				{
					foreach (var textureUnitEntry in textrueUnits)
					{
						var textureunit = textureUnitEntry.Value;

						// UE.Debug.Log($"    TextureUnit: {textureunitEntry.Key}");
						// foreach (var kvp in textureunit.properties)
						// {
						// 	UE.Debug.Log($"      TextureUnit: {kvp.Key} -> {kvp.Value}");
						// }
						var textureUnitProps = textureunit.properties;

						if (!textureUnitProps.ContainsKey("texture"))
						{
							continue;
						}

						var textureFileName = textureUnitProps["texture"];
						var textureFilePath = FindFile(uris, textureFileName);

						// UE.Debug.Log(textureFilePath + ", " + textureFileName);
						if (string.IsNullOrEmpty(textureFilePath))
						{
							continue;
						}

						ApplyTexture(textureFilePath, textureUnitProps, material);
						return;
					}
				}

				private static UE.Material[] ResizeMaterials(in OgreMaterial.Material ogreMaterial, in UE.Material[] baseMaterials)
				{
					var requiredMaterialCount = 0;
					foreach (var techEntry in ogreMaterial.techniques)
					{
						var technique = techEntry.Value;
						requiredMaterialCount += technique.passes.Count;
					}
					// UE.Debug.LogWarning($"requiredMaterialCount: {requiredMaterialCount}");

					// resizing materials
					UE.Material[] materials = null;
					if (baseMaterials.Length < requiredMaterialCount)
					{
						var prevMaterials = (UE.Material[])baseMaterials.Clone();
						var lastMaterial = prevMaterials[prevMaterials.Length - 1];

						materials = new UE.Material[requiredMaterialCount];
						for (var i = 0; i < prevMaterials.Length; i++)
						{
							materials[i] = prevMaterials[i];
						}
						for (var i = prevMaterials.Length - 1; i < requiredMaterialCount; i++)
						{
							materials[i] = new UE.Material(lastMaterial);
						}

						// UE.Debug.Log("Resizing materials " + materials.Length);
					}
					else
					{
						materials = baseMaterials;
					}

					return materials;
				}

				public static UE.Material[] ApplyMaterial(
					in OgreMaterial.Material ogreMaterial,
					UE.Material[] baseMaterials,
					in List<string> texturePathURIs)
				{
					var materials = ResizeMaterials(ogreMaterial, baseMaterials);

					if (ogreMaterial.hasReceiveShadows)
					{
						foreach (var material in materials)
						{
							material.SetFloat("_ReceiveShadows", ogreMaterial.receiveShadows ? 1f : 0);
						}
					}

					var materialIndex = 0;
					foreach (var techEntry in ogreMaterial.techniques)
					{
						var technique = techEntry.Value;

						foreach (var passEntry in technique.passes)
						{
							var pass = passEntry.Value;

							// UE.Debug.Log($"Passes in Technique: {technique.passes.Count}");
							// foreach (var kvp in pass.properties)
							// {
							// 	UE.Debug.Log($"  Pass: {kvp.Key}: {kvp.Value}");
							// }

							var material = materials[materialIndex++];

							ApplyVertexColour(pass.properties, material);

							ApplyTextureUnits(pass.textureUnits, material, texturePathURIs);
						}
					}

					return materials;
				}
			}
		}
	}
}