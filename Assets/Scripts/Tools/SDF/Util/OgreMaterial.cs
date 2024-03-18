/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
		public List<Technique> techniques = new List<Technique>();
	}

	public class Technique
	{
		public List<Pass> passes = new List<Pass>();
	}

	public class Pass
	{
		public Dictionary<string, string> properties = new Dictionary<string, string>();
		public List<TextureUnit> textureUnits = new List<TextureUnit>();
	}

	public class TextureUnit
	{
		public Dictionary<string, string> properties = new Dictionary<string, string>();
	}

	private enum PropertyLevel { NONE, MATERIAL, TECHNIQUE, PASS, TEXTUREUNIT };

	public static Material Parse(string filePath, string targetMaterial)
	{
		Material material = null;
		try
		{
			string[] lines = File.ReadAllLines(filePath);

			var propertyLevel = PropertyLevel.NONE;

			var skipForCommentOut = false;

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
						if (value == targetMaterial)
						{
							material = new Material(targetMaterial);
							propertyLevel = PropertyLevel.MATERIAL;
							// Debug.Log($"!! Found material: {targetMaterial}");

							if (parts.Length > 3 && parts[2] == ":")
							{
								var parentTargetMaterial = parts[3];
								Debug.Log($"!! Found parent material: {parentTargetMaterial}");
								var parentMaterial = Parse(filePath, parentTargetMaterial);
							}
						}
						else
						{
							// Debug.Log($"!! material: {value}");
						}
					}
					else if (key == "receive_shadows" && propertyLevel == PropertyLevel.MATERIAL)
					{
						// Debug.Log($"!! Found {key}: {value}");
						material.hasReceiveShadows = true;
						material.receiveShadows = (value == "on") ? true : false;
					}
					else if (key == "technique" && propertyLevel == PropertyLevel.MATERIAL)
					{
						material.techniques.Add(new Technique());
						propertyLevel = PropertyLevel.TECHNIQUE;
						// Debug.Log($"!! Found technique: {material.techniques.Count}");
					}
					else if (key == "pass" && propertyLevel == PropertyLevel.TECHNIQUE)
					{
						if (material.techniques.Count == 0)
						{
							// Debug.Log("!! Missing technique");
							break;
						}

						material.techniques.Last().passes.Add(new Pass());
						propertyLevel = PropertyLevel.PASS;
					}
					else if (key == "texture_unit" && propertyLevel == PropertyLevel.PASS)
					{
						if (material.techniques.Last().passes.Count == 0)
						{
							// Debug.Log("!! Missing pass");
							break;
						}

						material.techniques.Last().passes.Last().textureUnits.Add(new TextureUnit());
						propertyLevel = PropertyLevel.TEXTUREUNIT;
					}
					else if (key == "}")
					{
						if (propertyLevel == PropertyLevel.MATERIAL)
						{
							// Debug.Log($"!! End MATERIAL parsing: {targetMaterial} Abort");
							propertyLevel = PropertyLevel.NONE;
							break;
						}
						else if (propertyLevel == PropertyLevel.TECHNIQUE)
						{
							// Debug.Log($"!! End TECHNIQUE parsing:");
							propertyLevel = PropertyLevel.MATERIAL;
						}
						else if (propertyLevel == PropertyLevel.PASS)
						{
							// Debug.Log($"!! End PASS parsing: ");
							propertyLevel = PropertyLevel.TECHNIQUE;
						}
						else if (propertyLevel == PropertyLevel.TEXTUREUNIT)
						{
							// Debug.Log($"!! End TEXTUREUNIT parsing: ");
							propertyLevel = PropertyLevel.PASS;
						}
					}
					else
					{
						if (parts.Length >= 2 && propertyLevel != PropertyLevel.NONE)
						{
							// Debug.Log($"!! {key}: {value}");
							if (propertyLevel == PropertyLevel.PASS)
							{
								var values = string.Join(" ", parts, 1, parts.Length - 1);
								material.techniques.Last().passes.Last().properties[key] = values;
							}
							else if (propertyLevel == PropertyLevel.TEXTUREUNIT)
							{
								var values = string.Join(" ", parts, 1, parts.Length - 1);
								material.techniques.Last().passes.Last().textureUnits.Last().properties[key] = values;
								// Debug.Log($"!! {key}: {values}");
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