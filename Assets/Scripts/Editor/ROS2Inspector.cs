/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(UnityEngine.Transform), true), CanEditMultipleObjects]
public class ROS2Inspector : Editor
{
	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		var targetTransform = (UnityEngine.Transform)target;
		if (targetTransform.CompareTag("Props") ||
			targetTransform.CompareTag("Model") ||
			targetTransform.CompareTag("Link") ||
			targetTransform.CompareTag("Geometry") ||
			targetTransform.CompareTag("Visual") ||
			targetTransform.CompareTag("Collision") ||
			targetTransform.CompareTag("Sensor") ||
			targetTransform.CompareTag("Actor") ||
			targetTransform.CompareTag("Light"))
		{
			targetTransform.GetLocalPositionAndRotation(out var localPosition, out var localRotation);
			var sdfPosition = Unity2SDF.Position(localPosition);
			var sdfRotation = Unity2SDF.Rotation(localRotation);
			var ros2Position = new Vector3((float)sdfPosition.X, (float)sdfPosition.Y, (float)sdfPosition.Z);
			var ros2Rotation = new Vector3((float)sdfRotation.Roll, (float)sdfRotation.Pitch, (float)sdfRotation.Yaw);
			var ros2RotationDegree = ros2Rotation * Mathf.Rad2Deg;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("ROS2 Coordinates", EditorStyles.boldLabel);

			// GUI.enabled = false;
			EditorGUILayout.Vector3Field("Position (m)", ros2Position);
			EditorGUILayout.Vector3Field("Rotation(Roll, Pitch, Yaw) (rad)", ros2Rotation);
			EditorGUILayout.Vector3Field("Rotation(Roll, Pitch, Yaw) (deg)", ros2RotationDegree);
			// GUI.enabled = true;

			if (GUILayout.Button("Copy pose"))
			{
				var poseText = $"{ros2Position.x.ToString("f10")} {ros2Position.y.ToString("f10")} {ros2Position.z.ToString("f10")} "
							+ $"{ros2Rotation.x.ToString("f10")} {ros2Rotation.y.ToString("f10")} {ros2Rotation.z.ToString("f10")}";
				GUIUtility.systemCopyBuffer = poseText;
				Debug.LogFormat("Pose '{0}' Copied for {1}", poseText, targetTransform.name);
			}
		}
	}
}
