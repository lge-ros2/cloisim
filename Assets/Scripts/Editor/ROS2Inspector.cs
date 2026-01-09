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
			var ros2Position = Unity2SDF.Position(localPosition).AsUnity();
			var ros2Rotation = Unity2SDF.Vector(localRotation.eulerAngles).AsUnity();
			var ros2RotationDegree = ros2Rotation * Mathf.Rad2Deg;

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("ROS2 Coordinates, ", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Rotation is displayed as Roll, Pitch, Yaw (Euler angles).", MessageType.None);

			// GUI.enabled = false;
			EditorGUILayout.Vector3Field("Position (m)", ros2Position);
			EditorGUILayout.Vector3Field("Rotation (rad)", ros2Rotation);
			EditorGUILayout.Vector3Field("Rotation (deg)", ros2RotationDegree);
			// GUI.enabled = true;

			if (GUILayout.Button("Copy pose"))
			{
				var poseText = $"{ros2Position.x.ToString("0.##########")} {ros2Position.y.ToString("0.##########")} {ros2Position.z.ToString("0.##########")} "
							+ $"{ros2Rotation.x.ToString("0.##########")} {ros2Rotation.y.ToString("0.##########")} {ros2Rotation.z.ToString("0.##########")}";
				GUIUtility.systemCopyBuffer = poseText;
				Debug.LogFormat("Pose '{0}' Copied for {1}", poseText, targetTransform.name);
			}

			var rb = targetTransform.GetComponent<Rigidbody>();
			var ab = targetTransform.GetComponent<ArticulationBody>();
			var bodyType = string.Empty;
			
			if (rb == null && ab == null)
			{
				return;
			}
			else if (ab != null)
			{
				bodyType = "ArticulationBody";
			}
			else
			{
				bodyType = "RigidBody";
			}

			EditorGUILayout.Space();
			EditorGUILayout.LabelField($"Physics Info: {bodyType}", EditorStyles.boldLabel);
			EditorGUILayout.HelpBox("Inertia Tensor Rotation is displayed as Roll, Pitch, Yaw (Euler angles).", MessageType.None);

			if (rb != null)
			{
				EditorGUILayout.Vector3Field("Velocity", Unity2SDF.Vector(rb.velocity).AsUnity());
				EditorGUILayout.Vector3Field("Angular Velocity", Unity2SDF.Vector(rb.angularVelocity).AsUnity());
				EditorGUILayout.Vector3Field("Inertia Tensor", Unity2SDF.Vector(rb.inertiaTensor).AsUnity());
				EditorGUILayout.Vector3Field("Inertia Tensor Rotation", Unity2SDF.Vector(rb.inertiaTensorRotation.eulerAngles).AsUnity());
				EditorGUILayout.Vector3Field("Center of Mass (Local)", Unity2SDF.Vector(rb.centerOfMass).AsUnity());
				EditorGUILayout.Vector3Field("Center of Mass (World)", Unity2SDF.Vector(rb.worldCenterOfMass).AsUnity());
			}
			else if (ab != null)
			{
				EditorGUILayout.Vector3Field("Velocity", Unity2SDF.Vector(ab.velocity).AsUnity());
				EditorGUILayout.Vector3Field("Angular Velocity", Unity2SDF.Vector(ab.angularVelocity).AsUnity());
				EditorGUILayout.Vector3Field("Inertia Tensor", Unity2SDF.Vector(ab.inertiaTensor).AsUnity());
				EditorGUILayout.Vector3Field("Inertia Tensor Rotation", Unity2SDF.Vector(ab.inertiaTensorRotation.eulerAngles).AsUnity());
				EditorGUILayout.Vector3Field("Center of Mass (Local)", Unity2SDF.Vector(ab.centerOfMass).AsUnity());
				EditorGUILayout.Vector3Field("Center of Mass (World)", Unity2SDF.Vector(ab.transform.TransformPoint(ab.centerOfMass)).AsUnity());
			}
			else
			{
				EditorGUILayout.LabelField("No RigidBody or ArticulationBody exist.");
			}
		}
	}
}
