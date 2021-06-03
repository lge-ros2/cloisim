/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportSensor(in SDF.Sensor sensor, in System.Object parentObject)
			{
				// Console.WriteLine("[Sensor] {0}", item.Name);
				var targetObject = (parentObject as UE.GameObject);

				Device device = null;

				var sensorType = sensor.Type;

				switch (sensorType)
				{
					case "ray":
						Debug.LogWarning("It is preferred to use 'lidar' since 'ray' will be deprecated.");
						goto case "lidar";

					case "lidar":
						Debug.LogWarning("CPU based lidar or ray does not support. It will change to GPU based sensor.");
						goto case "gpu_lidar";

					case "gpu_ray":
						Debug.LogWarning("It is preferred to use 'gpu_lidar' since 'gpu_ray' will be deprecated.");
						goto case "gpu_lidar";

					case "gpu_lidar":
						var ray = sensor.GetSensor() as SDF.Lidar;
						device = Implement.Sensor.AddLidar(ray, targetObject);
						break;

					case "depth":
						var depthCamera = sensor.GetSensor() as SDF.Camera;
						device = Implement.Sensor.AddDepthCamera(depthCamera, targetObject);
						break;

					case "camera":
						var camera = sensor.GetSensor() as SDF.Camera;
						device = Implement.Sensor.AddCamera(camera, targetObject);
						break;

					case "multicamera":
						var cameras = sensor.GetSensor() as SDF.Cameras;
						device = Implement.Sensor.AddMultiCamera(cameras, targetObject);
						break;

					case "imu":
						var imu = sensor.GetSensor() as SDF.IMU;
						device = Implement.Sensor.AddImu(imu, targetObject);
						break;

					case "sonar":
						var sonar = sensor.GetSensor() as SDF.Sonar;
						device = Implement.Sensor.AddSonar(sonar, targetObject);
						break;

					case "gps":
						var gps = sensor.GetSensor() as SDF.GPS;
						device = Implement.Sensor.AddGps(gps, targetObject);
						break;

					case "contact":
						var contact = sensor.GetSensor() as SDF.Contact;
						device = Implement.Sensor.AddContact(contact, targetObject);
						break;

					case "air_pressure":
					case "altimeter":
					case "force_torque":
					case "logical_camera":
					case "magnetometer":
					case "rfid":
					case "rfidtag":
					case "rgbd_camera":
					case "thermal_camera":
					case "wireless_receiver":
					case "wireless_transmitter":
						Debug.LogWarningFormat("[Sensor] Not supported sensor name({0}) type({1})!!!!!", sensor.Name, sensorType);
						break;

					default:
						Debug.LogWarningFormat("[Sensor] type({0}) is not supprted.", sensorType);
						break;
				}

				if (device)
				{
					device.SetUpdateRate((float)sensor.UpdateRate());
					device.EnableVisualize = sensor.Visualize();

					var newSensorObject = device.gameObject;

					if (newSensorObject != null)
					{
						newSensorObject.tag = "Sensor";
						newSensorObject.name = sensor.Name;
						newSensorObject.transform.localPosition += SDF2Unity.GetPosition(sensor.Pose.Pos);
						newSensorObject.transform.localRotation *= SDF2Unity.GetRotation(sensor.Pose.Rot);
#if UNITY_EDITOR
						SceneVisibilityManager.instance.ToggleVisibility(newSensorObject, true);
						SceneVisibilityManager.instance.DisablePicking(newSensorObject, true);
#endif
						return (newSensorObject as System.Object);
					}
				}

				return null;
			}
		}
	}
}