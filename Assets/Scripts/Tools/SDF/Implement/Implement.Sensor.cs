/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Implement
	{
		public static class Sensor
		{
			private static string GetFrameName(this UE.GameObject currentObject)
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

			private static void AttachSensor(
				this UE.GameObject targetObject,
				in UE.GameObject sensorObject,
				Pose<double> sensorPose = null)
			{
				try
				{
					var sensorTransform = sensorObject.transform;
					sensorTransform.position = UE.Vector3.zero;
					sensorTransform.rotation = UE.Quaternion.identity;
					sensorTransform.SetParent(targetObject.transform, false);
					sensorTransform.localScale = UE.Vector3.one;
					sensorTransform.localPosition = SDF2Unity.Position(sensorPose?.Pos);
					sensorTransform.localRotation = SDF2Unity.Rotation(sensorPose?.Rot);
				}
				catch
				{
					UE.Debug.Log("sensorObject is null or Invalid obejct exist");
				}
			}

			public static Device AddLidar(this UE.GameObject targetObject, in SDF.Lidar element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var lidar = newSensorObject.AddComponent<SensorDevices.Lidar>();
				lidar.DeviceName = newSensorObject.GetFrameName();
				lidar.scanRange = new MathUtil.MinMax(element.range.min, element.range.max);
				lidar.rangeResolution = (float)element.range.resolution;
				var horizontal = element.horizontal;
				lidar.horizontal = new SensorDevices.LaserData.Scan(horizontal.samples, horizontal.min_angle, horizontal.max_angle, horizontal.resolution);

				var vertical = element.vertical;
				if (vertical != null)
				{
					lidar.vertical = new SensorDevices.LaserData.Scan(vertical.samples, vertical.min_angle, vertical.max_angle, vertical.resolution);
				}

				lidar.SetupNoise(element.noise);

				return lidar;
			}

			public static Device AddCamera(this UE.GameObject targetObject, in SDF.Camera element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, element.Pose);

				var camera = newSensorObject.AddComponent<SensorDevices.Camera>();
				camera.tag = "Sensor";
				camera.DeviceName = newSensorObject.GetFrameName();
				camera.SetParameter(element);

				camera.SetupNoise(element.noise);

				return camera;
			}

			public static Device AddSegmentationCamera(this UE.GameObject targetObject, in SDF.Camera element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, element.Pose);

				var camera = newSensorObject.AddComponent<SensorDevices.SegmentationCamera>();
				camera.DeviceName = newSensorObject.GetFrameName();

				switch (element.image.format)
				{
					case "L16":
					case "L_UINT16":
						// Debug.Log("Supporting data type for Depth camera");
						break;

					default:
						if (element.image.format.Equals(string.Empty))
						{
							UE.Debug.LogWarningFormat("'L16' will be set for Depth camera({0})'s image format", element.name);
						}
						else
						{
							UE.Debug.LogWarningFormat("Not supporting data type({0}) for Depth camera", element.image.format);
						}
						element.image.format = "L16";
						break;
				}

				camera.SetParameter(element);

				camera.SetupNoise(element.noise);

				return camera;
			}

			public static Device AddDepthCamera(this UE.GameObject targetObject, in SDF.Camera element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, element.Pose);

				var camera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
				camera.DeviceName = newSensorObject.GetFrameName();

				switch (element.image.format)
				{
					case "L16":
					case "L8":
					case "R_FLOAT32":
					case "L_UINT16":
					case "L_INT16":
					case "L_INT8":
						// Debug.Log("Supporting data type for Depth camera");
						break;

					default:
						if (element.image.format.Equals(string.Empty))
						{
							UE.Debug.LogWarningFormat("'L16' will be set for Depth camera({0})'s image format", element.name);
						}
						else
						{
							UE.Debug.LogWarningFormat("Not supporting data type({0}) for Depth camera", element.image.format);
						}
						element.image.format = "L16";
						break;
				}

				camera.SetParameter(element);

				camera.SetupNoise(element.noise);

				return camera;
			}

			public static Device AddMultiCamera(this UE.GameObject targetObject, in SDF.Cameras element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var multicamera = newSensorObject.AddComponent<SensorDevices.MultiCamera>();
				multicamera.DeviceName = newSensorObject.GetFrameName();
				foreach (var camParam in element.cameras)
				{
					var newCam = AddCamera(newSensorObject, camParam);
					newCam.name = camParam.name;
					newCam.Mode = Device.ModeType.NONE;
					newCam.DeviceName = multicamera.DeviceName + "::" + element.name + "::" + newCam.name;
					multicamera.AddCamera((SensorDevices.Camera)newCam);
				}

				return multicamera;
			}

			public static Device AddSonar(this UE.GameObject targetObject, in SDF.Sonar element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var sonar = newSensorObject.AddComponent<SensorDevices.Sonar>();
				sonar.DeviceName = newSensorObject.GetFrameName();
				sonar.Geometry = element.geometry;
				sonar.RangeMin = element.min;
				sonar.RangeMax = element.max;
				sonar.Radius = element.radius;

				return sonar;
			}

			public static Device AddImu(this UE.GameObject targetObject, in SDF.IMU element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var imu = newSensorObject.AddComponent<SensorDevices.IMU>();
				imu.DeviceName = newSensorObject.GetFrameName();

				imu.SetupNoises(element);

				return imu;
			}

			public static Device AddNavSat(this UE.GameObject targetObject, in SDF.NavSat element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var gps = newSensorObject.AddComponent<SensorDevices.GPS>();
				gps.DeviceName = newSensorObject.GetFrameName();
				gps.SetupNoises(element);

				return gps;
			}


			public static Device AddContact(this UE.GameObject targetObject, in SDF.Contact element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var contact = newSensorObject.AddComponent<SensorDevices.Contact>();
				contact.DeviceName = newSensorObject.GetFrameName();
				contact.TargetCollision = element.collision;
				contact.Topic = element.topic;

				var contactTrigger = targetObject.AddComponent<SensorDevices.ContactTrigger>();
				contactTrigger.collisionStay = contact.CollisionStay;
				contactTrigger.collisionExit = contact.CollisionExit;

				return contact;
			}
		}
	}
}