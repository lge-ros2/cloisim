/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public partial class MarkerVisualizer : MonoBehaviour
{
	private List<MarkerResponseLine> requestedMarkerLineList;
	private List<MarkerResponseText> requestedMarkerTextList;
	private List<MarkerResponseBox> requestedMarkerBoxList;
	private List<MarkerResponseSphere> requestedMarkerSphereList;

	public void InitializeList()
	{
		requestedMarkerLineList = new List<MarkerResponseLine>();
		requestedMarkerTextList = new List<MarkerResponseText>();
		requestedMarkerBoxList = new List<MarkerResponseBox>();
		requestedMarkerSphereList = new List<MarkerResponseSphere>();
	}

	public bool ListMarkers()
	{
		requestedMarkerLineList.Clear();
		requestedMarkerTextList.Clear();
		requestedMarkerBoxList.Clear();
		requestedMarkerSphereList.Clear();

		var filter = request.filter;
		var filterOn = (filter == null)? false : ((filter.IsEmpty())? false : true);

		// if (filterOn)
		// {
		// 	Debug.Log(filter.group + ", " + filter.id + ", " + filter.type);
		// }

		foreach (DictionaryEntry item in registeredMarkers)
		{
			if (filterOn)
			{
				var markerName = item.Key.ToString();

				if (!string.IsNullOrEmpty(filter.group) && !markerName.StartsWith(filter.group))
				{
					continue;
				}

				if (filter.id > -1 && !markerName.Contains(SimulationService.Delimiter + filter.id + SimulationService.Delimiter))
				{
					continue;
				}

				if (!string.IsNullOrEmpty(filter.type) && !markerName.EndsWith(filter.type))
				{
					continue;
				}
			}

			var markerSet = item.Value as Tuple<MarkerRequest, GameObject>;
			var markerRequest = markerSet.Item1;

			switch (markerRequest.type)
			{
				case Marker.Types.Line:
					{
						var marker = new MarkerResponseLine();
						SetCommonMarkerInfo(marker, markerRequest);
						marker.marker = markerRequest.line;
						requestedMarkerLineList.Add(marker);
					}
					break;

				case Marker.Types.Sphere:
					{
						var marker = new MarkerResponseSphere();
						SetCommonMarkerInfo(marker, markerRequest);
						marker.marker = markerRequest.sphere;
						requestedMarkerSphereList.Add(marker);
					}
					break;

				case Marker.Types.Box:
					{
						var marker = new MarkerResponseBox();
						SetCommonMarkerInfo(marker, markerRequest);
						marker.marker = markerRequest.box;
						requestedMarkerBoxList.Add(marker);
					}
					break;

				case Marker.Types.Text:
					{
						var marker = new MarkerResponseText();
						SetCommonMarkerInfo(marker, markerRequest);
						marker.marker = markerRequest.text;
						requestedMarkerTextList.Add(marker);
					}
					break;

				case Marker.Types.Unknown:
				default:
					Debug.Log("Warning Unknonw marker type detected!!");
					break;
			}
		}

		response.texts = requestedMarkerTextList;
		response.lines = requestedMarkerLineList;
		response.boxes = requestedMarkerBoxList;
		response.spheres = requestedMarkerSphereList;

		return true;
	}

	private void SetCommonMarkerInfo(in Marker response, in Marker request)
	{
		response.group = request.group;
		response.id = request.id;
		response.type = request.type;
		response.color = request.color;
	}
}