/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections;
using messages = cloisim.msgs;
using Any = cloisim.msgs.Any;

using cloisim.Native;
using System.Runtime.InteropServices;
using System;

public class LaserPlugin : CLOiSimPlugin
{
	private SensorDevices.Lidar _lidar = null;
	private IntPtr _rosNode = IntPtr.Zero;
	private IntPtr _rosPublisher = IntPtr.Zero;
	private IntPtr _rosPublisherPC2 = IntPtr.Zero;
	private string _outputType = "LaserScan";

	protected override void OnAwake()
	{
		_type = ICLOiSimPlugin.Type.LASER;
		_lidar = GetComponent<SensorDevices.Lidar>();
	}

	protected override IEnumerator OnStart()
	{
		if (GetPluginParameters().IsValidNode("filter"))
		{
			var useIntensity = GetPluginParameters().GetValue<bool>("intensity");

			if (GetPluginParameters().IsValidNode("filter/angle/horizontal"))
			{
				var filterAngleLower = GetPluginParameters().GetValue<double>("filter/angle/horizontal/lower", double.NegativeInfinity);
				var filterAngleUpper = GetPluginParameters().GetValue<double>("filter/angle/horizontal/upper", double.PositiveInfinity);
				_lidar.SetupLaserAngleFilter(filterAngleLower, filterAngleUpper, useIntensity);
			}

			if (GetPluginParameters().IsValidNode("filter/range"))
			{
				var filterRangeMin = GetPluginParameters().GetValue<double>("filter/range/min", double.NegativeInfinity);
				var filterRangeMax = GetPluginParameters().GetValue<double>("filter/range/max", double.PositiveInfinity);
				_lidar.SetupLaserRangeFilter(filterRangeMin, filterRangeMax, useIntensity);
			}
		}

		if (GetPluginParameters().IsValidNode("custom_noise"))
		{
			var customNoiseInRawXml = GetPluginParameters().GetValue<string>("custom_noise");
			_lidar.SetupCustomNoise(customNoiseInRawXml);
		}
		
		_outputType = GetPluginParameters().GetValue<string>("output_type", "LaserScan");
		
		yield return null;

		// Initialize ROS2 Native Plugin
		Ros2NativeWrapper.InitROS2(0, IntPtr.Zero);
		var nodeName = "cloisim_laser_" + gameObject.name.Replace(" ", "_");
		_rosNode = Ros2NativeWrapper.CreateNode(nodeName);
		
		var topicName = GetPluginParameters().GetValue<string>("topic", "/scan");

		if (_outputType == "LaserScan")
		{
			_rosPublisher = Ros2NativeWrapper.CreateLaserScanPublisher(_rosNode, topicName);
		}
		else if (_outputType == "PointCloud2")
		{
			_rosPublisherPC2 = Ros2NativeWrapper.CreatePointCloud2Publisher(_rosNode, topicName);
		}
		
		_lidar.OnLidarDataGenerated += HandleNativeLidarData;

		if (RegisterServiceDevice(out var portService, "Info"))
		{
			AddThread(portService, ServiceThread);
		}

		if (RegisterTxDevice(out var portTx, "Data"))
		{
			AddThread(portTx, SenderThread, _lidar);
		}

		yield return null;
	}

	private unsafe void HandleNativeLidarData(messages.LaserScanStamped laserScanStamped)
	{
		var scan = laserScanStamped.Scan;
		double timestamp = laserScanStamped.Time.Sec + (laserScanStamped.Time.Nsec * 1e-9);

		if (_outputType == "LaserScan" && _rosPublisher != IntPtr.Zero)
		{
			var data = new LaserScanStruct
			{
				timestamp = timestamp,
				frame_id = scan.Frame,
				angle_min = (float)scan.AngleMin,
				angle_max = (float)scan.AngleMax,
				angle_increment = (float)scan.AngleStep,
				time_increment = 0f,
				scan_time = 0f,
				range_min = (float)scan.RangeMin,
				range_max = (float)scan.RangeMax,
				ranges_length = scan.Ranges.Length,
				intensities_length = scan.Intensities.Length
			};

			// Convert double[] ranges to float[] inside unmanaged memory
			var floatRanges = new float[data.ranges_length];
			for (int i = 0; i < data.ranges_length; i++)
				floatRanges[i] = (float)scan.Ranges[i];

			var floatIntensities = new float[data.intensities_length];
			for (int i = 0; i < data.intensities_length; i++)
				floatIntensities[i] = (float)scan.Intensities[i];

			data.ranges = Marshal.AllocHGlobal(data.ranges_length * sizeof(float));
			Marshal.Copy(floatRanges, 0, data.ranges, data.ranges_length);

			if (data.intensities_length > 0)
			{
				data.intensities = Marshal.AllocHGlobal(data.intensities_length * sizeof(float));
				Marshal.Copy(floatIntensities, 0, data.intensities, data.intensities_length);
			}
			else
			{
				data.intensities = IntPtr.Zero;
			}

			Ros2NativeWrapper.PublishLaserScan(_rosPublisher, ref data);

			Marshal.FreeHGlobal(data.ranges);
			if (data.intensities != IntPtr.Zero)
			{
				Marshal.FreeHGlobal(data.intensities);
			}
		}
		else if (_outputType == "PointCloud2" && _rosPublisherPC2 != IntPtr.Zero)
		{
			// Convert LaserScan to PointCloud2
			int numSamples = (int)(scan.Count * scan.VerticalCount);
			int pointsCount = 0;
			
			// We need x, y, z, intensity (16 bytes per point)
			uint pointStep = 16;
			
			// Pre-calculate points to determine exact size
			var points = new float[numSamples * 4];
			
			for (int v = 0; v < scan.VerticalCount; v++)
			{
				double verticalAngle = scan.VerticalAngleMin + v * scan.VerticalAngleStep;
				double cosVertical = Math.Cos(verticalAngle);
				double sinVertical = Math.Sin(verticalAngle);

				for (int h = 0; h < scan.Count; h++)
				{
					int index = v * (int)scan.Count + h;
					double range = scan.Ranges[index];
					double intensity = scan.Intensities.Length > index ? scan.Intensities[index] : 0.0;
					
					// Ignore invalid ranges
					if (double.IsNaN(range) || double.IsInfinity(range) || range < scan.RangeMin || range > scan.RangeMax)
					{
						continue;
					}

					double horizontalAngle = scan.AngleMin + h * scan.AngleStep;
					
					float x = (float)(range * Math.Cos(horizontalAngle) * cosVertical);
					float y = (float)(range * Math.Sin(horizontalAngle) * cosVertical);
					float z = (float)(range * sinVertical);
					
					points[pointsCount * 4 + 0] = x;
					points[pointsCount * 4 + 1] = y;
					points[pointsCount * 4 + 2] = z;
					points[pointsCount * 4 + 3] = (float)intensity;
					
					pointsCount++;
				}
			}

			if (pointsCount == 0) return;

			// Define fields
			var fields = new PointFieldStruct[4];
			fields[0] = new PointFieldStruct { name = "x", offset = 0, datatype = 7 /* FLOAT32 */, count = 1 };
			fields[1] = new PointFieldStruct { name = "y", offset = 4, datatype = 7, count = 1 };
			fields[2] = new PointFieldStruct { name = "z", offset = 8, datatype = 7, count = 1 };
			fields[3] = new PointFieldStruct { name = "intensity", offset = 12, datatype = 7, count = 1 };

			IntPtr fieldsPtr = Marshal.AllocHGlobal(4 * Marshal.SizeOf<PointFieldStruct>());
			for (int i = 0; i < 4; i++)
			{
				IntPtr ptr = new IntPtr(fieldsPtr.ToInt64() + i * Marshal.SizeOf<PointFieldStruct>());
				Marshal.StructureToPtr(fields[i], ptr, false);
			}

			uint dataSize = (uint)(pointsCount * pointStep);
			IntPtr dataPtr = Marshal.AllocHGlobal((int)dataSize);
			Marshal.Copy(points, 0, dataPtr, pointsCount * 4);

			var pc2Data = new PointCloud2Struct
			{
				timestamp = timestamp,
				frame_id = scan.Frame,
				height = 1,
				width = (uint)pointsCount,
				fields = fieldsPtr,
				fields_length = 4,
				is_bigendian = 0,
				point_step = pointStep,
				row_step = dataSize,
				data = dataPtr,
				data_length = dataSize,
				is_dense = 1
			};

			Ros2NativeWrapper.PublishPointCloud2(_rosPublisherPC2, ref pc2Data);

			for (int i = 0; i < 4; i++)
			{
				IntPtr ptr = new IntPtr(fieldsPtr.ToInt64() + i * Marshal.SizeOf<PointFieldStruct>());
				Marshal.DestroyStructure<PointFieldStruct>(ptr);
			}
			Marshal.FreeHGlobal(fieldsPtr);
			Marshal.FreeHGlobal(dataPtr);
		}
	}

	new protected void OnDestroy()
	{
		if (_lidar != null) _lidar.OnLidarDataGenerated -= HandleNativeLidarData;
		if (_rosPublisher != IntPtr.Zero) Ros2NativeWrapper.DestroyLaserScanPublisher(_rosPublisher);
		if (_rosPublisherPC2 != IntPtr.Zero) Ros2NativeWrapper.DestroyPointCloud2Publisher(_rosPublisherPC2);
		if (_rosNode != IntPtr.Zero) Ros2NativeWrapper.DestroyNode(_rosNode);
	}

	protected override void HandleCustomRequestMessage(in string requestType, in Any requestValue, ref DeviceMessage response)
	{
		switch (requestType)
		{
			case "request_output_type":
				SetOutputTypeResponse(ref response);
				break;

			case "request_transform":
				var devicePose = _lidar.GetPose();
				var deviceName = _lidar.DeviceName;
				SetTransformInfoResponse(ref response, deviceName, devicePose, _parentLinkName);
				break;

			default:
				break;
		}
	}

	private void SetOutputTypeResponse(ref DeviceMessage msInfo)
	{
		var output_type = GetPluginParameters().GetValue<string>("output_type", "LaserScan");
		var outputTypeInfo = new messages.Param();
		outputTypeInfo.Name = "output_type";
		outputTypeInfo.Value = new Any { Type = Any.ValueType.String, StringValue = output_type };

		msInfo.SetMessage<messages.Param>(outputTypeInfo);
	}
}