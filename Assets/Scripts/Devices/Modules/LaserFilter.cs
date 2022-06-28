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

	private SensorDevices.LaserData.MinMax angle;

	[Header("Laser filter properties")]
	private SensorDevices.LaserData.MinMax? rangeFilter = null;
	private SensorDevices.LaserData.MinMax horizontalAnlgeFilter;
	private int? filterLowerHorizontalBeamIndex = null; // in rad
	private int? filterUpperHorizontalBeamIndex = null; // in rad

	public LaserFilter(in messages.LaserScan laserScan, bool useIntensity = false)
	{
		this.numberOfHorizontalBeams = laserScan.Count;
		this.numberOfVerticalBeams = laserScan.VerticalCount;
		this.totalNumberOfLaserBeams = this.numberOfHorizontalBeams * this.numberOfVerticalBeams;

		angle = new SensorDevices.LaserData.MinMax(laserScan.AngleMin, laserScan.AngleMax);

		this.useIntensity = useIntensity;
	}

	public void SetupAngleFilter(in double filterLowerHorizontalAngle, in double filterUpperHorizontalAngle)
	{
		this.horizontalAnlgeFilter = new SensorDevices.LaserData.MinMax(filterLowerHorizontalAngle, filterUpperHorizontalAngle);

		var filterLowerBeamIndexRatio = (horizontalAnlgeFilter.min - angle.min) / angle.range;
		var filterUpperBeamIndexRatio = (horizontalAnlgeFilter.max - angle.min) / angle.range;

		this.filterLowerHorizontalBeamIndex = (angle.min >= horizontalAnlgeFilter.min) ? (int?)null : (int)((double)numberOfHorizontalBeams * filterLowerBeamIndexRatio);
		this.filterUpperHorizontalBeamIndex = (angle.max <= horizontalAnlgeFilter.max) ? (int?)null : (int)((double)numberOfHorizontalBeams * filterUpperBeamIndexRatio);
	}

	public void SetupRangeFilter(in double filterRangeMin, in double filterRangeMax)
	{
		this.rangeFilter = new SensorDevices.LaserData.MinMax(filterRangeMin, filterRangeMax);
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

				for (var verticalIndex = 0; verticalIndex < numberOfVerticalBeams; verticalIndex++)
				{
					var index = horizontalIndex + (verticalIndex * numberOfHorizontalBeams);

					if (doFilter)
					{
						laserScan.Ranges[index] = double.NaN;
						laserScan.Intensities[index] = double.NaN;
					}

					if (rangeFilter != null)
					{
						if (laserScan.Ranges[index] > rangeFilter?.max || laserScan.Ranges[index] < rangeFilter?.min)
						{
							laserScan.Ranges[index] = double.NaN;
						}
					}
				}
			}
		}
	}
}