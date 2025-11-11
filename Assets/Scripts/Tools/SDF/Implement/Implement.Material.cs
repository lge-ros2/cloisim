/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using UE = UnityEngine;

namespace SDF
{
	namespace Implement
	{
		public partial class Material
		{
			public static void Apply(in SDF.Material sdfMaterial, UE.Renderer renderer)
			{
				foreach (var material in renderer.materials)
				{
					if (sdfMaterial.ambient != null)
					{
						UE.Debug.LogWarning(material.name + ": ambient is not support. " + SDF2Unity.Color(sdfMaterial.ambient));
					}

					if (sdfMaterial.diffuse != null)
					{
						SDF2Unity.Material.SetBaseColor(material, SDF2Unity.Color(sdfMaterial.diffuse));
					}

					if (sdfMaterial.emissive != null)
					{
						SDF2Unity.Material.SetEmission(material, SDF2Unity.Color(sdfMaterial.emissive));
					}

					if (sdfMaterial.specular != null)
					{
						SDF2Unity.Material.SetSpecular(material, SDF2Unity.Color(sdfMaterial.specular));
						// UE.Debug.Log("ImportMaterial HasColorSpecular " + material.GetColor("_SpecColor"));
					}

					if (sdfMaterial.shader != null)
					{
						SDF2Unity.Material.SetNormalMap(material, sdfMaterial.shader.normal_map);
						// UE.Debug.Log("ImportMaterial HasNormalmap " + sdfMaterial.shader.normal_map);
					}
				}

				// apply material script
				if (sdfMaterial.script != null)
				{
					// Name of material from an installed script file.
					// This will override the color element if the script exists.
					var scriptAppliedMaterials = ApplyScript(sdfMaterial.script, renderer.materials);
					renderer.materials = scriptAppliedMaterials;

					if (sdfMaterial.script.name.ToLower().Contains("tree"))
					{
						foreach (var material in renderer.materials)
						{
							SDF2Unity.Material.ConvertToSpeedTree(material);
						}
					}
				}
			}

			public static UE.Material ApplyScript(in SDF.Material.Script script, in UE.Material baseMasterial)
			{
				var materials = ApplyScript(script, new UE.Material[] { baseMasterial });
				return materials[0];
			}

			public static UE.Material[] ApplyScript(in SDF.Material.Script script, in UE.Material[] baseMaterials)
			{
				var targetMaterialName = script.name;
				var targetMaterialFilepath = FindMaterialFilepathAndUpdateURIs(script.uri, out var texturesPath);

				var outputMaterials = baseMaterials;

				if (string.IsNullOrEmpty(targetMaterialFilepath) == false)
				{
					var ogreMaterial = OgreMaterial.Parse(targetMaterialFilepath, targetMaterialName);
					if (ogreMaterial != null)
					{
						// UE.Debug.Log($"Found: '{ogreMaterial.name}' material, techniques: {ogreMaterial.techniques.Count}");
						outputMaterials = Ogre.ApplyMaterial(ogreMaterial, baseMaterials, texturesPath);
					}
				}

				return outputMaterials;
			}

			private static string FindMaterialFilepathAndUpdateURIs(in List<string> scriptUris, out List<string> texturesPath)
			{
				texturesPath = new List<string>();
				var targetMaterialFilepath = string.Empty;

				foreach (var uri in scriptUris)
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

				return targetMaterialFilepath;
			}
		}
	}
}