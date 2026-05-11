/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Linq;
using System.Text;
using System;

namespace SDFormat
{
	public sealed class ResourceModelTable
		: Dictionary<string, (string configName, string path, string filename)>
	{
	}

	public class RootLoader
	{
		private readonly string[] SdfVersions = {
					"1.12", "1.11", "1.10", "1.9", "1.8", "1.7", "1.6", "1.5", "1.4",
					"1.3", "1.2", "1.1", "1.0", string.Empty };
		private static readonly string ProtocolModel = "model://";
		private static readonly string ProtocolFile = "file://";

		// {Model Name, (Model Config Name, Model Path, Model File)}
		private ResourceModelTable _resourceModelTable = new();

		private XmlDocument _doc = new();
		private XmlDocument _originalDoc = null; // for Save
		private string _worldFileName = string.Empty;

		private string _sdfVersion = "1.7";

		public XmlDocument GetOriginalDocument() => _originalDoc;

		public List<string> fileDefaultPaths = new();

		public List<string> modelDefaultPaths = new();

		public List<string> worldDefaultPaths = new();

		public ResourceModelTable ResourceModelTable { get => _resourceModelTable; }

		public bool DoParse(out World world, out string worldFilePath, in string worldFileName)
		{
			world = null;
			worldFilePath = string.Empty;
			if (worldFileName.Trim().Length <= 0)
			{
				return false;
			}

			foreach (var worldPath in worldDefaultPaths)
			{
				var fullFilePath = worldPath + "/" + worldFileName;
				if (File.Exists(@fullFilePath))
				{
					try
					{
						_doc.RemoveAll();
						_doc.Load(fullFilePath);
						_originalDoc = (XmlDocument)_doc.CloneNode(true);
						_worldFileName = worldFileName;

						_sdfVersion = _doc.SelectSingleNode("/sdf")
							?.Attributes?["version"]?.Value ?? _sdfVersion;

						ReplaceAllIncludedModel();

						ConvertPathToAbsolutePaths();

						// Parse with SdFormat
						var sdfElement = ParseWithSdFormat(_doc);
						if (sdfElement == null)
						{
							Console.Error.Write($"Failed to parse World with SdFormat({fullFilePath})");
							return false;
						}

						var worldElement = sdfElement.FindElement("world");
						world = new World();
						var errors = world.Load(worldElement);
						foreach (var error in errors)
						{
							if (error.HasError)
							{
								Console.Error.Write($"SdFormat World load error: {error.Message}");
							}
						}
						worldFilePath = worldPath;

						return true;
					}
					catch (XmlException ex)
					{
						Console.Error.Write($"Failed to Load World({fullFilePath}) - {ex.Message}");
						return false;
					}
				}
			}

			Console.Error.Write("World file not exist: " + worldFileName);
			return false;
		}

		public bool DoParse(out Model model, in string modelFullPath, in string modelFileName)
		{
			model = null;

			var modelLocation = Path.Combine(modelFullPath, modelFileName);
			var modelName = Path.GetFileName(modelFullPath);
			try
			{
				_doc.RemoveAll();
				_doc.Load(modelLocation);

				ReplaceAllIncludedModel();

				var modelNode = _doc.SelectSingleNode("/sdf/model");

				StoreOriginalModelName(_doc, modelName, modelNode);

				ConvertPathToAbsolutePaths();

				// Parse with SdFormat
				var sdfElement = ParseWithSdFormat(_doc);
				if (sdfElement == null)
				{
					Console.Error.Write("Failed to parse Model with SdFormat(" + modelLocation + ")");
					return false;
				}

				var modelElement = sdfElement.FindElement("model");
				model = new Model();
				var errors = model.Load(modelElement);
				foreach (var error in errors)
				{
					if (error.HasError)
					{
						Console.Error.Write($"SdFormat Model load error: {error.Message}");
					}
				}

				return true;
			}
			catch (XmlException ex)
			{
				var errorMessage = "Failed to Load Model file(" + modelLocation + ") file - " + ex.Message;
				Console.Error.Write(errorMessage);
			}

			return false;
		}

		/// <summary>
		/// Serialize XmlDocument to string and parse with SDFormat.SdfParser.
		/// Returns the root SDFormat.Element (the &lt;sdf&gt; element).
		/// </summary>
		private Element ParseWithSdFormat(XmlDocument doc)
		{
			var sw = new StringWriter();
			var xw = new XmlTextWriter(sw);
			doc.WriteTo(xw);
			var xmlString = sw.ToString();

			var parser = new SdfParser();
			var (rootElement, errors) = parser.Parse(xmlString);

			foreach (var error in errors)
			{
				if (error.HasError)
				{
					Console.Error.Write($"SdFormat parse error: {error.Message}");
				}
			}

			return rootElement;
		}

		private string SortModelConfigName(XmlDocument xmldoc, DirectoryInfo directoryInfo)
		{
			var modelConfig = directoryInfo.FullName + "/model.config";

			if (!File.Exists(modelConfig))
			{
				return string.Empty;
			}

			try
			{
				xmldoc.Load(modelConfig);
			}
			catch (XmlException)
			{
				return string.Empty;
			}

			var modelNode = xmldoc.SelectSingleNode("model");
			var modelNameNode = modelNode.SelectSingleNode("name");

			return (modelNameNode == null) ? string.Empty : modelNameNode.InnerText;
		}

		public void UpdateResourceModelTable()
		{
			if (_resourceModelTable == null)
			{
				Console.Write("ERROR: Resource model table is not initialized!!!!");
				return;
			}

			var directoryErrlogs = new StringBuilder();
			var fileErrlogs = new StringBuilder();
			var sdfModelErrlogs = new StringBuilder();
			var failedModelTableList = new StringBuilder();
			var modelConfigDoc = new XmlDocument();

			// Loop model paths
			foreach (var modelPath in modelDefaultPaths)
			{
				if (!Directory.Exists(modelPath))
				{
					directoryErrlogs.AppendLine(modelPath);
					continue;
				}

				var rootDirectory = new DirectoryInfo(modelPath);

				// Loop models
				foreach (var subDirectory in rootDirectory.GetDirectories().OrderBy(d => SortModelConfigName(modelConfigDoc, d)))
				{
					if (subDirectory.Name.StartsWith("."))
					{
						continue;
					}

					var modelConfig = subDirectory.FullName + "/model.config";

					if (!File.Exists(modelConfig))
					{
						fileErrlogs.AppendLine(modelConfig);
						continue;
					}

					try
					{
						modelConfigDoc.Load(modelConfig);
					}
					catch (XmlException e)
					{
						sdfModelErrlogs.AppendLine($"{modelConfig} - {e.Message}");
						continue;
					}

					// Get Model root
					var modelNode = modelConfigDoc.SelectSingleNode("model");

					// Get Model name
					var modelName = subDirectory.Name;

					var modelNameNode = modelNode.SelectSingleNode("name");
					var modelConfigName = (modelNameNode == null) ? modelName : modelNameNode.InnerText;

					// Get Model SDF file name
					var sdfFileName = string.Empty;
					foreach (var version in SdfVersions)
					{
						var xpath = string.IsNullOrEmpty(version)
							? "sdf[not(@version)]"
							: $"sdf[@version='{version}' or not(@version)]";
						var sdfNode = modelNode.SelectSingleNode(xpath);
						if (sdfNode != null)
						{
							sdfFileName = sdfNode.InnerText;
							break;
						}
					}

					if (string.IsNullOrEmpty(sdfFileName))
					{
						sdfModelErrlogs.AppendLine($"{modelName} - empty SDF FileName!!!");
						continue;
					}

					// Insert resource table
					var modelValue = (configName: modelConfigName, path: subDirectory.FullName, filename: sdfFileName);
					try
					{
						if (_resourceModelTable.ContainsKey(modelName))
						{
							failedModelTableList.AppendLine(string.Empty);
							failedModelTableList.Append(string.Concat(modelName, " => ", modelValue));
						}
						else
						{
							_resourceModelTable.Add(modelName, modelValue);
						}
					}
					catch (NullReferenceException e)
					{
						Console.Write(e.Message);
					}
				}
			}

			if (directoryErrlogs.Length > 0)
			{
				directoryErrlogs.Insert(0, "Directory does not exists: \n");
				Console.Error.Write(directoryErrlogs.ToString());
			}

			if (fileErrlogs.Length > 0)
			{
				fileErrlogs.Insert(0, "File does not exists:\n");
				Console.Error.Write(fileErrlogs.ToString());
			}

			if (sdfModelErrlogs.Length > 0)
			{
				sdfModelErrlogs.Insert(0, "Failed to load model files:");
				Console.Error.Write(sdfModelErrlogs.ToString());
			}

			if (failedModelTableList.Length > 0)
			{
				failedModelTableList.Insert(0, $"Below models are already registered. - expected duplication of registeration");
				Console.Error.Write(failedModelTableList);
			}

			Console.Write($"Loaded total Models: {_resourceModelTable.Count}");
		}

		private string FindParentModelFolderName(in XmlNode targetNode)
		{
			var modelName = string.Empty;
			var node = targetNode?.ParentNode;
			while (node != null)
			{
				if (node.Name == "model")
				{
					modelName = node.Attributes["original_name"]?.Value;
					break;
				}
				node = node?.ParentNode;
			}

			if (modelName == null)
			{
				return string.Empty;
			}

			return modelName;
		}

		// Converting media/file uri
		private void ConvertPathToAbsolutePath(in string targetElement)
		{
			var nodeList = _doc.SelectNodes($"//{targetElement}");
			foreach (XmlNode node in nodeList)
			{
				var uri = node.InnerText;
				if (uri.StartsWith(ProtocolModel))
				{
					var modelUri = uri.Replace(ProtocolModel, string.Empty);
					var stringArray = modelUri.Split('/');

					// Get Model name from Uri
					var modelName = stringArray[0];

					// remove Model name in array
					modelUri = string.Join("/", stringArray.Skip(1));

					if (_resourceModelTable.TryGetValue(modelName, out var value))
					{
						node.InnerText = value.Item2 + "/" + modelUri;
					}
				}
				else if (uri.StartsWith(ProtocolFile))
				{
					foreach (var filePath in fileDefaultPaths)
					{
						var fileUri = uri.Replace(ProtocolFile, filePath + "/");
						if (File.Exists(@fileUri))
						{
							node.InnerText = fileUri;
							break;
						}
					}
				}
				else
				{
					var currentModelName = FindParentModelFolderName(node);

					var meshUri = string.Join("/", uri);

					if (_resourceModelTable.TryGetValue(currentModelName, out var value))
					{
						node.InnerText = value.Item2 + "/" + meshUri;
					}
				}
			}
		}

		private void DuplicateNode(in string targetElement, in string newName)
		{
			var nodeList = _doc.SelectNodes($"//{targetElement}");
			foreach (XmlNode node in nodeList)
			{
				var newNode = _doc.CreateElement(newName);
				foreach (XmlNode childNode in node.ChildNodes)
				{
					newNode.AppendChild(childNode.CloneNode(true));
				}
				node.ParentNode.AppendChild(newNode);
			}
		}

		private void ConvertPathToAbsolutePaths()
		{
			DuplicateNode("uri", "original_uri");
			ConvertPathToAbsolutePath("uri");
			ConvertPathToAbsolutePath("filename");
			ConvertPathToAbsolutePath("cubemap_uri");
			ConvertPathToAbsolutePath("texture/diffuse");
			ConvertPathToAbsolutePath("texture/normal");
			ConvertPathToAbsolutePath("normal_map");
		}

		private static bool IsMergeInclude(XmlNode includedNode)
		{
			// SDF 1.12 spec: merge is an XML attribute on <include merge="true">
			var mergeAttr = includedNode.Attributes?["merge"]?.Value?.Trim().ToLowerInvariant();
			if (mergeAttr == "true" || mergeAttr == "1")
				return true;

			// Fallback: some tools write it as a child element <merge>true</merge> or <merge/>
			var mergeElem = includedNode.SelectSingleNode("merge");
			if (mergeElem != null)
			{
				var val = mergeElem.InnerText.Trim().ToLowerInvariant();
				// empty <merge/> element is treated as true (flag presence)
				return val == "true" || val == "1" || val == string.Empty;
			}

			return false;
		}

		private void ReplaceAllIncludedModel()
		{
			// loop all include tag until all replaced.
			XmlNodeList nodes;
			do
			{
				nodes = _doc.SelectNodes("//include");

				foreach (XmlNode node in nodes)
				{
					var modelNode = GetIncludedModel(node);

					if (modelNode != null)
					{
						if (IsMergeInclude(node))
						{
							// Merge: flatten model children into parent scope instead of adding a model wrapper.
							// When parent is <world>, <link> elements are skipped (links must live inside a model).
							var parentNode = node.ParentNode;
							var isWorldScope = parentNode?.Name == "world";
							var children = Enumerable.Cast<XmlNode>(modelNode.ChildNodes).ToList();
							foreach (var child in children)
							{
								// In world scope, links and model-level plugins cannot be promoted
								// to world level — links have no meaning outside a model, and robot
								// plugins (MicomPlugin, JointControlPlugin, …) rely on a model
								// GameObject as their parent via GetComponentsInChildren<>.
								if (isWorldScope && (child.Name == "link" || child.Name == "plugin"))
									continue;
								parentNode.InsertBefore(_doc.ImportNode(child, true), node);
							}
							parentNode.RemoveChild(node);
						}
						else
						{
							var importNode = _doc.ImportNode(modelNode, true);

							var newAttr = _doc.CreateAttribute("is_nested");
							newAttr.Value = "true";
							importNode.Attributes.Append(newAttr);

							node.ParentNode.ReplaceChild(importNode, node);
						}
					}
					else
					{
						node.ParentNode.RemoveChild(node);
					}
				}
			} while (nodes.Count != 0);
		}

		#region Segmentation Tag
		private void StoreOriginalModelName(in XmlDocument targetDoc, in string modelName, XmlNode targetNode)
		{
			// store original model's name for segmentation Tag
			var newAttr = targetDoc.CreateAttribute("original_name");
			newAttr.Value = modelName;
			targetNode.Attributes.Append(newAttr);
		}
		#endregion

		private XmlNode GetIncludedModel(XmlNode includedNode)
		{
			var uriNode = includedNode.SelectSingleNode("uri");
			if (uriNode == null)
			{
				Console.Write("uri is empty.");
				return null;
			}

			var nameNode = includedNode.SelectSingleNode("name");
			var name = nameNode?.InnerText;

			var staticNode = includedNode.SelectSingleNode("static");
			var isStatic = staticNode?.InnerText;

			var placementFrameNode = includedNode.SelectSingleNode("placement_frame");
			var placementFrame = placementFrameNode?.InnerText;

			var poseNode = includedNode.SelectSingleNode("pose") as XmlElement;
			var pose = poseNode?.InnerText;
			var poseAttributes = poseNode?.Attributes;

			var pluginNodes = includedNode.SelectNodes("plugin");

			var uri = uriNode.InnerText;
			var modelName = uri.Replace(ProtocolModel, string.Empty);

			if (_resourceModelTable.TryGetValue(modelName, out var value))
			{
				uri = value.Item2 + "/" + value.Item3;
			}
			else
			{
				Console.Write("Not exists in database: " + uri);
				return null;
			}

			var modelSdfDoc = new XmlDocument();
			try
			{
				modelSdfDoc.Load(uri);
			}
			catch (XmlException e)
			{
				Console.Error.Write($"Failed to Load included model({modelName}) file - {e.Message}");
				return null;
			}

			var sdfNode = modelSdfDoc.SelectSingleNode("/sdf/model") ?? modelSdfDoc.SelectSingleNode("/sdf/light");
			if (sdfNode == null)
			{
				Console.Write($"<model> or <light> element not exist");
				return null;
			}

			var attributes = sdfNode.Attributes;
			var sdfDocVersion = modelSdfDoc.SelectSingleNode("/sdf")
				?.Attributes?.GetNamedItem("version")?.Value ?? string.Empty;
			if (!string.IsNullOrEmpty(sdfDocVersion) && !string.IsNullOrEmpty(_sdfVersion))
			{
				if (Version.TryParse(sdfDocVersion, out var includedVer) &&
					Version.TryParse(_sdfVersion, out var worldVer))
				{
					if (includedVer.Major != worldVer.Major)
					{
						Console.Error.Write(
							$"[Include] SDF major version mismatch: '{modelName}' uses v{sdfDocVersion}, " +
							$"world uses v{_sdfVersion}. Parsing may fail.");
					}
					else if (includedVer.Minor != worldVer.Minor)
					{
						Console.Error.Write(
							$"[Include] SDF minor version mismatch: '{modelName}' uses v{sdfDocVersion}, " +
							$"world uses v{_sdfVersion}.");
					}
				}
			}

			StoreOriginalModelName(modelSdfDoc, modelName, sdfNode);

			if (nameNode != null && attributes.GetNamedItem("name") != null)
			{
				attributes.GetNamedItem("name").Value = name;
			}

			if (poseNode != null)
			{
				var poseElem = sdfNode.SelectSingleNode("pose") as XmlElement;
				if (poseElem != null)
				{
					poseElem.InnerText = pose;
					if (poseAttributes != null)
					{
						foreach (XmlAttribute attr in poseAttributes)
						{
							poseElem.SetAttribute(attr.Name, attr.Value);
						}
					}
				}
				else
				{
					var elem = modelSdfDoc.CreateElement("pose");
					elem.InnerText = pose;
					if (poseAttributes != null)
					{
						foreach (XmlAttribute attr in poseAttributes)
						{
							elem.SetAttribute(attr.Name, attr.Value);
						}
					}
					sdfNode.InsertBefore(elem, sdfNode.FirstChild);
				}
			}

			if (staticNode != null)
			{
				var staticElem = sdfNode.SelectSingleNode("static") as XmlElement;
				if (staticElem != null)
				{
					staticElem.InnerText = isStatic;
				}
				else
				{
					var elem = modelSdfDoc.CreateElement("static");
					elem.InnerText = isStatic;
					sdfNode.InsertBefore(elem, sdfNode.FirstChild);
				}
			}

			if (pluginNodes != null && pluginNodes.Count > 0)
			{
				foreach (XmlNode pluginNode in pluginNodes)
				{
					sdfNode.AppendChild(modelSdfDoc.ImportNode(pluginNode, true));
				}
			}

			return sdfNode;
		}

		public void Save(in string filePath = "")
		{
			var fileName = Path.GetFileNameWithoutExtension(_worldFileName);
			var datetime = DateTime.Now.ToString("yyMMddHHmmss");

			var saveName = $"{filePath}/{fileName}{datetime}.world";
			_originalDoc.Save(saveName);

			Console.Write($"Worldfile Saved: {saveName}");
		}

		public void Print()
		{
			// Print all SDF contents
			var sw = new StringWriter();
			var xw = new XmlTextWriter(sw);
			_doc.WriteTo(xw);
			Console.Write(sw.ToString());
		}
	}
}
