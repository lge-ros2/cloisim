/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
#if UNITY_EDITOR
using SceneVisibilityManager = UnityEditor.SceneVisibilityManager;
#endif

public partial class SDFImporter : SDF.Importer
{
	protected override System.Object ImportSensor(in SDF.Sensor item, in System.Object parentObject)
	{
		// Console.WriteLine("[Sensor] {0}", item.Name);
		var targetObject = (parentObject as GameObject);

		Device sensor = null;

		var sensorType = item.Type;

		switch (sensorType)
		{
			case "lidar":
			case "ray":
			case "gpu_ray":
				var ray = item.GetSensor() as SDF.Ray;
				sensor = SDFImplement.Sensor.AddLidar(ray, targetObject);
				break;

			case "depth":
				var depthCamera = item.GetSensor() as SDF.Camera;
				sensor = SDFImplement.Sensor.AddDepthCamera(depthCamera, targetObject);
				break;

			case "camera":
				var camera = item.GetSensor() as SDF.Camera;
				sensor = SDFImplement.Sensor.AddCamera(camera, targetObject);
				break;

			case "multicamera":
				var cameras = item.GetSensor() as SDF.Cameras;
				sensor = SDFImplement.Sensor.AddMultiCamera(cameras, targetObject);
				break;

			case "imu":
				var imu = item.GetSensor() as SDF.IMU;
				sensor = SDFImplement.Sensor.AddImu(imu, targetObject);
				break;

			case "sonar":
				var sonar = item.GetSensor() as SDF.Sonar;
				sensor = SDFImplement.Sensor.AddSonar(sonar, targetObject);
				break;

			case "gps":
				var gps = item.GetSensor() as SDF.GPS;
				sensor = SDFImplement.Sensor.AddGps(gps, targetObject);
				break;

			case "contact":
				var contact = item.GetSensor() as SDF.Contact;
				sensor = SDFImplement.Sensor.AddContact(contact, targetObject);
				break;

			case "air_pressure":
			case "altimeter":
			case "force_torque":
			case "logical_camera":
			case "magnetometer":
			case "rfidtag":
			case "rfid":
			case "transceiver":
				Console.WriteLine("[Sensor] Not supported sensor name({0}) type({1})!!!!!", item.Name, sensorType);
				break;

			default:
				Debug.LogWarningFormat("[Sensor] type({0}) is not supprted.", sensorType);
				break;
		}

		GameObject newSensorObject = null;

		if (sensor)
		{
			sensor.SetUpdateRate((float)item.UpdateRate());
			sensor.EnableVisualize = item.Visualize();
			newSensorObject = sensor.gameObject;

			if (newSensorObject != null)
			{
				newSensorObject.tag = "Sensor";
				newSensorObject.name = item.Name;
				newSensorObject.transform.localPosition += SDF2Unity.GetPosition(item.Pose.Pos);
                newSensorObject.transform.localRotation *= SDF2Unity.GetRotation(item.Pose.Rot);
#if UNITY_EDITOR
				SceneVisibilityManager.instance.ToggleVisibility(newSensorObject, true);
				SceneVisibilityManager.instance.DisablePicking(newSensorObject, true);
#endif
			}
		}

		return (newSensorObject as System.Object);
	}
}