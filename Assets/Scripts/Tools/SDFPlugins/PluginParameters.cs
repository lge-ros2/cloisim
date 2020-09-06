/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

public class PluginParameters
{
	private XmlNode parameters = null;

	public void SetRootData(in XmlNode node)
	{
		parameters = node.SelectSingleNode(".");
	}

	public T GetAttribute<T>(in string xpath, in string attributeName, in T defaultValue = default(T))
	{
		var node = parameters.SelectSingleNode(xpath);
		if (node != null)
		{
			var attributes = node.Attributes;
			var attributeNode = attributes[attributeName];
			if (attributeNode != null)
			{
				var attributeValue = attributeNode.Value;
				return SDF.Entity.ConvertValueType<T>(attributeValue);
			}
		}

		return defaultValue;
	}

	public T GetValue<T>(in string xpath, T defaultValue = default(T))
	{
		if (string.IsNullOrEmpty(xpath) || parameters == null)
		{
			return defaultValue;
		}

		var node = parameters.SelectSingleNode(xpath);
		if (node == null)
		{
			return defaultValue;
		}

		try
		{
			return SDF.Entity.ConvertXmlNodeToValue<T>(node);
		}
		catch (XmlException ex)
		{
			Debug.LogErrorFormat("ERROR: GetValue with {0} : {1} ", xpath, ex.Message);
			return defaultValue;
		}
	}

	public bool GetValues<T>(in string xpath, out List<T> valueList)
	{
		valueList = null;

		var result = GetValues(xpath, out var nodeList);
		valueList = nodeList.ConvertAll(s => SDF.Entity.ConvertXmlNodeToValue<T>(s));

		return result;
	}

	public bool GetValues(in string xpath, out List<XmlNode> valueList)
	{
		valueList = null;

		if (string.IsNullOrEmpty(xpath) || parameters == null)
		{
			return false;
		}

		try
		{
			valueList = new List<XmlNode>(parameters.SelectNodes(xpath).Cast<XmlNode>());
			if (valueList == null)
			{
				return false;
			}

			return true;
		}
		catch (XmlException ex)
		{
			Debug.LogErrorFormat("ERROR: GetValue with {0} : {1} ", xpath, ex.Message);
			return false;
		}
	}

	public void PrintData()
	{
		if (parameters != null)
		{
			Debug.LogWarning(" Data is empty");
		}
		else
		{
			// Print all SDF contents
			var sw = new StringWriter();
			var xw = new XmlTextWriter(sw);
			parameters.WriteTo(xw);
			Debug.Log(sw.ToString());
		}
	}
}