/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public partial class MarkerVisualizer : MonoBehaviour
{
	public bool RemoveMarkers()
	{
		foreach (var item in request.markers)
		{
			var markerName = item.MarkerName();

			if (registeredMarkers[markerName] == null)
			{
				continue;
			}
			else
			{
				var markerSet =  registeredMarkers[markerName] as Tuple<Marker, GameObject>;
				// Debug.Log("Remove Marker: " + targetObject.name);
				Destroy(markerSet.Item2);

				registeredMarkers.Remove(markerName);
			}
		}

		return true;
	}
}