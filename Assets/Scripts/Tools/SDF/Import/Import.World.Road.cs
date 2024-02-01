/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Splines = UnityEngine.Splines;
using System.Collections.Generic;
using System.IO;
using System;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private string FindFile(in string currentDir, in string targetFileName)
			{
				var ext = Path.GetExtension(targetFileName);

				var parentDir = Directory.GetParent(currentDir).Parent.ToString();
				// UE.Debug.Log(parentDir);

				var subdirectoryEntries = Directory.GetDirectories(parentDir);
				foreach (var subdirectory in subdirectoryEntries)
				{
					// UE.Debug.Log(subdirectory);
					var fileEntries = Directory.GetFiles(subdirectory, "*" + ext);

					foreach (var fileName in fileEntries)
					{
						// UE.Debug.Log(fileName);
						if (fileName.EndsWith(targetFileName))
							return fileName;
					}
				}

				return string.Empty;
			}

			private void ImportRoad(in World.Road road)
			{
				var newRoadObject = new UE.GameObject();
				newRoadObject.name = road.Name;
				newRoadObject.tag = "Road";
				newRoadObject.transform.SetParent(Main.RoadsRoot.transform);

				var splineContainer = newRoadObject.AddComponent<Splines.SplineContainer>();

				foreach (var point in road.points)
				{
					var knotPos = SDF2Unity.GetPosition(point);
					var knot = new Splines.BezierKnot();
					knot.Position = knotPos;
					splineContainer.Spline.Add(knot, Splines.TangentMode.Continuous);
				}
				splineContainer.Spline.SetTangentMode(0, Splines.TangentMode.AutoSmooth);


				var material = SDF2Unity.GetNewMaterial(road.Name + "_Material");

				var targetMaterialName = road.material.script.name;
				foreach (var uri in road.material.script.uri)
				{
					var ogreMaterial = OgreMaterial.Parse(uri, targetMaterialName);
					if (ogreMaterial != null)
					{
						if (ogreMaterial != null)
						{
							// UE.Debug.Log($"Found: {targetMaterialName} material");
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
										var specularColor = SDF2Unity.GetColor(specular);
										material.SetColor("_SpecColor", specularColor);
									}

									foreach (var textureunit in pass.textureUnits)
									{
										// UE.Debug.Log($"    TextureUnit: {pass.textureUnits.IndexOf(textureunit)}");

										// foreach (var kvp in textureunit.properties)
										// {
										// 	UE.Debug.Log($"      TextureUnit: {kvp.Key} -> {kvp.Value}");
										// }

										if (textureunit.properties.ContainsKey("texture"))
										{
											var textureFileName = textureunit.properties["texture"];
											var textureFilePath = FindFile(uri, textureFileName);
											// UE.Debug.Log(textureFilePath);
											if (!string.IsNullOrEmpty(textureFilePath))
											{
												var texture = MeshLoader.GetTexture(textureFilePath);
												if (texture != null)
												{
													var textureFiltering = textureunit.properties["filtering"];

													// to make upper in First character
													textureFiltering = textureFiltering.Remove(1).ToUpper() + textureFiltering.Substring(1);
													texture.filterMode = (UE.FilterMode)Enum.Parse(typeof(UE.FilterMode), textureFiltering);
													material.SetTexture("_BaseMap", texture);
												}
											}
										}
										break;
									}
								}
							}
						}
						break;
					}
				}

				var roadGenerator = newRoadObject.AddComponent<Unity.Splines.LoftRoadGenerator>();
				roadGenerator.Material = material;
				roadGenerator.LoftAllRoads();
				roadGenerator.Widths.Add(new Splines.SplineData<float>((float)road.width));
			}

			private void ImportRoads(IReadOnlyList<World.Road> items)
			{
				foreach (var item in items)
				{
					ImportRoad(item);
				}
			}
		}
	}
}