/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDFormat
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
				SDFormat.Math.Pose3d? sensorPose = null)
			{
				try
				{
					var sensorTransform = sensorObject.transform;
					sensorTransform.position = UE.Vector3.zero;
					sensorTransform.rotation = UE.Quaternion.identity;
					sensorTransform.SetParent(targetObject.transform, false);
					sensorTransform.localScale = UE.Vector3.one;
					sensorTransform.localPosition = sensorPose?.ToUnityPosition() ?? UE.Vector3.zero;
					sensorTransform.localRotation = sensorPose?.ToUnityRotation() ?? UE.Quaternion.identity;
				}
				catch
				{
					UE.Debug.Log("sensorObject is null or Invalid obejct exist");
				}
			}

			public static Device AddLidar(this UE.GameObject targetObject, in SDFormat.LidarSensor element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var lidar = newSensorObject.AddComponent<SensorDevices.Lidar>();
				lidar.DeviceName = newSensorObject.GetFrameName();
				lidar.ScanRange = new MathUtil.MinMax(element.RangeMin, element.RangeMax);
				lidar.Resolution = new SensorDevices.LaserData.Resolution((float)element.RangeResolution);

				lidar.Horizontal = new SensorDevices.LaserData.Scan(
					(uint)element.HorizontalScanSamples,
					element.HorizontalScanMinAngle.Radians,
					element.HorizontalScanMaxAngle.Radians,
					element.HorizontalScanResolution);

				lidar.Vertical = new SensorDevices.LaserData.Scan(
					(uint)element.VerticalScanSamples,
					element.VerticalScanMinAngle.Radians,
					element.VerticalScanMaxAngle.Radians,
					element.VerticalScanResolution);

				lidar.SetupNoise(element.RangeNoise);

				return lidar;
			}

			public static Device AddCamera(this UE.GameObject targetObject, in SDFormat.CameraSensor element, SDFormat.Math.Pose3d? sensorPose = null)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, sensorPose);

				var camera = newSensorObject.AddComponent<SensorDevices.Camera>();
				camera.tag = "Sensor";
				camera.DeviceName = newSensorObject.GetFrameName();
				camera.SetParameter(element);

				camera.SetupNoise(element.ImageNoise);

				return camera;
			}

			public static Device AddSegmentationCamera(this UE.GameObject targetObject, in SDFormat.CameraSensor element, SDFormat.Math.Pose3d? sensorPose = null)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, sensorPose);

				var camera = newSensorObject.AddComponent<SensorDevices.SegmentationCamera>();
				camera.DeviceName = newSensorObject.GetFrameName();

				camera.SetParameter(element);

				camera.SetupNoise(element.ImageNoise);

				return camera;
			}

			public static Device AddDepthCamera(this UE.GameObject targetObject, in SDFormat.CameraSensor element, SDFormat.Math.Pose3d? sensorPose = null)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, sensorPose);

				var camera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
				camera.DeviceName = newSensorObject.GetFrameName();

				camera.SetParameter(element);

				camera.SetupNoise(element.ImageNoise);

				return camera;
			}

			public static Device AddMultiCamera(this UE.GameObject targetObject, in System.Collections.Generic.List<SDFormat.CameraSensor> cameras)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var multicamera = newSensorObject.AddComponent<SensorDevices.MultiCamera>();
				multicamera.DeviceName = newSensorObject.GetFrameName();
				if (cameras != null)
				{
					foreach (var camParam in cameras)
					{
						var newCam = AddCamera(newSensorObject, camParam);
						newCam.name = camParam.Name;
						newCam.Mode = Device.ModeType.NONE;
						newCam.DeviceName = multicamera.DeviceName + "::" + newCam.name;
						multicamera.AddCamera((SensorDevices.Camera)newCam);
					}
				}

				return multicamera;
			}

			public static Device AddSonar(this UE.GameObject targetObject, in SDFormat.SonarSensor element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var sonar = newSensorObject.AddComponent<SensorDevices.Sonar>();
				sonar.DeviceName = newSensorObject.GetFrameName();
				sonar.Geometry = element.Geometry;
				sonar.RangeMin = element.Min;
				sonar.RangeMax = element.Max;
				sonar.Radius = element.Radius;

				return sonar;
			}

			public static Device AddImu(this UE.GameObject targetObject, in SDFormat.ImuSensor element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var imu = newSensorObject.AddComponent<SensorDevices.IMU>();
				imu.DeviceName = newSensorObject.GetFrameName();

				imu.SetupNoises(element);

				return imu;
			}

			public static Device AddNavSat(this UE.GameObject targetObject, in SDFormat.NavSatSensor element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var gps = newSensorObject.AddComponent<SensorDevices.GPS>();
				gps.DeviceName = newSensorObject.GetFrameName();
				gps.SetupNoises(element);

				return gps;
			}

			public static Device AddContact(this UE.GameObject targetObject, in ContactData element)
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