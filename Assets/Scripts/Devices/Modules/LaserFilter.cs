/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using messages = cloisim.msgs;

public class LaserFilter
{
	private bool useIntensity = false; // TODO: Currently do nothing

	[Header("Laser sensor properties")]
	private uint numberOfHorizontalBeams = 0;
	private uint numberOfVerticalBeams = 0;
	private uint totalNumberOfLaserBeams = 0;

	private SensorDevices.Lidar.MinMax angle;

	[Header("Laser filter properties")]
	private SensorDevices.Lidar.MinMax horizontalAnlgeFilter;
	// private float horizontalAngleLowerFilter = float.NegativeInfinity;
	// private float horizontalAngleUpperFilter = float.PositiveInfinity;
	private int? filterLowerHorizontalBeamIndex = null; // in rad
	private int? filterUpperHorizontalBeamIndex = null; // in rad

	public LaserFilter(in messages.LaserScan laserScan, bool useIntensity = false)
	{
		this.numberOfHorizontalBeams = laserScan.Count;
		this.numberOfVerticalBeams = laserScan.VerticalCount;
		this.totalNumberOfLaserBeams = this.numberOfHorizontalBeams * this.numberOfVerticalBeams;

		angle = new SensorDevices.Lidar.MinMax(laserScan.AngleMin, laserScan.AngleMax);

		this.useIntensity = useIntensity;
	}

	public void SetupFilter(in double filterLowerHorizontalAngle, in double filterUpperHorizontalAngle)
	{
		this.horizontalAnlgeFilter = new SensorDevices.Lidar.MinMax(filterLowerHorizontalAngle, filterUpperHorizontalAngle);

		var scanRange = angle.max - angle.min;
		var filterLowerBeamIndexRatio = (horizontalAnlgeFilter.min - angle.min) / scanRange;
		var filterUpperBeamIndexRatio = (horizontalAnlgeFilter.max - angle.min) / scanRange;

		this.filterLowerHorizontalBeamIndex = (angle.min >= horizontalAnlgeFilter.min) ? (int?)null : (int)((double)numberOfHorizontalBeams * filterLowerBeamIndexRatio);
		this.filterUpperHorizontalBeamIndex = (angle.max <= horizontalAnlgeFilter.max) ? (int?)null : (int)((double)numberOfHorizontalBeams * filterUpperBeamIndexRatio);
	}

	private bool IsIndexFiltering(in int index)
	{
		return ((filterLowerHorizontalBeamIndex != null && filterLowerHorizontalBeamIndex > index) ||
				(filterUpperHorizontalBeamIndex != null && filterUpperHorizontalBeamIndex < index));
	}

	public void DoFilter(ref messages.LaserScan laserScan)
	{
		if (filterLowerHorizontalBeamIndex == null && filterUpperHorizontalBeamIndex == null)
		{
			return;
		}

		if (laserScan.Intensities.Length == totalNumberOfLaserBeams && laserScan.Ranges.Length == totalNumberOfLaserBeams)
		{
			for (var horizontalIndex = 0; horizontalIndex < numberOfHorizontalBeams; horizontalIndex++)
			{
				var doFilter = (IsIndexFiltering(horizontalIndex)) ? true : false;

				if (doFilter)
				{
					for (var verticalIndex = 0; verticalIndex < numberOfVerticalBeams; verticalIndex++)
					{
						var index = horizontalIndex + (verticalIndex * numberOfHorizontalBeams);
						laserScan.Ranges[index] = double.NaN;
						laserScan.Intensities[index] = double.NaN;
					}
				}
			}
		}
	}
}