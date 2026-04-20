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

namespace SDFormat
{
	using Implement;
	namespace Import
	{
		public partial class Loader : Base
		{
			protected override System.Object ImportSensor(in Sensor sensor, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);

				Device device = null;

				var sensorType = sensor.TypeStr;

				switch (sensorType)
				{
					case "ray":
						Debug.LogWarning("[Import] It is preferred to use 'lidar' since 'ray' will be deprecated.");
						goto case "lidar";

					case "lidar":
						Debug.LogWarning("[Import] CPU based lidar or ray does not support. It will change to GPU based sensor.");
						goto case "gpu_lidar";

					case "gpu_ray":
						Debug.LogWarning("[Import] It is preferred to use 'gpu_lidar' since 'gpu_ray' will be deprecated.");
						goto case "gpu_lidar";

					case "gpu_lidar":
						var lidar = sensor.Lidar;
						device = targetObject.AddLidar(lidar);
						break;

					case "depth_camera":
					case "depth":
						var depthCamera = sensor.Camera;
						device = targetObject.AddDepthCamera(depthCamera, sensor.RawPose);
						break;

					case "camera":
						var camera = sensor.Camera;
						device = targetObject.AddCamera(camera, sensor.RawPose);
						break;

					case "segmentation_camera":
					case "segmentation":
						var segmentationCamera = sensor.Camera;
						device = targetObject.AddSegmentationCamera(segmentationCamera, sensor.RawPose);
						break;

					case "rgbd_camera":
					case "rgbd":
					case "multicamera":
						var cameras = sensor.GetCameras();
						device = targetObject.AddMultiCamera(cameras);
						break;

					case "imu":
						var imu = sensor.Imu;
						device = targetObject.AddImu(imu);
						break;

					case "sonar":
						var sonar = sensor.Sonar;
						device = targetObject.AddSonar(sonar);
						break;

					case "gps":
						Debug.LogWarning("[Import] It is preferred to use 'navsat' since 'gps' will be deprecated.");
						goto case "navsat";

					case "navsat":
						var navsat = sensor.NavSat;
						device = targetObject.AddNavSat(navsat);
						break;

					case "contact":
						var contact = sensor.GetContactData();
						device = targetObject.AddContact(contact);
						break;

					case "air_pressure":
					case "air_speed":
					case "altimeter":
					case "force_torque":
					case "logical_camera":
					case "thermal_camera":
					case "bounding_box_camera":
					case "wide_angle_camera":
					case "magnetometer":
					case "rfid":
					case "rfidtag":
					case "wireless_receiver":
					case "wireless_transmitter":
					case "custom":
						Debug.LogWarningFormat("[Sensor] Not supported sensor name({0}) type({1})!!!!!", sensor.Name, sensorType);
						break;

					default:
						Debug.LogWarningFormat("[Sensor] type({0}) is not supprted.", sensorType);
						break;
				}

				if (device)
				{
					device.SetUpdateRate((float)sensor.UpdateRate);
					device.EnableVisualize = sensor.Visualize();

					var newSensorObject = device.gameObject;

					if (newSensorObject != null)
					{
						var (localPosition, localRotation) = sensor.RawPose.ToUnity();

						newSensorObject.tag = "Sensor";
						newSensorObject.name = sensor.Name;
						newSensorObject.transform.localPosition += localPosition;
						newSensorObject.transform.localRotation *= localRotation;
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