/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

namespace SensorDevices
{
	public partial class Lidar : Device
	{
		private bool useIntensity_ = false; // TODO: Currently do nothing
		private int? filterLowerBeamIndex_ = null; // in rad
		private int? filterUpperBeamIndex_ = null; // in rad

		private void DoParseFilter()
		{
			if (pluginParameters == null)
			{
				return;
			}

			// Get Paramters
			useIntensity_ = pluginParameters.GetValue<bool>("intensity");
			var filterAngleLower_ = pluginParameters.GetValue<float>("filter/angle/horizontal/lower", float.NegativeInfinity);
			var filterAngleUpper_ = pluginParameters.GetValue<float>("filter/angle/horizontal/upper", float.PositiveInfinity);

			// calculate angle filter range
			var laserScan = laserScanStamped.Scan;
			var numberOfBeams = laserScan.Ranges.Length;

			if (numberOfBeams == laserScan.Intensities.Length)
			{
				var filterLowerBeamIndexRatio = (filterAngleLower_ - laserScan.AngleMin) / (laserScan.AngleMax - laserScan.AngleMin);
				var filterUpperBeamIndexRatio = (filterAngleUpper_ - laserScan.AngleMin) / (laserScan.AngleMax - laserScan.AngleMin);

				filterLowerBeamIndex_ = (laserScan.AngleMin >= filterAngleLower_) ? (int?)null : (int)((double)numberOfBeams * filterLowerBeamIndexRatio);
				filterUpperBeamIndex_ = (laserScan.AngleMax <= filterAngleUpper_) ? (int?)null : (int)((double)numberOfBeams * filterUpperBeamIndexRatio);
			}
			else
			{
				Debug.LogWarningFormat("Length of RayRanges and intensites are different {0}-{1}", numberOfBeams, laserScan.Intensities.Length);
			}

		}

		private bool IsIndexFiltering(in int index)
		{
			return ((filterLowerBeamIndex_ != null && filterLowerBeamIndex_ > index) ||
					(filterUpperBeamIndex_ != null && filterUpperBeamIndex_ < index));
		}

		private void DoLaserAngleFilter()
		{
			if (filterLowerBeamIndex_ == null && filterUpperBeamIndex_ == null)
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