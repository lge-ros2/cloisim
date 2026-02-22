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

	// ═══════════════════════════════════════════════
	//  P5: Pooled buffers — avoid AllocHGlobal/FreeHGlobal every callback
	// ═══════════════════════════════════════════════
	private float[] _pooledFloatRanges = null;
	private float[] _pooledFloatIntensities = null;
	private IntPtr _pooledRangesPtr = IntPtr.Zero;
	private IntPtr _pooledIntensitiesPtr = IntPtr.Zero;
	private int _pooledRangesCapacity = 0;
	private int _pooledIntensitiesCapacity = 0;

	// PointCloud2 pooled buffers
	private float[] _pooledPoints = null;
	private IntPtr _pooledFieldsPtr = IntPtr.Zero;
	private IntPtr _pooledDataPtr = IntPtr.Zero;
	private int _pooledPointsCapacity = 0;
	private int _pooledDataCapacity = 0;

	private void EnsureRangesBuffer(int length)
	{
		if (_pooledRangesCapacity < length)
		{
			if (_pooledRangesPtr != IntPtr.Zero) Marshal.FreeHGlobal(_pooledRangesPtr);
			_pooledRangesCapacity = length;
			_pooledFloatRanges = new float[length];
			_pooledRangesPtr = Marshal.AllocHGlobal(length * sizeof(float));
		}
	}

	private void EnsureIntensitiesBuffer(int length)
	{
		if (_pooledIntensitiesCapacity < length)
		{
			if (_pooledIntensitiesPtr != IntPtr.Zero) Marshal.FreeHGlobal(_pooledIntensitiesPtr);
			_pooledIntensitiesCapacity = length;
			_pooledFloatIntensities = new float[length];
			_pooledIntensitiesPtr = Marshal.AllocHGlobal(length * sizeof(float));
		}
	}

	private void EnsurePointsBuffer(int numSamples)
	{
		int needed = numSamples * 4;
		if (_pooledPointsCapacity < needed)
		{
			_pooledPointsCapacity = needed;
			_pooledPoints = new float[needed];
		}
	}

	private void EnsurePC2DataBuffer(int dataSize)
	{
		if (_pooledDataCapacity < dataSize)
		{
			if (_pooledDataPtr != IntPtr.Zero) Marshal.FreeHGlobal(_pooledDataPtr);
			_pooledDataCapacity = dataSize;
			_pooledDataPtr = Marshal.AllocHGlobal(dataSize);
		}
	}

	private void FreePooledBuffers()
	{
		if (_pooledRangesPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_pooledRangesPtr); _pooledRangesPtr = IntPtr.Zero; }
		if (_pooledIntensitiesPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_pooledIntensitiesPtr); _pooledIntensitiesPtr = IntPtr.Zero; }
		if (_pooledFieldsPtr != IntPtr.Zero)
		{
			for (int i = 0; i < 4; i++)
			{
				IntPtr ptr = new IntPtr(_pooledFieldsPtr.ToInt64() + i * Marshal.SizeOf<PointFieldStruct>());
				Marshal.DestroyStructure<PointFieldStruct>(ptr);
			}
			Marshal.FreeHGlobal(_pooledFieldsPtr);
			_pooledFieldsPtr = IntPtr.Zero;
		}
		if (_pooledDataPtr != IntPtr.Zero) { Marshal.FreeHGlobal(_pooledDataPtr); _pooledDataPtr = IntPtr.Zero; }
	}

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

			// P5: Reuse pooled buffers instead of AllocHGlobal/FreeHGlobal each frame
			EnsureRangesBuffer(data.ranges_length);
			for (int i = 0; i < data.ranges_length; i++)
				_pooledFloatRanges[i] = (float)scan.Ranges[i];
			Marshal.Copy(_pooledFloatRanges, 0, _pooledRangesPtr, data.ranges_length);
			data.ranges = _pooledRangesPtr;

			if (data.intensities_length > 0)
			{
				EnsureIntensitiesBuffer(data.intensities_length);
				for (int i = 0; i < data.intensities_length; i++)
					_pooledFloatIntensities[i] = (float)scan.Intensities[i];
				Marshal.Copy(_pooledFloatIntensities, 0, _pooledIntensitiesPtr, data.intensities_length);
				data.intensities = _pooledIntensitiesPtr;
			}
			else
			{
				data.intensities = IntPtr.Zero;
			}

			Ros2NativeWrapper.PublishLaserScan(_rosPublisher, ref data);
			// P5: No FreeHGlobal needed — buffers are reused
		}
		else if (_outputType == "PointCloud2" && _rosPublisherPC2 != IntPtr.Zero)
		{
			// Convert LaserScan to PointCloud2
			int numSamples = (int)(scan.Count * scan.VerticalCount);
			int pointsCount = 0;
			
			uint pointStep = 16; // x, y, z, intensity (4 floats)
			
			// P5: Reuse pooled points buffer
			EnsurePointsBuffer(numSamples);
			
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
					
					if (double.IsNaN(range) || double.IsInfinity(range) || range < scan.RangeMin || range > scan.RangeMax)
					{
						continue;
					}

					double horizontalAngle = scan.AngleMin + h * scan.AngleStep;
					
					_pooledPoints[pointsCount * 4 + 0] = (float)(range * Math.Cos(horizontalAngle) * cosVertical);
					_pooledPoints[pointsCount * 4 + 1] = (float)(range * Math.Sin(horizontalAngle) * cosVertical);
					_pooledPoints[pointsCount * 4 + 2] = (float)(range * sinVertical);
					_pooledPoints[pointsCount * 4 + 3] = (float)intensity;
					
					pointsCount++;
				}
			}

			if (pointsCount == 0) return;

			// P5: Allocate fields ptr once (lazy init)
			if (_pooledFieldsPtr == IntPtr.Zero)
			{
				var fields = new PointFieldStruct[4];
				fields[0] = new PointFieldStruct { name = "x", offset = 0, datatype = 7, count = 1 };
				fields[1] = new PointFieldStruct { name = "y", offset = 4, datatype = 7, count = 1 };
				fields[2] = new PointFieldStruct { name = "z", offset = 8, datatype = 7, count = 1 };
				fields[3] = new PointFieldStruct { name = "intensity", offset = 12, datatype = 7, count = 1 };

				_pooledFieldsPtr = Marshal.AllocHGlobal(4 * Marshal.SizeOf<PointFieldStruct>());
				for (int i = 0; i < 4; i++)
				{
					IntPtr ptr = new IntPtr(_pooledFieldsPtr.ToInt64() + i * Marshal.SizeOf<PointFieldStruct>());
					Marshal.StructureToPtr(fields[i], ptr, false);
				}
			}

			uint dataSize = (uint)(pointsCount * pointStep);
			EnsurePC2DataBuffer((int)dataSize);
			Marshal.Copy(_pooledPoints, 0, _pooledDataPtr, pointsCount * 4);

			var pc2Data = new PointCloud2Struct
			{
				timestamp = timestamp,
				frame_id = scan.Frame,
				height = 1,
				width = (uint)pointsCount,
				fields = _pooledFieldsPtr,
				fields_length = 4,
				is_bigendian = 0,
				point_step = pointStep,
				row_step = dataSize,
				data = _pooledDataPtr,
				data_length = dataSize,
				is_dense = 1
			};

			Ros2NativeWrapper.PublishPointCloud2(_rosPublisherPC2, ref pc2Data);
			// P5: No FreeHGlobal — buffers are reused
		}
	}

	new protected void OnDestroy()
	{
		if (_lidar != null) _lidar.OnLidarDataGenerated -= HandleNativeLidarData;

		// P5: Free pooled buffers
		FreePooledBuffers();

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