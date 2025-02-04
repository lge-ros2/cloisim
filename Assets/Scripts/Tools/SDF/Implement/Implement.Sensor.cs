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
				var horizontal = element.horizontal;
				lidar.horizontal = new SensorDevices.LaserData.Scan(horizontal.samples, horizontal.min_angle, horizontal.max_angle, horizontal.resolution);

				var vertical = element.vertical;
				if (vertical != null)
				{
					lidar.vertical = new SensorDevices.LaserData.Scan(vertical.samples, vertical.min_angle, vertical.max_angle, vertical.resolution);
				}

				if (element.noise != null)
				{
					lidar.SetupNoise(element.noise);
				}

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

				if (element.noise != null)
				{
					camera.noise = new SensorDevices.Noise(element.noise, element.type);
				}

				return camera;
			}

			public static Device AddSegmentaionCamera(this UE.GameObject targetObject, in SDF.Camera element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, element.Pose);

				var segmentationCamera = newSensorObject.AddComponent<SensorDevices.SegmentationCamera>();
				segmentationCamera.DeviceName = newSensorObject.GetFrameName();

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

				segmentationCamera.SetParameter(element);

				if (element.noise != null)
				{
					segmentationCamera.noise = new SensorDevices.Noise(element.noise, element.type);
				}

				return segmentationCamera;
			}

			public static Device AddDepthCamera(this UE.GameObject targetObject, in SDF.Camera element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject, element.Pose);

				var depthCamera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
				depthCamera.DeviceName = newSensorObject.GetFrameName();

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

				depthCamera.SetParameter(element);

				if (element.noise != null)
				{
					depthCamera.noise = new SensorDevices.Noise(element.noise, element.type);
				}

				return depthCamera;
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

				if (element != null)
				{
					if (element.noise_angular_velocity.x != null)
					{
						imu.angular_velocity_noises["x"] = new SensorDevices.Noise(element.noise_angular_velocity.x, "imu");
					}

					if (element.noise_angular_velocity.y != null)
					{
						imu.angular_velocity_noises["y"] = new SensorDevices.Noise(element.noise_angular_velocity.y, "imu");
					}

					if (element.noise_angular_velocity.z != null)
					{
						imu.angular_velocity_noises["z"] = new SensorDevices.Noise(element.noise_angular_velocity.z, "imu");
					}

					if (element.noise_linear_acceleration.x != null)
					{
						imu.linear_acceleration_noises["x"] = new SensorDevices.Noise(element.noise_linear_acceleration.x, "imu");
					}

					if (element.noise_linear_acceleration.y != null)
					{
						imu.linear_acceleration_noises["y"] = new SensorDevices.Noise(element.noise_linear_acceleration.y, "imu");
					}

					if (element.noise_linear_acceleration.z != null)
					{
						imu.linear_acceleration_noises["z"] = new SensorDevices.Noise(element.noise_linear_acceleration.z, "imu");
					}
				}

				return imu;
			}

			public static Device AddNavSat(this UE.GameObject targetObject, in SDF.NavSat element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var gps = newSensorObject.AddComponent<SensorDevices.GPS>();
				gps.DeviceName = newSensorObject.GetFrameName();

				if (element != null)
				{
					if (element.position_sensing.horizontal_noise != null)
					{
						gps.position_sensing_noises["horizontal"] = new SensorDevices.Noise(element.position_sensing.horizontal_noise, "gps");
					}

					if (element.position_sensing.vertical_noise != null)
					{
						gps.position_sensing_noises["vertical"] = new SensorDevices.Noise(element.position_sensing.vertical_noise, "gps");
					}

					if (element.velocity_sensing.horizontal_noise != null)
					{
						gps.velocity_sensing_noises["horizontal"] = new SensorDevices.Noise(element.velocity_sensing.horizontal_noise, "gps");
					}

					if (element.velocity_sensing.vertical_noise != null)
					{
						gps.velocity_sensing_noises["vertical"] = new SensorDevices.Noise(element.velocity_sensing.vertical_noise, "gps");
					}
				}

				return gps;
			}


			public static Device AddContact(this UE.GameObject targetObject, in SDF.Contact element)
			{
				var newSensorObject = new UE.GameObject();
				targetObject.AttachSensor(newSensorObject);

				var contact = newSensorObject.AddComponent<SensorDevices.Contact>();
				contact.DeviceName = newSensorObject.GetFrameName();
				contact.targetCollision = element.collision;
				contact.topic = element.topic;

				var contactTrigger = targetObject.AddComponent<SensorDevices.ContactTrigger>();
				contactTrigger.collisionStay = contact.CollisionStay;
				contactTrigger.collisionExit = contact.CollisionExit;

				return contact;
			}
		}
	}
}