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
		private Dictionary<string, Tuple<string, string>> resourceModelTable = new Dictionary<string, Tuple<string, string>>();

		private string[] sdfVersions = {"1.7", "1.6", "1.5", "1.4", "1.3", "1.2", string.Empty};

		private XmlDocument doc = new XmlDocument();

		private string sdfVersion = "1.7";

		private World world = null;

		public string fileDefaultPath = String.Empty;

		public List<string> modelDefaultPaths = new List<string>();

		public List<string> worldDefaultPath = new List<string>();

		private string worldFileName = string.Empty;

		private SimulationDisplay simulationDisplay = null;

		public Root()
			: this("")
		{
		}

		public Root(string filename)
		{
			Console.SetOut(new DebugLogWriter());

			SetWorldFileName(filename);
		}

		public void SetTargetLogOutput(in SimulationDisplay target)
		{
			simulationDisplay = target;
		}

		public void SetWorldFileName(in string filename)
		{
			if (!string.IsNullOrEmpty(filename))
			{
				worldFileName = filename;
			}
		}

		public World World()
		{
			return world;
		}

		public bool DoParse()
		{
			// Console.WriteLine("Loading World File from SDF!!!!!");
			updateResourceModelTable();

			var worldFound = false;
			if (doc != null && worldFileName != null && worldFileName.Length > 0)
			{
				// Console.WriteLine("World file, PATH: " + worldFileName);
				foreach (var worldPath in worldDefaultPath)
				{
					var fullFilePath = worldPath + "/" + worldFileName;
					if (File.Exists(@fullFilePath))
					{
						doc.Load(fullFilePath);
						worldFound = true;
						break;
					}
				}
			}

			if (!worldFound)
			{
				(Console.Out as DebugLogWriter).SetWarningOnce();
				Console.WriteLine("World file not exist: " + worldFileName);
				return false;
			}

			replaceAllIncludedModel();

			ConvertUriToAbsolutePath();

			// Console.WriteLine("Load World");
			var worldNode = doc.SelectSingleNode("/sdf/world");
			world = new World(worldNode);

			// Console.WriteLine("Load Completed!!!");

			// Print all SDF contents
			// StringWriter sw = new StringWriter();
			// XmlTextWriter xw = new XmlTextWriter(sw);
			// doc.WriteTo(xw);
			// Console.WriteLine(sw.ToString());

			return true;
		}

		public void updateResourceModelTable()
		{
			if (resourceModelTable == null)
			{
				Console.WriteLine("ERROR: Resource model table is not initialized!!!!");
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
					Console.WriteLine("Directory does not exists: " + modelPath);
					continue;
				}

				var rootDirectory = new DirectoryInfo(modelPath);
				//Console.WriteLine(">>> Model Default Path: " + modelPath);

				// Loop models
				foreach (var subDirectory in rootDirectory.GetDirectories())
				{
					if (subDirectory.Name.StartsWith("."))
					{
						continue;
					}

					//Console.WriteLine(subDirectory.Name + " => " + subDirectory.FullName);
					var modelConfig = subDirectory.FullName + "/model.config";

					if (!File.Exists(modelConfig))
					{
						Console.WriteLine("File does not exists: " + modelConfig);
						continue;
					}

					try
					{
						modelConfigDoc.Load(modelConfig);
					}
					catch (XmlException e)
					{
						Console.WriteLine("Failed to Load model file(" + modelConfig  + ") - " + e.Message);
						continue;
					}

					// Get Model name
					var modelName = subDirectory.Name; //modelNode["name"].InnerText;

					// Get Model root
					var modelNode = modelConfigDoc.SelectSingleNode("model");

					// Get Model SDF file name
					var sdfFileName = string.Empty;
					foreach (var version in sdfVersions)
					{
						//Console.WriteLine(version);
						var sdfNode = modelNode.SelectSingleNode("sdf[@version=" + version + " or not(@version)]");
						if (sdfNode != null)
						{
							sdfFileName = sdfNode.InnerText;
							sdfVersion = version;
							//Console.WriteLine(version + "," + sdfFileName);
							break;
						}
					}

					if (string.IsNullOrEmpty(sdfFileName))
					{
						Console.WriteLine(modelName + ": SDF FileName is empty!!");
						continue;
					}

					// Insert resource table
					var modelValue = new Tuple<string, string>(subDirectory.FullName, sdfFileName);
					try
					{
						// Console.WriteLine(modelName + ":" + subDirectory.FullName + ":" + sdfFileName);
						// Console.WriteLine(modelName + ", " + modelValue);
						if (resourceModelTable.ContainsKey(modelName))
						{
							failedModelTableList.AppendLine("");
							failedModelTableList.Append(modelName + " => Cannot register" + modelValue);
							numberOfFailedModelTable++;
						}
						else
						{
							resourceModelTable.Add(modelName, modelValue);
						}
					}
					catch (NullReferenceException e)
					{
						Console.WriteLine(e.Message);
					}
				}
			}

			if (numberOfFailedModelTable > 0)
			{
				failedModelTableList.Insert(0, "All failed models(" + numberOfFailedModelTable + ") are already registered.");
				(Console.Out as DebugLogWriter).SetWarningOnce();
				Console.WriteLine(failedModelTableList);
			}

			Console.WriteLine("Total Models: " + resourceModelTable.Count);
		}

		// Converting media/file uri
		private void ConvertUriToAbsolutePath()
		{
			XmlNodeList nodeList = doc.SelectNodes("//uri");
			// Console.WriteLine("Num Of uri nodes: " + nodeList.Count);
			foreach (XmlNode node in nodeList)
			{
				string uri = node.InnerText;
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
					string mediaUri = uri.Replace("file://", fileDefaultPath);
					node.InnerText = mediaUri;
				}
				else
				{
					Console.WriteLine("Cannot convert uri: " + uri);
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
				// 	Console.WriteLine("Num Of Included Model nodes: " + nodes.Count);

				foreach (XmlNode node in nodes)
				{
					var modelNode = GetIncludedModel(node);

					if (modelNode != null)
					{
						// Console.WriteLine("Node - " + modelNode);
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

		private XmlNode GetIncludedModel(XmlNode _node)
		{
			var nameNode = _node.SelectSingleNode("name");
			var name = (nameNode == null) ? null : nameNode.InnerText;

			var poseNode = _node.SelectSingleNode("pose");
			var pose = (poseNode == null) ? null : poseNode.InnerText;

			var staticNode = _node.SelectSingleNode("static");
			var isStatic = (staticNode == null) ? null : staticNode.InnerText;

			var uri = _node.SelectSingleNode("uri").InnerText;

			// Console.WriteLineFormat("{0} | {1} | {2} | {3}", name, uri, pose, isStatic);

			Tuple<string, string> value;
			var modelName = uri.Replace("model://", string.Empty);
			if (resourceModelTable.TryGetValue(modelName, out value))
			{
				uri = value.Item1 + "/" + value.Item2;
			}
			else
			{
				Console.WriteLine("Not exists in database: " + uri);
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
				Console.WriteLine(errorMessage);
				simulationDisplay?.SetEventMessage(errorMessage);
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