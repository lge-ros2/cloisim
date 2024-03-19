/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class OgreMaterial
{
	public class Material
	{
		public Material(string name)
		{
			this.name = name;
		}

		public string name;

		public bool hasReceiveShadows = false;
		public bool receiveShadows = false;

		public Dictionary<string, Technique> techniques = new Dictionary<string, Technique>();
	}

	public class Technique
	{
		public Dictionary<string, Pass> passes = new Dictionary<string, Pass>();
	}

	public class Pass
	{
		public Dictionary<string, string> properties = new Dictionary<string, string>();
		public Dictionary<string, TextureUnit> textureUnits = new Dictionary<string, TextureUnit>();
	}

	public class TextureUnit
	{
		public Dictionary<string, string> properties = new Dictionary<string, string>();
	}

	private enum PropertyLevel { NONE, MATERIAL, TECHNIQUE, PASS, TEXTUREUNIT };

	public static Material Parse(string filePath, string targetMaterialName)
	{
		Material material = null;
		try
		{
			var lines = File.ReadAllLines(filePath);

			var propertyLevel = PropertyLevel.NONE;

			var skipForCommentOut = false;

			var targetTechName = string.Empty;
			var targetPassName = string.Empty;
			var targetTextureUnitName = string.Empty;

			foreach (var line in lines)
			{
				if (string.IsNullOrWhiteSpace(line) || line.Trim().StartsWith("#") || line.Trim().StartsWith("//"))
					continue;

				if (line.Trim().StartsWith("/*"))
					skipForCommentOut = true;
				else if (line.Trim().StartsWith("*/") || line.Trim().EndsWith("*/"))
					skipForCommentOut = false;

				if (skipForCommentOut)
					continue;

				string[] parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

				if (parts.Length >= 1)
				{
					var key = parts[0].Trim();
					var value = (parts.Length > 1)? parts[1].Trim() : string.Empty;
					// Debug.Log(key + " => " + value);

					if (key == "material")
					{
						if (value == targetMaterialName)
						{
							// Debug.Log($"!! Found material: {targetMaterialName}");
							if (parts.Length > 3 && parts[2] == ":")
							{
								var parentMaterialName = parts[3];
								// Debug.Log($"!! Found parent material: {parentMaterialName}");
								material = Parse(filePath, parentMaterialName);
								material.name = targetMaterialName;
							}
							else
							{
								material = new Material(targetMaterialName);
							}

							propertyLevel = PropertyLevel.MATERIAL;
						}
						// else
						// {
						// 	Debug.Log($"!! material: {value}");
						// }
					}
					else if (key == "receive_shadows" && propertyLevel == PropertyLevel.MATERIAL)
					{
						// Debug.Log($"!! Found {key}: {value}");
						material.hasReceiveShadows = true;
						material.receiveShadows = (value == "on") ? true : false;
					}
					else if (key == "technique" && propertyLevel == PropertyLevel.MATERIAL)
					{
						var techName = (parts.Length > 1) ? parts[1] : string.Empty;

						material.techniques[techName] = new Technique();
						targetTechName = techName;

						propertyLevel = PropertyLevel.TECHNIQUE;
						// Debug.Log($"!! Found technique: {material.techniques.Count}");
					}
					else if (key == "pass" && propertyLevel == PropertyLevel.TECHNIQUE)
					{
						// Debug.Log(key + " => " + value);
						if (material.techniques.Count == 0)
						{
							// Debug.Log("!! Missing technique");
							break;
						}

						var passName = (parts.Length > 1) ? parts[1] : string.Empty;

						var targetTechnique = material.techniques[targetTechName];
						if (!targetTechnique.passes.ContainsKey(passName))
							targetTechnique.passes[passName] = new Pass();

						// Debug.Log(targetTechnique.passes.Count);
						targetPassName = passName;

						propertyLevel = PropertyLevel.PASS;
					}
					else if (key == "texture_unit" && propertyLevel == PropertyLevel.PASS)
					{
						if (material.techniques[targetTechName].passes.Count == 0)
						{
							// Debug.Log("!! Missing pass");
							break;
						}

						var textureUnitName = (parts.Length > 1) ? parts[1] : string.Empty;

						var targetPass = material.techniques[targetTechName].passes[targetPassName];

						if (!targetPass.textureUnits.ContainsKey(textureUnitName))
							targetPass.textureUnits[textureUnitName] = new TextureUnit();

						targetTextureUnitName = textureUnitName;
						propertyLevel = PropertyLevel.TEXTUREUNIT;
					}
					else if (key == "}")
					{
						if (propertyLevel == PropertyLevel.MATERIAL)
						{
							// Debug.Log($"!! End MATERIAL parsing: {targetMaterialName} Abort");
							propertyLevel = PropertyLevel.NONE;
							break;
						}
						else if (propertyLevel == PropertyLevel.TECHNIQUE)
						{
							// Debug.Log($"!! End TECHNIQUE parsing:");
							targetTechName = string.Empty;
							propertyLevel = PropertyLevel.MATERIAL;
						}
						else if (propertyLevel == PropertyLevel.PASS)
						{
							// Debug.Log($"!! End PASS parsing: ");
							targetPassName = string.Empty;
							propertyLevel = PropertyLevel.TECHNIQUE;
						}
						else if (propertyLevel == PropertyLevel.TEXTUREUNIT)
						{
							// Debug.Log($"!! End TEXTUREUNIT parsing: ");
							targetTextureUnitName = string.Empty;
							propertyLevel = PropertyLevel.PASS;
						}
					}
					else
					{
						if (parts.Length >= 2 && propertyLevel != PropertyLevel.NONE)
						{
							var values = string.Join(" ", parts, 1, parts.Length - 1);
							if (propertyLevel == PropertyLevel.PASS)
							{
								material.techniques[targetTechName]
										.passes[targetPassName]
										.properties[key] = values;
								// Debug.Log($"!! {key}: {values}");
							}
							else if (propertyLevel == PropertyLevel.TEXTUREUNIT)
							{
								material.techniques[targetTechName]
										.passes[targetPassName]
										.textureUnits[targetTextureUnitName]
										.properties[key] = values;
								// Debug.Log($"!! {key}: {values}");
							}
							else
							{
								Debug.Log($"Unsupported entries {key}: {values}");
							}
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			Debug.Log($"Error reading or parsing the file: {ex.Message}");
		}

		return material;
	}
}