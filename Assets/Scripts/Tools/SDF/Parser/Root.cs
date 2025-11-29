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

namespace SDF
{
	public class Root
	{
		private readonly string[] SdfVersions = {
					"1.9", "1.8", "1.7", "1.6", "1.5", "1.4",
					"1.3", "1.2", "1.1", "1.0", string.Empty };
		private static readonly string ProtocolModel = "model://";
		private static readonly string ProtocolFile = "file://";

		// {Model Name, (Model Config Name, Model Path, Model File)}
		public Dictionary<string, Tuple<string, string, string>> resourceModelTable = new Dictionary<string, Tuple<string, string, string>>();

		private XmlDocument _doc = new XmlDocument();
		private XmlDocument _originalDoc = null; // for Save
		private string _worldFileName = string.Empty;

		private string _sdfVersion = "1.7";

		private DebugLogWriter _logger;
		private DebugLogWriter _loggerErr;

		public XmlDocument GetOriginalDocument() => _originalDoc;

		public List<string> fileDefaultPaths = new List<string>();

		public List<string> modelDefaultPaths = new List<string>();

		public List<string> worldDefaultPaths = new List<string>();


		public Root()
		{
			_logger = new DebugLogWriter();
			_loggerErr = new DebugLogWriter(true);
			Console.SetOut(_logger);
			Console.SetError(_loggerErr);
		}

		public bool DoParse(out World world, out string worldFilePath, in string worldFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			world = null;
			worldFilePath = string.Empty;
			if (worldFileName.Trim().Length <= 0)
			{
				return false;
			}

			// Console.Write("World file, PATH: " + worldFileName);
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

						ReplaceAllIncludedModel();

						ConvertPathToAbsolutePaths();

						// Console.Write("Load World");
						var worldNode = _doc.SelectSingleNode("/sdf/world");
						world = new World(worldNode);
						worldFilePath = worldPath;

						_logger.Write($"World({worldFileName}) is loaded.");
						// _logger.SetShowOnDisplayOnce();
						return true;
					}
					catch (XmlException ex)
					{
						_loggerErr.SetShowOnDisplayOnce();
						_loggerErr.Write($"Failed to Load World({fullFilePath}) - {ex.Message}");
					}
				}
			}

			_loggerErr.Write("World file not exist: " + worldFileName);
			return false;
		}

		public bool DoParse(out Model model, in string modelFullPath, in string modelFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			model = null;

			var modelLocation = Path.Combine(modelFullPath, modelFileName);
			var modelName = Path.GetFileName(modelFullPath);
			// Console.Write(modelFullPath + " -> " + modelName);
			try
			{
				_doc.RemoveAll();
				_doc.Load(modelLocation);

				ReplaceAllIncludedModel();

				// Console.Write("Load World");
				var modelNode = _doc.SelectSingleNode("/sdf/model");

				StoreOriginalModelName(_doc, modelName, modelNode);

				ConvertPathToAbsolutePaths();

				model = new Model(modelNode);

				_logger.SetShowOnDisplayOnce();
				_logger.Write($"Model({modelName}) is loaded. > {model.Name}");

				return true;
			}
			catch (XmlException ex)
			{
				var errorMessage = "Failed to Load Model file(" + modelLocation + ") file - " + ex.Message;
				_loggerErr.SetShowOnDisplayOnce();
				_loggerErr.Write(errorMessage);
			}

			return false;
		}

		public void UpdateResourceModelTable()
		{
			if (resourceModelTable == null)
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
				//Console.Write(">>> Model Default Path: " + modelPath);

				// Loop models
				foreach (var subDirectory in rootDirectory.GetDirectories())
				{
					if (subDirectory.Name.StartsWith("."))
					{
						continue;
					}

					// Console.Write(subDirectory.Name + " => " + subDirectory.FullName);
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
						// Console.Write(version);
						// Console.Write(modelNode);
						var sdfNode = modelNode.SelectSingleNode($"sdf[@version={version} or not(@version)]");
						if (sdfNode != null)
						{
							sdfFileName = sdfNode.InnerText;
							_sdfVersion = version;
							//Console.Write(version + "," + sdfFileName);
							break;
						}
					}

					if (string.IsNullOrEmpty(sdfFileName))
					{
						sdfModelErrlogs.AppendLine($"{modelName} - empty SDF FileName!!!");
						continue;
					}

					// Insert resource table
					var modelValue = new Tuple<string, string, string>(modelConfigName, subDirectory.FullName, sdfFileName);
					try
					{
						// Console.Write(modelName + ":" + subDirectory.FullName + ":" + sdfFileName);
						// Console.Write(modelName + ", " + modelValue);
						if (resourceModelTable.ContainsKey(modelName))
						{
							failedModelTableList.AppendLine(string.Empty);
							failedModelTableList.Append(String.Concat(modelName, " => ", modelValue));
						}
						else
						{
							resourceModelTable.Add(modelName, modelValue);
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
				_loggerErr.Write(directoryErrlogs.ToString());
			}

			if (fileErrlogs.Length > 0)
			{
				fileErrlogs.Insert(0, "File does not exists:\n");
				_loggerErr.Write(fileErrlogs.ToString());
			}

			if (sdfModelErrlogs.Length > 0)
			{
				sdfModelErrlogs.Insert(0, "Failed to load model files:");
				_loggerErr.Write(sdfModelErrlogs.ToString());
			}

			if (failedModelTableList.Length > 0)
			{
				failedModelTableList.Insert(0, $"Below models are already registered. - expected duplication of registeration");
				_loggerErr.Write(failedModelTableList);
			}

			Console.Write($"Loaded total Models: {resourceModelTable.Count}");
		}

		private string FindParentModelFolderName(in XmlNode targetNode)
		{
			var modelName = string.Empty;
			var node = targetNode?.ParentNode;
			while (node != null)
			{
				// Console.Write(node.Name + " - " + node.LocalName);
				if (node.Name == "model")
				{
					modelName = node.Attributes["original_name"]?.Value;
					// Console.Write("Found model " + modelName);
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
			// Console.Write("Target:" + targetElement + ", Num Of uri nodes: " + nodeList.Count);
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

					if (resourceModelTable.TryGetValue(modelName, out var value))
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

					if (resourceModelTable.TryGetValue(currentModelName, out var value))
					{
						node.InnerText = value.Item2 + "/" + meshUri;
					}
					// Console.Write($"Cannot convert: {uri}");
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
			ConvertPathToAbsolutePath("texture/diffuse");
			ConvertPathToAbsolutePath("texture/normal");
			ConvertPathToAbsolutePath("normal_map");
		}

		private void ReplaceAllIncludedModel()
		{
			// loop all include tag until all replaced.
			XmlNodeList nodes;
			do
			{
				nodes = _doc.SelectNodes("//include");

				// if (nodes.Count > 0)
				// 	Console.Write("Num Of Included Model nodes: " + nodes.Count);

				foreach (XmlNode node in nodes)
				{
					var modelNode = GetIncludedModel(node);

					if (modelNode != null)
					{
						// Console.Write("Node - " + modelNode);
						var importNode = _doc.ImportNode(modelNode, true);

						var newAttr = _doc.CreateAttribute("is_nested");
						newAttr.Value = "true";
						importNode.Attributes.Append(newAttr);

						node.ParentNode.ReplaceChild(importNode, node);
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
			// Console.Write(targetNode.Name + " - " + targetNode.LocalName + " - " + modelName);
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

			var pluginNode = includedNode.SelectSingleNode("plugin");
			// var plugin = (pluginNode == null) ? null : pluginNode.InnerText;

			var uri = uriNode.InnerText;
			var modelName = uri.Replace(ProtocolModel, string.Empty);

			if (resourceModelTable.TryGetValue(modelName, out var value))
			{
				uri = value.Item2 + "/" + value.Item3;
				// Console.WriteLine($"include/modelname = {name} | {uri} | {modelName} | {pose} | {isStatic}");
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
				_loggerErr.SetShowOnDisplayOnce();
				_loggerErr.Write($"Failed to Load included model({modelName}) file - {e.Message}");
				return null;
			}

			var sdfNode = modelSdfDoc.SelectSingleNode("/sdf/model") ?? modelSdfDoc.SelectSingleNode("/sdf/light");
			if (sdfNode == null)
			{
				Console.Write($"<model> or <light> element not exist");
				return null;
			}

			var attributes = sdfNode.Attributes;
			if (attributes.GetNamedItem("version") != null)
			{
				var modelSdfDocVersion = attributes.GetNamedItem("version").Value;
				// TODO: Version check
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

			if (pluginNode != null)
			{
				sdfNode.InsertBefore(modelSdfDoc.ImportNode(pluginNode, true), sdfNode.LastChild);
			}

			return sdfNode;
		}

		public void Save(in string filePath = "")
		{
			var fileName = Path.GetFileNameWithoutExtension(_worldFileName);
			var datetime = DateTime.Now.ToString("yyMMddHHmmss"); // DateTime.Now.ToString("yyyyMMddHHmmss");

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