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
		// {Model Name, (Model Path, Model File)}
		public Dictionary<string, Tuple<string, string>> resourceModelTable = new Dictionary<string, Tuple<string, string>>();

		private readonly string[] sdfVersions = {"1.9", "1.8", "1.7", "1.6", "1.5", "1.4", "1.3", "1.2", string.Empty};

		private XmlDocument doc = new XmlDocument();

		private string sdfVersion = "1.7";

		public List<string> fileDefaultPaths = new List<string>();

		public List<string> modelDefaultPaths = new List<string>();

		public List<string> worldDefaultPaths = new List<string>();

		private DebugLogWriter logger;
		private DebugLogWriter errLogger;

		public Root()
		{
			logger = new DebugLogWriter();
			errLogger = new DebugLogWriter(true);
			Console.SetOut(logger);
			Console.SetError(errLogger);
		}

		public bool DoParse(out World world, in string worldFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			var worldFound = false;
			world = null;
			if (worldFileName.Trim().Length > 0)
			{
				// Console.Write("World file, PATH: " + worldFileName);
				foreach (var worldPath in worldDefaultPaths)
				{
					var fullFilePath = worldPath + "/" + worldFileName;
					if (File.Exists(@fullFilePath))
					{
						try
						{
							doc.RemoveAll();
							doc.Load(fullFilePath);

							replaceAllIncludedModel();

							ConvertPathToAbsolutePath("uri");
							ConvertPathToAbsolutePath("filename");

							// Console.Write("Load World");
							var worldNode = doc.SelectSingleNode("/sdf/world");
							world = new World(worldNode);
							worldFound = true;

							var infoMessage = "World(" + worldFileName + ") is loaded.";

							logger.Write(infoMessage);
							// logger.SetShowOnDisplayOnce();
						}
						catch (XmlException ex)
						{
							var errorMessage = "Failed to Load World(" + fullFilePath + ") - " + ex.Message;
							errLogger.SetShowOnDisplayOnce();
							errLogger.Write(errorMessage);
						}
						break;
					}
				}
			}

			if (!worldFound)
			{
				errLogger.Write("World file not exist: " + worldFileName);
			}

			return worldFound;
		}

		public bool DoParse(out Model model, in string modelFullPath, in string modelFileName)
		{
			// Console.Write("Loading World File from SDF!!!!!");
			var modelFound = false;
			model = null;
			var modelLocation = Path.Combine(modelFullPath, modelFileName);
			try
			{
				doc.RemoveAll();
				doc.Load(modelLocation);

				replaceAllIncludedModel();

				ConvertPathToAbsolutePath("uri");
				ConvertPathToAbsolutePath("filename");

				// Console.Write("Load World");
				var modelNode = doc.SelectSingleNode("/sdf/model");
				model = new Model(modelNode);
				modelFound = true;

				var infoMessage =  model.Name + " Model(" + modelFileName + ") is loaded.";
				// logger.SetShowOnDisplayOnce();
				logger.Write(infoMessage);
			}
			catch (XmlException ex)
			{
				var errorMessage = "Failed to Load Model file(" + modelLocation + ") file - " + ex.Message;
				errLogger.SetShowOnDisplayOnce();
				errLogger.Write(errorMessage);
			}

			return modelFound;
		}

#if false
		public void SaveDocument()
		{
			// Print all SDF contents
			StringWriter sw = new StringWriter();
			XmlTextWriter xw = new XmlTextWriter(sw);
			doc.WriteTo(xw);
			Console.Write(sw.ToString());
		}
#endif
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

					//Console.Write(subDirectory.Name + " => " + subDirectory.FullName);
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
						Console.Write("Failed to Load model file(" + modelConfig  + ") - " + e.Message);
						continue;
					}

					// Get Model name
					var modelName = subDirectory.Name;

					// Get Model root
					var modelNode = modelConfigDoc.SelectSingleNode("model");

					// Get Model SDF file name
					var sdfFileName = string.Empty;
					foreach (var version in sdfVersions)
					{
						//Console.Write(version);
						var sdfNode = modelNode.SelectSingleNode("sdf[@version=" + version + " or not(@version)]");
						if (sdfNode != null)
						{
							sdfFileName = sdfNode.InnerText;
							sdfVersion = version;
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
					var modelValue = new Tuple<string, string>(subDirectory.FullName, sdfFileName);
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
				failedModelTableList.Insert(0, "All failed models(" + numberOfFailedModelTable + ") are already registered.");
				errLogger.Write(failedModelTableList);
			}

			Console.Write("Total Models: " + resourceModelTable.Count);
		}

		// Converting media/file uri
		private void ConvertPathToAbsolutePath(in string target_element)
		{
			var nodeList = doc.SelectNodes("//" + target_element);
			// Console.Write("Num Of uri nodes: " + nodeList.Count);
			foreach (XmlNode node in nodeList)
			{
				var uri = node.InnerText;
				if (uri.StartsWith("model://"))
				{
					var modelUri = uri.Replace("model://", string.Empty);
					var stringArray = modelUri.Split('/');

					// Get Model name from Uri
					var modelName = stringArray[0];

					// remove Model name in array
					modelUri = string.Join("/", stringArray.Skip(1));

					Tuple<string, string> value;
					if (resourceModelTable.TryGetValue(modelName, out value))
					{
						node.InnerText = value.Item1 + "/" + modelUri;
					}
				}
				else if (uri.StartsWith("file://"))
				{
					foreach (var filePath in fileDefaultPaths)
					{
						var fileUri = uri.Replace("file://", filePath + "/");
						if (File.Exists(@fileUri))
						{
							node.InnerText = fileUri;
							break;
						}
					}
				}
				else
				{
					Console.Write("Cannot convert: " + uri);
				}
			}
		}

		private void replaceAllIncludedModel()
		{
			// loop all include tag until all replaced.
			XmlNodeList nodes;
			do
			{
				nodes = doc.SelectNodes("//include");

				// if (nodes.Count > 0)
				// 	Console.Write("Num Of Included Model nodes: " + nodes.Count);

				foreach (XmlNode node in nodes)
				{
					var modelNode = GetIncludedModel(node);

					if (modelNode != null)
					{
						// Console.Write("Node - " + modelNode);
						var importNode = doc.ImportNode(modelNode, true);
						node.ParentNode.ReplaceChild(importNode, node);
					}
					else
					{
						node.ParentNode.RemoveChild(node);
					}
				}
			} while (nodes.Count != 0);
		}

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
			// Console.WriteLineFormat("{0} | {1} | {2} | {3}", name, uri, pose, isStatic);

			Tuple<string, string> value;
			var modelName = uri.Replace("model://", string.Empty);
			if (resourceModelTable.TryGetValue(modelName, out value))
			{
				uri = value.Item1 + "/" + value.Item2;
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
				errLogger.SetShowOnDisplayOnce();
				errLogger.Write(errorMessage);
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
			}

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
					XmlElement elem = sdfNode.OwnerDocument.CreateElement("pose");
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
					XmlElement elem = sdfNode.OwnerDocument.CreateElement("static");
					elem.InnerText = isStatic;
					sdfNode.InsertBefore(elem, sdfNode.FirstChild);
				}
			}

			return sdfNode;
		}

		// private void Save()
		// {
		// }
	}
}