/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Stopwatch = System.Diagnostics.Stopwatch;
using messages = gazebo.msgs;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private const int NONE_FILTER = -1;
		private bool useIntensity_ = false; // TODO: Currently do nothing
		private int filterLowerBeamIndex_; // in rad
		private int filterUpperBeamIndex_; // in rad

		private void DoParseFilter()
		{
			// Get Paramters
			useIntensity_ = parameters.GetValue<bool>("intensity");
			var filterAngleLower_ = parameters.GetValue<float>("filter/angle/horizontal/lower", float.NegativeInfinity);
			var filterAngleUpper_ = parameters.GetValue<float>("filter/angle/horizontal/upper", float.PositiveInfinity);

			// calculate angle filter range
			var laserScan = laserScanStamped.Scan;
			var numberOfBeams = laserScan.Ranges.Length;

			if (numberOfBeams == laserScan.Intensities.Length)
			{
				var filterLowerBeamIndexRatio = (filterAngleLower_ - laserScan.AngleMin) / (laserScan.AngleMax - laserScan.AngleMin);
				var filterUpperBeamIndexRatio = (filterAngleUpper_ - laserScan.AngleMin) / (laserScan.AngleMax - laserScan.AngleMin);

				filterLowerBeamIndex_ = (laserScan.AngleMin >= filterAngleLower_) ? NONE_FILTER : (int)((double)numberOfBeams * filterLowerBeamIndexRatio);
				filterUpperBeamIndex_ = (laserScan.AngleMax <= filterAngleUpper_) ? NONE_FILTER : (int)((double)numberOfBeams * filterUpperBeamIndexRatio);
			}
			else
			{
				Debug.LogWarningFormat("Length of RayRanges and intensites are different {0}-{1}", numberOfBeams, laserScan.Intensities.Length);
				filterLowerBeamIndex_ = -1;
				filterUpperBeamIndex_ = -1;
			}

		}

		private bool IsIndexFiltering(in int index)
		{
			return ((filterLowerBeamIndex_ != NONE_FILTER && filterLowerBeamIndex_ > index) ||
					(filterUpperBeamIndex_ != NONE_FILTER && filterUpperBeamIndex_ < index));
		}

		private void DoLaserAngleFilter()
		{
			if (filterLowerBeamIndex_ == NONE_FILTER && filterUpperBeamIndex_ == NONE_FILTER)
			{
				return;
			}

			var laserScan = laserScanStamped.Scan;
			var numberOfBeams = laserScan.Ranges.Length;

			for (var index = 0; index < numberOfBeams; index++)
			{
				var doFilter = (IsIndexFiltering(index)) ? true : false;

				if (doFilter)
				{
					laserScan.Ranges[index] = 0;
					laserScan.Intensities[index] = 0;
				}
			}
		}
	}
}