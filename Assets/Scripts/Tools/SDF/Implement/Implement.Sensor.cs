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

			public static Device AddLidar(in SDF.Lidar element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var lidar = newSensorObject.AddComponent<SensorDevices.Lidar>();

				lidar.DeviceName = GetFrameName(newSensorObject);
				lidar.range = new SensorDevices.LaserData.MinMax(element.range.min, element.range.max);
				var horizontal = element.horizontal;
				lidar.horizontal = new SensorDevices.LaserData.Scan(horizontal.samples, horizontal.min_angle, horizontal.max_angle, horizontal.resolution);

				var vertical = element.vertical;
				if (vertical != null)
				{
					lidar.vertical = new SensorDevices.LaserData.Scan(vertical.samples, vertical.min_angle, vertical.max_angle, vertical.resolution);
				}

				if (element.noise != null)
				{
					lidar.noise = new SensorDevices.Noise(element.noise, "lidar");
				}

				return lidar;
			}

			public static Device AddCamera(in SDF.Camera element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject, element.Pose);

				var camera = newSensorObject.AddComponent<SensorDevices.Camera>();
				camera.DeviceName = GetFrameName(newSensorObject);
				camera.SetCamParameter(element);

				if (element.noise != null)
				{
					camera.noise = new SensorDevices.Noise(element.noise, "camera");
				}

				return camera;
			}

			public static Device AddDepthCamera(in SDF.Camera element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject, element.Pose);

				var depthCamera = newSensorObject.AddComponent<SensorDevices.DepthCamera>();
				depthCamera.DeviceName = GetFrameName(newSensorObject);

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

				depthCamera.SetCamParameter(element);

				if (element.noise != null)
				{
					depthCamera.noise = new SensorDevices.Noise(element.noise, "depthcamera");
				}

				return depthCamera;
			}

			public static Device AddMultiCamera(in SDF.Cameras element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var multicamera = newSensorObject.AddComponent<SensorDevices.MultiCamera>();
				multicamera.DeviceName = GetFrameName(newSensorObject);

				foreach (var camParam in element.cameras)
				{
					var newCamObject = new GameObject();
					newCamObject.name = camParam.name;

					AttachSensor(newCamObject, newSensorObject, camParam.Pose);

					var newCam = newCamObject.AddComponent<SensorDevices.Camera>();
					newCam.Mode = Device.ModeType.NONE;
					newCam.DeviceName = "MultiCamera::" + newCamObject.name;
					newCam.SetCamParameter(camParam);

					if (camParam.noise != null)
					{
						newCam.noise = new SensorDevices.Noise(camParam.noise, "multicamera");
					}

					multicamera.AddCamera(newCam);
				}

				return multicamera;
			}

			public static Device AddSonar(in SDF.Sonar element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var sonar = newSensorObject.AddComponent<SensorDevices.Sonar>();
				sonar.DeviceName = GetFrameName(newSensorObject);
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
				imu.DeviceName = GetFrameName(newSensorObject);

				if (element.angular_velocity_x_noise != null)
				{
					imu.angular_velocity_noises["x"] = new SensorDevices.Noise(element.angular_velocity_x_noise, "imu");
				}

				if (element.angular_velocity_y_noise != null)
				{
					imu.angular_velocity_noises["y"] = new SensorDevices.Noise(element.angular_velocity_y_noise, "imu");
				}

				if (element.angular_velocity_z_noise != null)
				{
					imu.angular_velocity_noises["z"] = new SensorDevices.Noise(element.angular_velocity_z_noise, "imu");
				}

				if (element.linear_acceleration_x_noise != null)
				{
					imu.linear_acceleration_noises["x"] = new SensorDevices.Noise(element.linear_acceleration_x_noise, "imu");
				}

				if (element.linear_acceleration_y_noise != null)
				{
					imu.linear_acceleration_noises["y"] = new SensorDevices.Noise(element.linear_acceleration_y_noise, "imu");
				}

				if (element.linear_acceleration_z_noise != null)
				{
					imu.linear_acceleration_noises["z"] = new SensorDevices.Noise(element.linear_acceleration_z_noise, "imu");
				}

				return imu;
			}

			public static Device AddGps(in SDF.GPS element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var gps = newSensorObject.AddComponent<SensorDevices.GPS>();
				gps.DeviceName = GetFrameName(newSensorObject);

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

				return gps;
			}

			public static Device AddContact(in SDF.Contact element, in GameObject targetObject)
			{
				var newSensorObject = new GameObject();
				AttachSensor(newSensorObject, targetObject);

				var contact = newSensorObject.AddComponent<SensorDevices.Contact>();
				contact.DeviceName = GetFrameName(newSensorObject);
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