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

		public bool DoParse(out World world, in string worldFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			world = null;
			if (worldFileName.Trim().Length <= 0)
			{
				return false;
			}

			var worldFound = false;
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

						replaceAllIncludedModel();

						ConvertPathToAbsolutePaths();

						// Console.Write("Load World");
						var worldNode = _doc.SelectSingleNode("/sdf/world");
						world = new World(worldNode);
						worldFound = true;

						var infoMessage = "World(" + worldFileName + ") is loaded.";
						_logger.Write(infoMessage);
						// _logger.SetShowOnDisplayOnce();
						break;
					}
					catch (XmlException ex)
					{
						var errorMessage = "Failed to Load World(" + fullFilePath + ") - " + ex.Message;
						_loggerErr.SetShowOnDisplayOnce();
						_loggerErr.Write(errorMessage);
					}
				}
			}

			if (!worldFound)
			{
				_loggerErr.Write("World file not exist: " + worldFileName);
			}

			return worldFound;
		}

		public bool DoParse(out Model model, in string modelFullPath, in string modelFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			model = null;

			var modelLocation = Path.Combine(modelFullPath, modelFileName);
			var modelName = Path.GetFileName(modelFullPath);
			// Console.Write(modelFullPath, modelName);
			try
			{
				_doc.RemoveAll();
				_doc.Load(modelLocation);

				replaceAllIncludedModel();

				ConvertPathToAbsolutePaths();

				// Console.Write("Load World");
				var modelNode = _doc.SelectSingleNode("/sdf/model");

				StoreOriginalModelName(_doc, modelName, modelNode);

				model = new Model(modelNode);

				// _logger.SetShowOnDisplayOnce();
				// _logger.Write($"Model({modelName}) is loaded. > {model.Name}");

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

			var failedModelTableList = new StringBuilder();
			var numberOfFailedModelTable = 0;

			var modelConfigDoc = new XmlDocument();

			// Loop model paths
			foreach (var modelPath in modelDefaultPaths)
			{
				if (!Directory.Exists(modelPath))
				{
					Console.Write("Directory does not exists: " + modelPath);
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
						Console.Write("File does not exists: " + modelConfig);
						continue;
					}

					try
					{
						modelConfigDoc.Load(modelConfig);
					}
					catch (XmlException e)
					{
						Console.Write("Failed to Load model file(" + modelConfig + ") - " + e.Message);
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
						var sdfNode = modelNode.SelectSingleNode("sdf[@version=" + version + " or not(@version)]");
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
						Console.Write(modelName + ": SDF FileName is empty!!");
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
							failedModelTableList.Append(String.Concat(modelName, " => Cannot register", modelValue));
							numberOfFailedModelTable++;
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

			if (numberOfFailedModelTable > 0)
			{
				failedModelTableList.Insert(0, $"All failed models({numberOfFailedModelTable}) are already registered.");
				_loggerErr.Write(failedModelTableList);
			}

			Console.Write($"Total Models: {resourceModelTable.Count}");
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
					Console.Write($"Cannot convert: {uri}");
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
		}

		private void replaceAllIncludedModel()
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
		private void StoreOriginalModelName(XmlDocument targetDoc, in string modelName, XmlNode targetNode)
		{
 			// store original model's name for segmentation Tag
			var newAttr = targetDoc.CreateAttribute("original_name");
			newAttr.Value = modelName;
			targetNode.Attributes.Append(newAttr);
		}
		#endregion

		private XmlNode GetIncludedModel(XmlNode included_node)
		{
			var uri_node = included_node.SelectSingleNode("uri");
			if (uri_node == null)
			{
				Console.Write("uri is empty.");
				return null;
			}

			var nameNode = included_node.SelectSingleNode("name");
			var name = (nameNode == null) ? null : nameNode.InnerText;

			var staticNode = included_node.SelectSingleNode("static");
			var isStatic = (staticNode == null) ? null : staticNode.InnerText;

			var placementFrameNode = included_node.SelectSingleNode("placement_frame");
			var placementFrame = (placementFrameNode == null) ? null : placementFrameNode.InnerText;

			var poseNode = included_node.SelectSingleNode("pose");
			var pose = (poseNode == null) ? null : poseNode.InnerText;

			// var pluginNode = included_node.SelectSingleNode("plugin");
			// var plugin = (pluginNode == null) ? null : pluginNode.InnerText;

			var uri = uri_node.InnerText;
			var modelName = uri.Replace(ProtocolModel, string.Empty);

			if (resourceModelTable.TryGetValue(modelName, out var value))
			{
				uri = value.Item2 + "/" + value.Item3;
				// Console.WriteLine($"{name} | {uri} | {modelName} | {pose} | {isStatic}");
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
				var errorMessage = "Failed to Load included model(" + modelName + ") file - " + e.Message;
				_loggerErr.SetShowOnDisplayOnce();
				_loggerErr.Write(errorMessage);
				return null;
			}

			var sdfNode = modelSdfDoc.SelectSingleNode("/sdf/model");

			if (sdfNode == null)
			{
				sdfNode = modelSdfDoc.SelectSingleNode("/sdf/light");
			}

			var attributes = sdfNode.Attributes;
			if (attributes.GetNamedItem("version") != null)
			{
				var modelSdfDocVersion = attributes.GetNamedItem("version").Value;
				// TODO: Version check
			}

			StoreOriginalModelName(modelSdfDoc, modelName, sdfNode);

			// Edit custom parameter
			if (nameNode != null)
			{
				sdfNode.Attributes["name"].Value = name;
			}

			if (poseNode != null)
			{
				if (sdfNode.SelectSingleNode("pose") != null)
				{
					sdfNode.SelectSingleNode("pose").InnerText = pose;
				}

				else
				{
					var elem = sdfNode.OwnerDocument.CreateElement("pose");
					elem.InnerText = pose;
					sdfNode.InsertBefore(elem, sdfNode.FirstChild);
				}
			}

			if (staticNode != null)
			{
				if (sdfNode.SelectSingleNode("static") != null)
				{
					sdfNode.SelectSingleNode("static").InnerText = isStatic;
				}
				else
				{
					var elem = sdfNode.OwnerDocument.CreateElement("static");
					elem.InnerText = isStatic;
					sdfNode.InsertBefore(elem, sdfNode.FirstChild);
				}
			}

			return sdfNode;
		}

		public void Save()
		{
			var fileName = Path.GetFileNameWithoutExtension(_worldFileName);
			var datetime = DateTime.Now.ToString("yyMMddHHmmss"); // DateTime.Now.ToString("yyyyMMddHHmmss");

			var saveName = fileName + datetime + ".world";

			_originalDoc.Save(saveName);

			Console.Write(saveName);
		}

		public void Print()
		{
			// Print all SDF contents
			StringWriter sw = new StringWriter();
			XmlTextWriter xw = new XmlTextWriter(sw);
			_doc.WriteTo(xw);
			Console.Write(sw.ToString());
		}
	}
}