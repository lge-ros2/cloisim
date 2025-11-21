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
	using Implement;
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
						Debug.LogWarning("[SDF.Import] It is preferred to use 'lidar' since 'ray' will be deprecated.");
						goto case "lidar";

					case "lidar":
						Debug.LogWarning("[SDF.Import] CPU based lidar or ray does not support. It will change to GPU based sensor.");
						goto case "gpu_lidar";

					case "gpu_ray":
						Debug.LogWarning("[SDF.Import] It is preferred to use 'gpu_lidar' since 'gpu_ray' will be deprecated.");
						goto case "gpu_lidar";

					case "gpu_lidar":
						var lidar = sensor.GetSensor() as SDF.Lidar;
						device = targetObject.AddLidar(lidar);
						break;

					case "depth_camera":
					case "depth":
						var depthCamera = sensor.GetSensor() as SDF.Camera;
						device = targetObject.AddDepthCamera(depthCamera);
						break;

					case "camera":
						var camera = sensor.GetSensor() as SDF.Camera;
						device = targetObject.AddCamera(camera);
						break;

					case "segmentation_camera":
					case "segmentation":
						var segmentationCamera = sensor.GetSensor() as SDF.Camera;
						device = targetObject.AddSegmentationCamera(segmentationCamera);
						break;

					case "rgbd_camera":
					case "rgbd":
					case "multicamera":
						var cameras = sensor.GetSensor() as SDF.Cameras;
						device = targetObject.AddMultiCamera(cameras);
						break;

					case "imu":
						var imu = sensor.GetSensor() as SDF.IMU;
						device = targetObject.AddImu(imu);
						break;

					case "sonar":
						var sonar = sensor.GetSensor() as SDF.Sonar;
						device = targetObject.AddSonar(sonar);
						break;

					case "gps":
						Debug.LogWarning("[SDF.Import] It is preferred to use 'navsat' since 'gps' will be deprecated.");
						goto case "navsat";

					case "navsat":
						var navsat = sensor.GetSensor() as SDF.NavSat;
						device = targetObject.AddNavSat(navsat);
						break;

					case "contact":
						var contact = sensor.GetSensor() as SDF.Contact;
						device = targetObject.AddContact(contact);
						break;

					case "air_pressure":
					case "altimeter":
					case "force_torque":
					case "logical_camera":
					case "thermal_camera":
					case "magnetometer":
					case "rfid":
					case "rfidtag":
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
						newSensorObject.transform.localPosition += SDF2Unity.Position(sensor.Pose?.Pos);
						newSensorObject.transform.localRotation *= SDF2Unity.Rotation(sensor.Pose?.Rot);
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