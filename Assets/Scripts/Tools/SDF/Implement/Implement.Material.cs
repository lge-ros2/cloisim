/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Text;
using System.IO;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Implement
	{
		public static partial class Material
		{
			public static void Apply(this SDFormat.Material sdfMaterial, UE.Renderer renderer, out StringBuilder logs)
			{
				logs = new StringBuilder();

				foreach (var material in renderer.materials)
				{
					material.SetBaseColor(sdfMaterial.Diffuse.ToUnity());

					material.SetEmission(sdfMaterial.Emissive.ToUnity());

					// Only apply specular when SDF provides non-trivial data.
					// Default black (0,0,0) is a Phong-era "no highlight" signal,
					// not a PBR F0 color — switching workflow for it kills diffuse.
					var specColor = sdfMaterial.Specular.ToUnity();
					if (specColor.maxColorComponent > 0.01f)
					{
						material.SetSpecular(specColor);
					}

					if (sdfMaterial.Shader != ShaderType.Pixel && !string.IsNullOrEmpty(sdfMaterial.NormalMap))
					{
						material.SetNormalMap(sdfMaterial.NormalMap);
					}

					// Apply PBR workflow if available
					if (sdfMaterial.PbrMaterial != null)
					{
						ApplyPbrWorkflow(material, sdfMaterial.PbrMaterial, logs);
						UE.Debug.Log($"Applied PBR workflow for material: {renderer.name}");
					}
				}

				// apply material script
				if (!string.IsNullOrEmpty(sdfMaterial.ScriptName))
				{
					var scriptUri = new List<string>();
					if (!string.IsNullOrEmpty(sdfMaterial.ScriptUri))
					{
						scriptUri.Add(sdfMaterial.ScriptUri);
					}

					FindMaterialFilepathAndUpdateURIs(scriptUri, out var targetMaterialFilepath, out var texturesPath);

					if (!string.IsNullOrEmpty(targetMaterialFilepath))
					{
						var ogreMaterial = OgreMaterial.Parse(targetMaterialFilepath, sdfMaterial.ScriptName);
						if (ogreMaterial != null)
						{
							var scriptAppliedMaterials = ogreMaterial.ApplyMaterial(renderer.materials, texturesPath);
							renderer.materials = scriptAppliedMaterials;

							if (sdfMaterial.ScriptName.ToLower().Contains("tree"))
							{
								foreach (var mat in renderer.materials)
								{
									mat.ConvertToSpeedTree();
								}
							}
						}
					}
				}
			}

			public static UE.Material ApplyScript(in string scriptUri, in string scriptName, in UE.Material baseMaterial)
			{
				var scriptUris = new List<string>();
				if (!string.IsNullOrEmpty(scriptUri))
				{
					scriptUris.Add(scriptUri);
				}
				FindMaterialFilepathAndUpdateURIs(scriptUris, out var targetMaterialFilepath, out var texturesPath);

				if (!string.IsNullOrEmpty(targetMaterialFilepath))
				{
					var ogreMaterial = OgreMaterial.Parse(targetMaterialFilepath, scriptName);
					if (ogreMaterial != null)
					{
						var materials = ogreMaterial.ApplyMaterial(new UE.Material[] { baseMaterial }, texturesPath);
						return materials[0];
					}
				}
				return baseMaterial;
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

			private static void ApplyPbrWorkflow(UE.Material material, Pbr pbr, StringBuilder logs)
			{
				var metalWorkflow = pbr.GetWorkflow(PbrWorkflowType.Metal);
				var specularWorkflow = pbr.GetWorkflow(PbrWorkflowType.Specular);

				var workflow = metalWorkflow ?? specularWorkflow;
				if (workflow == null)
				{
					return;
				}

				if (!string.IsNullOrEmpty(workflow.AlbedoMap))
				{
					var texture = MeshLoader.GetTexture(workflow.AlbedoMap);
					if (texture != null)
					{
						material.SetTexture("_BaseMap", texture);
					}
					else
					{
						logs.AppendLine($"PBR: Failed to load albedo map: {workflow.AlbedoMap}");
					}
				}

				if (!string.IsNullOrEmpty(workflow.NormalMap))
				{
					material.SetNormalMap(workflow.NormalMap);
				}

				if (!string.IsNullOrEmpty(workflow.EmissiveMap))
				{
					var texture = MeshLoader.GetTexture(workflow.EmissiveMap);
					if (texture != null)
					{
						material.SetTexture("_EmissionMap", texture);
						if (material.GetColor("_EmissionColor").maxColorComponent <= 0f)
						{
							material.SetColor("_EmissionColor", UE.Color.white);
						}
					}
				}

				if (!string.IsNullOrEmpty(workflow.AmbientOcclusionMap))
				{
					var texture = MeshLoader.GetTexture(workflow.AmbientOcclusionMap);
					if (texture != null)
					{
						material.SetTexture("_OcclusionMap", texture);
					}
				}

				if (metalWorkflow != null)
				{
					material.UseMetallicWorkflow();
					material.SetFloat("_SmoothnessTextureChannel", 0f);
					material.SetFloat("_Metallic", (float)metalWorkflow.Metalness);
					material.SetFloat("_Smoothness", 1f - (float)metalWorkflow.Roughness);

					if (!string.IsNullOrEmpty(metalWorkflow.MetalnessMap))
					{
						var texture = MeshLoader.GetTexture(metalWorkflow.MetalnessMap);
						if (texture != null)
						{
							material.SetTexture("_MetallicGlossMap", texture);
						}
					}
				}
				else if (specularWorkflow != null)
				{
					material.UseSpecularWorkflow();
					material.SetFloat("_SmoothnessTextureChannel", 0f);
					material.SetFloat("_Smoothness", (float)specularWorkflow.Glossiness);

					if (!string.IsNullOrEmpty(specularWorkflow.SpecularMap))
					{
						var texture = MeshLoader.GetTexture(specularWorkflow.SpecularMap);
						if (texture != null)
						{
							material.SetTexture("_SpecGlossMap", texture);
						}
					}
				}

				material.RefreshLitKeywords();
			}
		}
	}
}