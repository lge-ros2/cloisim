/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

public partial class MarkerVisualizer : MonoBehaviour
{
	bool AddMarkers()
	{
		GameObject marker = null;
		try
		{
			foreach (var item in request.markers)
			{
				var markerName = item.MarkerName();
				if (registeredMarkers[markerName] != null)
				{
					Debug.LogWarning(markerName + " is Already Exist in visual marker list!!!");
					return false;
				}
			}

			foreach (var item in request.markers)
			{
				var markerName = item.MarkerName();

				marker = null;
				switch (item.type)
				{
					case Marker.Types.Line:
						AddMarkerLine(markerName, item, out marker);
						break;

					case Marker.Types.Box:
						AddMarkerBox(markerName, item, out marker);
						break;

					case Marker.Types.Sphere:
						AddMarkerSphere(markerName, item, out marker);
						break;

					case Marker.Types.Text:
						AddMarkerText(markerName, item, out marker);
						break;

					case Marker.Types.Unknown:
					default:
						break;
				}

				if (marker != null)
				{
					marker.tag = "Marker";
					if (rootMarkers != null)
					{
						if (marker.transform.parent == null)
						{
							marker.transform.SetParent(rootMarkers.transform, true);
						}
					}

					var newMarkerSet = new Tuple<MarkerRequest, GameObject>(item, marker);
					registeredMarkers.Add(markerName, newMarkerSet);
#if UNITY_EDITOR
					SceneVisibilityManager.instance.DisablePicking(marker, true);
#endif
				}
			}
		}
		catch
		{
			return false;
		}

		return true;
	}

	private void AddMarkerLine(in string markerName, MarkerRequest properties, out GameObject markerObject)
	{
		var markerProperties = properties.line;
		if (markerProperties != null)
		{
			markerObject = new GameObject(markerName);
			var lineRenderer = markerObject.AddComponent<LineRenderer>();
			lineRenderer.startWidth = markerProperties.size;
			lineRenderer.endWidth = markerProperties.size;
			lineRenderer.SetPosition(0, markerProperties.point);
			lineRenderer.SetPosition(1, markerProperties.endpoint);
			lineRenderer.alignment = LineAlignment.View;
			lineRenderer.startColor = properties.GetColor();
			lineRenderer.endColor = properties.GetColor();
			SetDefaultMeshRenderer(lineRenderer);
		}
		else
		{
			markerObject = null;
		}
	}

	private void AddMarkerBox(in string markerName, in MarkerRequest properties, out GameObject markerObject)
	{
		var markerProperties = properties.box;
		if (markerProperties != null)
		{
			markerObject = new GameObject(markerName);
			markerObject.transform.position = markerProperties.point;
			markerObject.transform.localScale.Set(markerProperties.size, markerProperties.size, markerProperties.size);
			var meshFilter = markerObject.AddComponent<MeshFilter>();
			meshFilter.mesh = ProceduralMesh.CreateBox();

			var meshRenderer = markerObject.AddComponent<MeshRenderer>();
			SetDefaultMeshRenderer(meshRenderer);
			meshRenderer.material.color = properties.GetColor();
		}
		else
		{
			markerObject = null;
		}
	}

	private void AddMarkerSphere(in string markerName, in MarkerRequest properties, out GameObject markerObject)
	{
		var markerProperties = properties.sphere;
		if (markerProperties != null)
		{
			markerObject = new GameObject(markerName);

			var meshFilter = markerObject.AddComponent<MeshFilter>();
			meshFilter.mesh = ProceduralMesh.CreateSphere(1, 12, 12);
			var meshRenderer = markerObject.AddComponent<MeshRenderer>();
			SetDefaultMeshRenderer(meshRenderer);
			meshRenderer.material.color = properties.GetColor();

			markerObject.transform.position = markerProperties.point;
			markerObject.transform.localScale.Set(markerProperties.size, markerProperties.size, markerProperties.size);
		}
		else
		{
			markerObject = null;
		}
	}

	private void AddMarkerText(in string markerName, in MarkerRequest properties, out GameObject markerObject)
	{
		var markerProperties = properties.text;
		if (markerProperties != null)
		{
			markerObject = new GameObject(markerName);

			var text = markerObject.AddComponent<TextMeshPro>();
			text.fontSize = (int)markerProperties.size;
			text.alignment = GetTextAlignment(markerProperties.align);
			text.text = markerProperties.text;
			text.enableWordWrapping = false;
			text.overflowMode = TextOverflowModes.Overflow;

			var rectTransform = markerObject.GetComponent<RectTransform>();
			rectTransform.position = markerProperties.point;
			rectTransform.localScale  = new Vector3(-1, 1, 1);

			if (!string.IsNullOrEmpty(markerProperties.following))
			{
				AddFollowingObjectByText(markerName, markerProperties.following, text);
			}
		}
		else
		{
			markerObject = null;
		}
	}

	static public TextAlignmentOptions GetTextAlignment(in MarkerText.TextAlign align)
	{
		var alignment = TextAlignmentOptions.Center;
		switch (align)
		{
			case MarkerText.TextAlign.Right:
				alignment = TextAlignmentOptions.MidlineRight;
				break;

			case MarkerText.TextAlign.Center:
				alignment = TextAlignmentOptions.Center;
				break;

			case MarkerText.TextAlign.Left:
			default:
				alignment = TextAlignmentOptions.MidlineLeft;
				break;
		}
		return alignment;
	}

	private void AddFollowingObjectByText(in string markerName, in string targetFollowingObjectName, TextMeshPro text)
	{
		if (registeredObjectsForFollowingText[markerName] != null)
		{
			Debug.LogWarning("Already registered!! " + markerName);
			return;
		}

		var followingObject = GameObject.Find(targetFollowingObjectName);
		if (followingObject != null)
		{
			registeredObjectsForFollowingText.Add(markerName, followingObject);
			followingTextMarkers.Add(markerName, text);
		}
	}
}