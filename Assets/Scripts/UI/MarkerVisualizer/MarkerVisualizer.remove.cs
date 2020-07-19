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
		var removedCount = 0;

		foreach (var item in request.markers)
		{
			var markerName = item.MarkerName();
			var markerSet = registeredMarkers[markerName] as Tuple<MarkerRequest, GameObject>;

			if (markerSet == null)
			{
				continue;
			}
			else
			{
				// Debug.Log("Remove Marker: " + targetObject.name);
				if (markerSet.Item2 != null)
				{
					Destroy(markerSet.Item2);
				}

				registeredMarkers.Remove(markerName);
				removedCount++;

				// remove text object if it exists.
				RemoveFollowingObjectByText(markerName);
			}
		}

		return (removedCount == 0)? false:true;
	}

	private void RemoveFollowingObjectByText(in string markerName)
	{
		// remove if empty
		if (registeredObjectsForFollowingText[markerName] != null)
		{
			registeredObjectsForFollowingText.Remove(markerName);
			followingTextMarkers.Remove(markerName);
		}
	}
}