/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using Splines = UnityEngine.Splines;
using System.Xml;
using System.Collections.Generic;


public class WorldSaver
{
	private XmlDocument _doc = null;
	private XmlNode _worldNode = null;

	public WorldSaver(in XmlDocument doc)
	{
		_doc = doc;
		_worldNode = GetNode(_doc, "sdf/world");
	}

	private XmlNode GetNode(XmlNode node, in string xpath)
	{
		return (node == null) ? null : node.SelectSingleNode(xpath);
	}

	private XmlNodeList GetNodes(XmlNode node, in string xpath)
	{
		return (node == null) ? null : node.SelectNodes(xpath);
	}

	public void Update()
	{
		ClearAllComments();
		UpdateGUI();
		UpdateModels();
		UpdateRoads();
	}

	private void ClearAllComments()
	{
		var list = _doc.SelectNodes("//comment()");

		foreach (XmlNode node in list)
		{
			node.ParentNode.RemoveChild(node);
		}
	}

	private void CleanIncludedModels()
	{
		var includes = GetNodes(_worldNode, "include");
		for (var i = includes.Count - 1; i >= 0; i--)
		{
			_worldNode.RemoveChild(includes[i]);
		}
	}

	private void CleanModels()
	{
		var worldModelList = new List<string>();
		var worldTransform = Main.WorldRoot.transform;
		for (var i = 0; i < worldTransform.childCount; i++)
		{
			var childTransform = worldTransform.GetChild(i);
			var modelName = childTransform.name;
			// Debug.Log("model-in-world= " + modelName);
			worldModelList.Add(modelName);
		}

		var models = GetNodes(_worldNode, "model");
		for (var i = models.Count - 1; i >= 0; i--)
		{
			var modelName = models[i].Attributes["name"].Value;
			if (!worldModelList.Exists(x => x == modelName))
			{
				// Debug.Log("removed in world :" + modelName);
				_worldNode.RemoveChild(models[i]);
			}
		}
	}

	private XmlNode GetModel(in string modelName)
	{
		return GetNode(_worldNode, $"model[@name='{modelName}']");
	}

	private void UpdateGUI()
	{
		var mainCamera = Camera.main;
		if (mainCamera == null)
		{
			return;
		}

		var guiNode = GetNode(_worldNode, "gui");
		if (guiNode == null)
		{
			guiNode = _doc.CreateElement("gui");
			_worldNode.AppendChild(guiNode);
		}

		var cameraNode = GetNode(guiNode, "camera");
		if (cameraNode == null)
		{
			cameraNode = _doc.CreateElement("camera");
			guiNode.AppendChild(cameraNode);
		}

		var cameraPoseNode = GetNode(cameraNode, "pose");
		if (cameraPoseNode == null)
		{
			cameraPoseNode = _doc.CreateElement("pose");
			cameraNode.AppendChild(cameraPoseNode);
		}

		var camPosition = Unity2SDF.Position(mainCamera.transform.localPosition);
		var camRotation = Unity2SDF.Rotation(mainCamera.transform.localRotation);
		var pose = Unity2SDF.Pose(camPosition, camRotation);
		cameraPoseNode.InnerText = pose.ToString();
	}

	private void UpdateModels()
	{
		CleanIncludedModels();

		CleanModels();

		var worldTransform = Main.WorldRoot.transform;
		for (var i = 0; i < worldTransform.childCount; i++)
		{
			var childTransform = worldTransform.GetChild(i);

			var modelName = childTransform.name;
			var isStatic = childTransform.gameObject.isStatic;
			var position = Unity2SDF.Position(childTransform.localPosition);
			var rotation = Unity2SDF.Rotation(childTransform.localRotation);
			var pose = Unity2SDF.Pose(position, rotation);

			var model = GetModel(modelName);
			// Debug.Log(modelName);

			if (model == null)
			{
				model = _doc.CreateElement("include");

				var modelNameNode = _doc.CreateElement("name");
				modelNameNode.InnerText = modelName;
				model.AppendChild(modelNameNode);

				var modelHelper = childTransform.GetComponent<SDF.Helper.Model>();

				var uriNode = _doc.CreateElement("uri");
				uriNode.InnerText = $"model://{modelHelper.modelNameInPath}";
				model.AppendChild(uriNode);

				_worldNode.AppendChild(model);
			}

			var staticNode = GetNode(model, "static");
			if (staticNode == null)
			{
				staticNode = _doc.CreateElement("static");
				model.AppendChild(staticNode);
			}
			staticNode.InnerText = isStatic.ToString().ToLower();

			var poseNode = GetNode(model, "pose");
			if (poseNode == null)
			{
				poseNode = _doc.CreateElement("pose");
				model.AppendChild(poseNode);
			}
			poseNode.InnerText = pose.ToString();
		}
	}

	private void ClearRoadPoints(XmlNode roadNode)
	{
		var points = GetNodes(roadNode, "point");
		for (var i = points.Count - 1; i >= 0; i--)
		{
			roadNode.RemoveChild(points[i]);
		}
	}

	private void UpdateRoads()
	{
		// Debug.Log("UpdateRoads");
		var roadTransforms = Main.RoadsRoot.GetComponentsInChildren<Transform>();


		foreach (XmlNode roadNode in GetNodes(_worldNode, "road"))
		{
			var roadName = roadNode.SelectSingleNode("name").InnerText;
			// Debug.Log(roadName);

			ClearRoadPoints(roadNode);

			foreach (var roadTransform in roadTransforms)
			{
				if (roadTransform.CompareTag("Road") && roadTransform.name.CompareTo(roadName) == 0)
				{
					AddRoadPoint(roadNode, roadTransform);
					break;
				}
			}
		}
	}

	private void AddRoadPoint(XmlNode roadNode, in Transform roadTransform)
	{
		var centerPos = roadTransform.localPosition;
		var splineContainer = roadTransform.GetComponent<Splines.SplineContainer>();
		var spline = splineContainer.Spline;

		for (var i = 0; i < spline.Count; i++)
		{
			var elem = _doc.CreateElement("point");
			var knotPos = spline[i].Position;
			var offset = new Vector3(knotPos.x, knotPos.y, knotPos.z);
			var point = offset + centerPos;
			elem.InnerText = Unity2SDF.Position(point).ToString();
			roadNode.AppendChild(elem);
		}
	}
}