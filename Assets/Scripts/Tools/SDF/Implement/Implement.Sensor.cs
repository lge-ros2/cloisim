/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SDF
{
	public partial class Implement
	{
		public class Sensor
		{
			private static string GetFrameName(in GameObject currentObject)
			{
				var frameName = string.Empty;
				var nextObject = currentObject.transform.parent;

				do
				{
					frameName = "::" + nextObject.name + frameName;
					nextObject = nextObject.transform.parent;

				} while (!nextObject.Equals(nextObject.transform.root));

				return frameName.Substring(2);
			}

			private static void AttachSensor(in GameObject sensorObject, in GameObject targetObject, SDF.Pose<double> sensorPose = null)
			{
				try
				{
					var sensorTransform = sensorObject.transform;
					sensorTransform.position = Vector3.zero;
					sensorTransform.rotation = Quaternion.identity;
					sensorTransform.SetParent(targetObject.transform, false);
					sensorTransform.localScale = Vector3.one;

					if (sensorPose == null)
					{
						sensorTransform.localPosition = Vector3.zero;
						sensorTransform.localRotation = Quaternion.identity;
					}
					else
					{
						sensorTransform.localPosition = SDF2Unity.GetPosition(sensorPose.Pos);
						sensorTransform.localRotation = SDF2Unity.GetRotation(sensorPose.Rot);
					}
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
				lidar.verticalAngleMin = element.vertical.min_angle * Mathf.Rad2Deg;
				lidar.verticalAngleMax = element.vertical.max_angle * Mathf.Rad2Deg;
				// lidar.hideFlags = HideFlags.NotEditable;

				return lidar;
			}

			public static Device AddCamera(in SDF.Camera element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject, element.Pose);

				var camera = newSensorObject.AddComponent<SensorDevices.Camera>();
				camera.deviceName = GetFrameName(newSensorObject);
				camera.SetDeviceParameter(element as SDF.SensorType);
				return camera;
			}

			public static Device AddDepthCamera(in SDF.Camera element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject, element.Pose);

				var depthCamera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
				depthCamera.deviceName = GetFrameName(newSensorObject);

				if (string.IsNullOrEmpty(element.image_format))
				{
					element.image_format = "R_FLOAT32";
				}
				else
				{
					switch (element.image_format)
					{
						case "L16":
						case "R_FLOAT16":
						case "R_FLOAT32":
						case "L_INT16":
						case "L_UINT16":
							// Debug.Log("Supporting data type for Depth camera");
							break;

						default:
							Debug.LogWarningFormat("Not supporting data type({0}) for Depth camera", element.image_format);
							element.image_format = "R_FLOAT32";
							break;
					}
				}

				depthCamera.SetDeviceParameter(element as SDF.SensorType);

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

			public static Device AddGps(in SDF.GPS element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var gps = newSensorObject.AddComponent<SensorDevices.GPS>();
				gps.deviceName = GetFrameName(newSensorObject);

				return gps;
			}

			public static Device AddContact(in SDF.Contact element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var contact = newSensorObject.AddComponent<SensorDevices.Contact>();
				contact.deviceName = GetFrameName(newSensorObject);
				contact.collision = element.collision;
				contact.topic = element.topic;

				var contactTrigger = targetObject.AddComponent<SensorDevices.ContactTrigger>();
				contactTrigger.collisionEnter = contact.CollisionEnter;
				contactTrigger.collisionExit = contact.CollisionExit;
				contactTrigger.collisionStay = contact.CollisionStay;

				return contact;
			}
		}
	}
}