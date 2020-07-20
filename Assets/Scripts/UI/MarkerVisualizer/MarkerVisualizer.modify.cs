/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using TMPro;

public partial class MarkerVisualizer : MonoBehaviour
{
	public bool ModifyMarkers()
	{
		var modifieddCount = 0;

		foreach (var newMarker in request.markers)
		{
			var markerName = newMarker.MarkerName();

			if (registeredMarkers[markerName] == null)
			{
				Debug.LogWarning(markerName + " is NOT registred!!!!");
				continue;
			}
			else
			{
				var markerSet = registeredMarkers[markerName] as Tuple<MarkerRequest, GameObject>;
				var oldMarker = markerSet.Item1;
				var markerObject = markerSet.Item2;
				oldMarker = newMarker;

				switch (newMarker.type)
				{
					case Marker.Types.Line:
						ModifyMarkerLine(markerObject, newMarker);
						break;

					case Marker.Types.Box:
						ModifyMarkerBox(markerObject, newMarker);
						break;

					case Marker.Types.Sphere:
						ModifyMarkerSphere(markerObject, newMarker);
						break;

					case Marker.Types.Text:
						ModifyMarkerText(markerObject, newMarker);
						break;

					case Marker.Types.Unknown:
					default:
						break;
				}

				modifieddCount++;
			}
		}

		return (modifieddCount == 0)? false:true;
	}

	private void ModifyMarkerLine(in GameObject markerObject, in MarkerRequest properties)
	{
		var markerProperties = properties.line;

		if (markerProperties == null)
		{
			return;
		}

		var lineRenderer = markerObject.GetComponent<LineRenderer>();
		lineRenderer.startWidth = markerProperties.size;
		lineRenderer.endWidth = markerProperties.size;
		lineRenderer.SetPosition(0, markerProperties.point);
		lineRenderer.SetPosition(1, markerProperties.endpoint);
		lineRenderer.alignment = LineAlignment.View;
		lineRenderer.startColor = properties.GetColor();
		lineRenderer.endColor = properties.GetColor();
	}

	private void ModifyMarkerBox(in GameObject markerObject, in MarkerRequest properties)
	{
		var markerProperties = properties.box;
		if (markerProperties == null)
		{
			return;
		}

		markerObject.transform.position = markerProperties.point;
		markerObject.transform.localScale.Set(markerProperties.size, markerProperties.size, markerProperties.size);

		var meshRenderer = markerObject.GetComponent<MeshRenderer>();
		meshRenderer.material.color = properties.GetColor();
	}

	private void ModifyMarkerSphere(in GameObject markerObject, in MarkerRequest properties)
	{
		var markerProperties = properties.sphere;
		if (markerProperties == null)
		{
			return;
		}

		markerObject.transform.position = markerProperties.point;
		markerObject.transform.localScale.Set(markerProperties.size, markerProperties.size, markerProperties.size);

		var meshRenderer = markerObject.GetComponent<MeshRenderer>();
		meshRenderer.material.color = properties.GetColor();
	}

	private void ModifyMarkerText(in GameObject markerObject, in MarkerRequest properties)
	{
		var markerProperties = properties.text;
		if (markerProperties == null)
		{
			return;
		}

		var text = markerObject.GetComponent<TextMeshPro>();
		text.fontSize = (int)markerProperties.size;
		text.alignment = GetTextAlignment(markerProperties.align);
		text.text = markerProperties.text;

		var markerName = markerObject.name;
		if (string.IsNullOrEmpty(markerProperties.following))
		{
			RemoveFollowingObjectByText(markerName);
			markerObject.transform.position = markerProperties.point;
		}
		else
		{
			var registeredObject = registeredObjectsForFollowingText[markerName];
			if (registeredObject != null)
			{
				// if following name is changed, remove exists one
				var followingObject = registeredObject as GameObject;

				if (!markerProperties.following.Equals(followingObject.name))
				{
					RemoveFollowingObjectByText(followingObject.name);
				}
			}

			AddFollowingObjectByText(markerName, markerProperties.following, text);

			var currentPosition = markerObject.transform.position;
			var tempPosition = markerProperties.point;
			tempPosition.x = currentPosition.x;
			tempPosition.z = currentPosition.z;
			markerObject.transform.position = tempPosition;
		}
	}
}