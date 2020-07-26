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

		if (sensorType.Equals("lidar") || sensorType.Equals("ray") || sensorType.Equals("gpu_ray"))
		{
			var ray = item.GetSensor() as SDF.Ray;
			sensor = SDFImplement.Sensor.AddLidar(ray, targetObject);
		}
		else if (sensorType.Equals("depth"))
		{
			var depthCamera = item.GetSensor() as SDF.Camera;
			sensor = SDFImplement.Sensor.AddDepthCamera(depthCamera, targetObject);
		}
		else if (sensorType.Equals("camera"))
		{
			var camera = item.GetSensor() as SDF.Camera;
			sensor = SDFImplement.Sensor.AddCamera(camera, targetObject);
		}
		else if (sensorType.Equals("multicamera"))
		{
			var cameras = item.GetSensor() as SDF.Cameras;
			sensor = SDFImplement.Sensor.AddMultiCamera(cameras, targetObject);
		}
		else if (sensorType.Equals("imu"))
		{
			var imu = item.GetSensor() as SDF.IMU;
			sensor = SDFImplement.Sensor.AddImu(imu, targetObject);
		}
		else if (sensorType.Equals("sonar"))
		{
			var sonar = item.GetSensor() as SDF.Sonar;
			sensor = SDFImplement.Sensor.AddSonar(sonar, targetObject);
		}
		else if (sensorType.Equals("gps"))
		{
			var gps = item.GetSensor() as SDF.GPS;
			sensor = SDFImplement.Sensor.AddGps(gps, targetObject);
		}
		else if (sensorType.Equals("air_pressure") ||
				 sensorType.Equals("altimeter") || sensorType.Equals("contact") ||
				 sensorType.Equals("force_torque") ||
				 sensorType.Equals("logical_camera") || sensorType.Equals("magnetometer") ||
				 sensorType.Equals("rfidtag") ||
				 sensorType.Equals("rfid") || sensorType.Equals("transceiver"))
		{
			Console.WriteLine("[Sensor] Not supported sensor name({0}) type({1})!!!!!", item.Name, sensorType);
		}
		else
		{
			Debug.LogWarningFormat("[Sensor] type({0}) is not supprted.", sensorType);
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
				SDFImplement.Sensor.TransformSensor(newSensorObject, item.Pose);
#if UNITY_EDITOR
				SceneVisibilityManager.instance.ToggleVisibility(newSensorObject, true);
				SceneVisibilityManager.instance.DisablePicking(newSensorObject, true);
#endif
			}
		}

		return (newSensorObject as System.Object);
	}
}