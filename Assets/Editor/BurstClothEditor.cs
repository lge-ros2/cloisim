/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using UnityEditor;
using CLOiSim.Cloth;

[CustomEditor(typeof(BurstCloth))]
public class BurstClothEditor : Editor
{
	private int _draggedVertex = -1;
	private int _activeHandleId = 0;

	private void OnSceneGUI()
	{
		var cloth = (BurstCloth)target;
		if (!cloth.IsInitialized || !cloth.DrawVertices) return;

		var positions = cloth.GetPositions();
		if (!positions.IsCreated) return;

		// Release detection: if we were dragging and the hot control is no longer our handle
		if (_draggedVertex >= 0 && _activeHandleId != 0 && GUIUtility.hotControl != _activeHandleId)
		{
			cloth.ReleaseVertex(_draggedVertex);
			cloth.Paused = false;
			_draggedVertex = -1;
			_activeHandleId = 0;
			SceneView.RepaintAll();
		}

		var handleSize = cloth.VertexSize * 2f;

		for (var i = 0; i < positions.Length; i++)
		{
			var isGrabbed = cloth.IsVertexGrabbed(i);
			var isPinned = cloth.IsVertexPinned(i) && !isGrabbed;

			if (isPinned && !cloth.DrawPinnedVertices) continue;

			// Skip pinned (non-grabbed) vertices — they can't be moved
			if (isPinned) continue;

			var color = isGrabbed ? cloth.GrabbedVertexColor : cloth.FreeVertexColor;

			Handles.color = color;
			var worldPos = (Vector3)positions[i];
			var size = HandleUtility.GetHandleSize(worldPos) * handleSize;

			// Capture the control ID that will be used by FreeMoveHandle
			var controlId = GUIUtility.GetControlID(FocusType.Passive);

			EditorGUI.BeginChangeCheck();
			var newPos = Handles.FreeMoveHandle(controlId, worldPos, size, Vector3.zero, Handles.SphereHandleCap);

			if (EditorGUI.EndChangeCheck())
			{
				// Snap to the closest free vertex from the handle position
				var snapIndex = i;
				var bestDistSq = float.MaxValue;
				for (var j = 0; j < positions.Length; j++)
				{
					if (cloth.IsVertexPinned(j) || cloth.IsVertexGrabbed(j)) continue;
					var dSq = ((Vector3)positions[j] - newPos).sqrMagnitude;
					if (dSq < bestDistSq)
					{
						bestDistSq = dSq;
						snapIndex = j;
					}
				}

				if (_draggedVertex != snapIndex)
				{
					if (_draggedVertex >= 0)
					{
						cloth.ReleaseVertex(_draggedVertex);
					}
					cloth.Paused = true;
					cloth.GrabVertex(snapIndex, newPos);
					_draggedVertex = snapIndex;
				}
				_activeHandleId = GUIUtility.hotControl;
				cloth.UpdateGrabPosition(_draggedVertex, newPos);
				SceneView.RepaintAll();
			}
		}
	}
}
