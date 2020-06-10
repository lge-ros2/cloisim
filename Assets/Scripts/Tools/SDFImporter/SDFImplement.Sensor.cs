/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public partial class SDFImplement
{
	public class Sensor
	{
		private static string GetFrameName(in GameObject currentObject)
		{
			string frameName = "";

			var nextObject = currentObject.transform.parent;

			do
			{
				frameName = "::" + nextObject.name + frameName;
				nextObject = nextObject.transform.parent;

			} while (!nextObject.Equals(nextObject.transform.root));

			return frameName.Substring(2);
		}

		private static void AttachSensor(in GameObject sensorObject, in GameObject targetObject)
		{
			try
			{
				var sensorTransform = sensorObject.transform;
				sensorTransform.position = Vector3.zero;
				sensorTransform.rotation = Quaternion.identity;
				sensorTransform.SetParent(targetObject.transform, false);
				sensorTransform.localScale = Vector3.one;
				sensorTransform.localPosition = Vector3.zero;
				sensorTransform.localRotation = Quaternion.identity;
			}
			catch
			{
				Debug.Log("sensorObject is null or Invalid obejct exist");
			}
		}

		public static void TransformSensor(in GameObject sensorObject, SDF.Pose<double> sensorPose)
		{
			try
			{
				var sensorTransform = sensorObject.transform;
				sensorTransform.localPosition = SDF2Unity.GetPosition(sensorPose.Pos);
				sensorTransform.localRotation = SDF2Unity.GetRotation(sensorPose.Rot);
			}
			catch
			{
				Debug.Log("sensorObject is null or Invalid obejct exist");
			}
		}

		public static Device AddLidar(in SDF.Ray element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);

			var lidar = newSensorObject.AddComponent<SensorDevices.Lidar>();

			lidar.deviceName = GetFrameName(newSensorObject);
			lidar.samples = element.horizontal.samples;
			lidar.rangeMin = element.range_min;
			lidar.rangeMax = element.range_max;
			lidar.angleMin = element.horizontal.min_angle * Mathf.Rad2Deg;
			lidar.angleMax = element.horizontal.max_angle * Mathf.Rad2Deg;
			lidar.resolution = element.horizontal.resolution;
			lidar.verticalSamples = element.vertical.samples;
			lidar.verticalAngleMin = element.vertical.min_angle;
			lidar.verticalAngleMax = element.vertical.max_angle;
			// lidar.hideFlags = HideFlags.NotEditable;

			return lidar;
		}

		public static Device AddCamera(in SDF.Camera element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);
			TransformSensor(newSensorObject, element.Pose);
			var camera = newSensorObject.AddComponent<SensorDevices.Camera>();
			camera.deviceName = GetFrameName(newSensorObject);
			camera.parameters = element;
			return camera;
		}

		public static Device AddDepthCamera(in SDF.Camera element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);
			TransformSensor(newSensorObject, element.Pose);

			var depthCamera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
			depthCamera.deviceName = GetFrameName(newSensorObject);
			depthCamera.parameters = element;
			return depthCamera;
		}

		public static Device AddMultiCamera(in SDF.Cameras element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);

			var multicamera = newSensorObject.AddComponent<SensorDevices.MultiCamera>();
			multicamera.deviceName = GetFrameName(newSensorObject);
			multicamera.parameters = element;

			return multicamera;
		}

		public static Device AddSonar(in SDF.Sonar element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);

			var sonar = newSensorObject.AddComponent<SensorDevices.Sonar>();
			sonar.deviceName = GetFrameName(newSensorObject);
			sonar.geometry = element.geometry;
			sonar.rangeMin = element.min;
			sonar.rangeMax = element.max;
			sonar.radius = element.radius;

			return sonar;
		}

		public static Device AddImu(in SDF.IMU element, in GameObject targetObject)
		{
			var newSensorObject = new GameObject();
			AttachSensor(newSensorObject, targetObject);

			var imu = newSensorObject.AddComponent<SensorDevices.IMU>();
			imu.deviceName = GetFrameName(newSensorObject);

			return imu;
		}
	}
}
