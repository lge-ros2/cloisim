/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using System.IO;
using UE = UnityEngine;

namespace SDF
{
	namespace Implement
	{
		public static partial class Material
		{
			public static void Apply(this SDF.Material sdfMaterial, UE.Renderer renderer, out StringBuilder logs)
			{
				logs = new StringBuilder();

				foreach (var material in renderer.materials)
				{
					if (sdfMaterial.ambient != null)
					{
						logs.AppendLine($"{material.name}: ambient({sdfMaterial.ambient.ToUnity()}) is not support.");
					}

					if (sdfMaterial.diffuse != null)
					{
						material.SetBaseColor(sdfMaterial.diffuse.ToUnity());
					}

					if (sdfMaterial.emissive != null)
					{
						material.SetEmission(sdfMaterial.emissive.ToUnity());
					}

					if (sdfMaterial.specular != null)
					{
						material.SetSpecular(sdfMaterial.specular.ToUnity());
						// logs.AppendLine($"{material.name}: specular({material.GetColor("_SpecColor")})");
					}

					if (sdfMaterial.shader != null)
					{
						material.SetNormalMap(sdfMaterial.shader.normal_map);
						// logs.AppendLine($"{material.name}: normalmap({sdfMaterial.shader.normal_map})");
					}
				}

				// apply material script
				if (sdfMaterial.script != null)
				{
					// Name of material from an installed script file.
					// This will override the color element if the script exists.
					var scriptAppliedMaterials = sdfMaterial.script.ApplyScript(renderer.materials);
					renderer.materials = scriptAppliedMaterials;

					if (sdfMaterial.script.name.ToLower().Contains("tree"))
					{
						foreach (var material in renderer.materials)
						{
							material.ConvertToSpeedTree();
						}
					}
				}
			}

			public static UE.Material ApplyScript(this SDF.Material.Script script, in UE.Material baseMasterial)
			{
				var materials = script.ApplyScript(new UE.Material[] { baseMasterial });
				return materials[0];
			}

			public static UE.Material[] ApplyScript(this SDF.Material.Script script, in UE.Material[] baseMaterials)
			{
				var targetMaterialName = script.name;
				FindMaterialFilepathAndUpdateURIs(script.uri, out var targetMaterialFilepath, out var texturesPath);

				var outputMaterials = baseMaterials;

				if (string.IsNullOrEmpty(targetMaterialFilepath) == false)
				{
					var ogreMaterial = OgreMaterial.Parse(targetMaterialFilepath, targetMaterialName);
					if (ogreMaterial != null)
					{
						// UE.Debug.Log($"Found: '{ogreMaterial.name}' material, techniques: {ogreMaterial.techniques.Count}");
						outputMaterials = ogreMaterial.ApplyMaterial(baseMaterials, texturesPath);
					}
				}

				return outputMaterials;
			}

			private static void FindMaterialFilepathAndUpdateURIs(in List<string> scriptUris, out string targetMaterialFilepath, out List<string> texturesPath)
			{
				targetMaterialFilepath = string.Empty;
				texturesPath = new List<string>();

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
			}
		}
	}
}