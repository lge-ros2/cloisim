/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Linq;
using System.IO;
using System;
using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Implement
	{
		public class Visual
		{
			public static void OptimizeMeshes(in UE.Transform targetTransform)
			{
				var meshFilters = targetTransform.GetComponentsInChildren<UE.MeshFilter>();

				if (meshFilters.Length <= 1)
				{
					return;
				}

				var materialTable = new Dictionary<string, UE.Material>();
				var meshFilterTable = new Dictionary<string, HashSet<UE.MeshFilter>>();

				foreach (var meshFilter in meshFilters)
				{
					var meshRenderer = meshFilter.gameObject.GetComponent<UE.MeshRenderer>();
					var material = meshRenderer.material;
					var materialName = material.name;

					if (!materialTable.ContainsKey(materialName))
					{
						materialTable.Add(materialName, material);
					}

					if (!meshFilterTable.ContainsKey(materialName))
					{
						var meshFilterSet = new HashSet<UE.MeshFilter>();
						meshFilterSet.Add(meshFilter);
						meshFilterTable.Add(materialName, meshFilterSet);
					}
					else
					{
						if (meshFilterTable.TryGetValue(materialName, out var value))
						{
							value.Add(meshFilter);
						}
						else
						{
							Debug.Log("Error!!");
							Debug.Break();
						}
					}

					if (!meshFilter.gameObject.CompareTag("Visual"))
					{
						UE.GameObject.Destroy(meshFilter.gameObject);
					}
				}

				foreach (var meshFilterSet in meshFilterTable)
				{
					if (!materialTable.TryGetValue(meshFilterSet.Key, out UE.Material material))
					{
						material = null;
					}

					var meshFilterList = meshFilterSet.Value.ToArray();
					var mergedMesh = SDF2Unity.MergeMeshes(meshFilterList);

					var newName = meshFilterSet.Key.Replace("(Instance)", "Combined").Trim();
					var newVisualGeometryObject = new UE.GameObject(newName);

					var meshFilter = newVisualGeometryObject.AddComponent<UE.MeshFilter>();
					mergedMesh.name = newName;
					meshFilter.sharedMesh = mergedMesh;

					var meshRenderer = newVisualGeometryObject.AddComponent<UE.MeshRenderer>();
					meshRenderer.material = material;

					newVisualGeometryObject.transform.SetParent(targetTransform, true);
				}
			}

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

			private static void ApplyOgreMaterial(in OgreMaterial.Material ogreMaterial, UE.Material material, in List<string> uris)
			{
				material.SetFloat("_ReceiveShadows", ogreMaterial.receiveShadows ? 1 : 0);

				foreach (var technique in ogreMaterial.techniques)
				{
					foreach (var pass in technique.passes)
					{
						// UE.Debug.Log($"Technique: {technique.passes.IndexOf(pass)}");
						// foreach (var kvp in pass.properties)
						// {
						// 	UE.Debug.Log($"  Pass: {kvp.Key}: {kvp.Value}");
						// }

						if (pass.properties.ContainsKey("diffuse"))
						{
							var diffuse = pass.properties["diffuse"];
							var diffuseColor = SDF2Unity.GetColor(diffuse);
							material.SetColor("_BaseColor", diffuseColor);

							if (diffuseColor.a < 1)
							{
								SDF2Unity.SetMaterialTransparent(material);
							}
							else
							{
								SDF2Unity.SetMaterialOpaque(material);
							}
						}
						else if (pass.properties.ContainsKey("emissive"))
						{
							var emissive = pass.properties["emissive"];
							var emissiveColor = SDF2Unity.GetColor(emissive);
							material.SetColor("_EmissionColor", emissiveColor);
						}
						else if (pass.properties.ContainsKey("specular"))
						{
							var specular = pass.properties["specular"];

							specular = specular.Trim();
							var tmp = specular.Split(' ');
							if (tmp.Length == 5)
							{
								var shininess = Convert.ToInt32(tmp[4]);
								// ObsoleteProperties in Simple lit
								material.SetFloat("_Shininess", shininess);

								specular = string.Join(" ", tmp, 0, 4);
							}

							var specularColor = SDF2Unity.GetColor(specular);
							material.SetColor("_SpecColor", specularColor);

							material.SetFloat("_SpecularHighlights", 1f);
							material.EnableKeyword("_SPECULAR_SETUP");
						}

						foreach (var textureunit in pass.textureUnits)
						{
							// UE.Debug.Log($"    TextureUnit: {pass.textureUnits.IndexOf(textureunit)}");
							// foreach (var kvp in textureunit.properties)
							// {
							// 	UE.Debug.Log($"      TextureUnit: {kvp.Key} -> {kvp.Value}");
							// }
							var textureUnitProps = textureunit.properties;

							if (textureUnitProps.ContainsKey("texture"))
							{
								var textureFileName = textureUnitProps["texture"];
								var textureFilePath = FindFile(uris, textureFileName);

								// UE.Debug.Log(textureFileName);
								// UE.Debug.Log(textureFilePath);
								if (!string.IsNullOrEmpty(textureFilePath))
								{
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
											var tileScale = SDF2Unity.GetScale(scaleSet);

											// TODO: Check texture tile scaling
											tileScale.x = 1 / tileScale.x;
											tileScale.y = 1 / tileScale.y;

											material.SetTextureScale("_BaseMap", tileScale);
										}

										material.SetTexture("_BaseMap", texture);
									}
								}
							}
							break;
						}
					}
				}
			}

			public static void ApplyMaterial(in SDF.Material.Script script, UE.Material targetMaterial)
			{
				var targetMaterialName = script.name;
				var texturesPath = new List<string>();
				var targetMaterialFilepath = string.Empty;

				foreach (var uri in script.uri)
				{
					// Debug.Log(uri);
					if (uri.EndsWith(".material") && File.Exists(uri))
					{
						targetMaterialFilepath = uri;
						var targetDir = Directory.GetParent(uri).Parent.ToString();
						texturesPath.Add(targetDir);
					}
					// find *.material file in folder
					else if (Directory.Exists(uri))
					{
						var files = Directory.GetFiles(uri);
						foreach (var file in files)
						{
							if (file.EndsWith(".material"))
							{
								targetMaterialFilepath = file;
								// Console.Write(file);
							}
						}

						texturesPath.Add(uri);
					}
				}

				if (string.IsNullOrEmpty(targetMaterialFilepath) == false)
				{
					var ogreMaterial = OgreMaterial.Parse(targetMaterialFilepath, targetMaterialName);
					if (ogreMaterial != null)
					{
						// UE.Debug.Log($"Found: '{ogreMaterial.name}' material, techniques: {ogreMaterial.techniques.Count}");
						ApplyOgreMaterial(ogreMaterial, targetMaterial, texturesPath);
					}
				}
			}
		}
	}
}