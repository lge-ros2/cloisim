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


				private static void ApplyVertexColour(in OgreMaterial.Pass pass, UE.Material material)
				{
					if (pass.properties.ContainsKey("diffuse"))
					{
						var diffuse = pass.properties["diffuse"].Trim();
						SDF2Unity.Material.SetBaseColor(material, SDF2Unity.Color(diffuse));
					}

					if (pass.properties.ContainsKey("emissive"))
					{
						var emissive = pass.properties["emissive"].Trim();
						SDF2Unity.Material.SetEmission(material, SDF2Unity.Color(emissive));
					}

					if (pass.properties.ContainsKey("specular"))
					{
						var specular = pass.properties["specular"].Trim();

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
				}

				private static void ApplyTextureUnits(in OgreMaterial.Pass pass, UE.Material material, in List<string> uris)
				{
					foreach (var textureunitEntry in pass.textureUnits)
					{
						var textureunit = textureunitEntry.Value;

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

						var texture = MeshLoader.GetTexture(textureFilePath);
						if (texture != null)
						{
							if (textureUnitProps.ContainsKey("filtering"))
							{
								var textureFiltering = textureUnitProps["filtering"];
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

							if (textureUnitProps.ContainsKey("max_anisotropy"))
							{
								var textureAnisotropy = textureUnitProps["max_anisotropy"];
								texture.anisoLevel = Convert.ToInt32(textureAnisotropy);
							}

							if (textureUnitProps.ContainsKey("scale"))
							{
								var scaleSet = textureUnitProps["scale"];
								var tileScale = SDF2Unity.Scale(scaleSet);

								// TODO: Check texture tile scaling
								tileScale.x = 1 / tileScale.x;
								tileScale.y = 1 / tileScale.y;

								material.SetTextureScale("_BaseMap", tileScale);
							}

							material.SetTexture("_BaseMap", texture);
						}

						return;
					}
				}

				public static void ApplyMaterial(in OgreMaterial.Material ogreMaterial, UE.Material material, in List<string> uris)
				{
					if (ogreMaterial.hasReceiveShadows)
					{
						material.SetFloat("_ReceiveShadows", ogreMaterial.receiveShadows ? 1f : 0);
					}

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

							ApplyVertexColour(pass, material);

							ApplyTextureUnits(pass, material, uris);
						}
					}
				}
			}
		}
	}
}