/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;

public partial class Odometry
{
	private class RollingMean
	{
		private int _maxSize = 0;
		private Queue<double> _accumulate = null;
		private double _accumulateSum = 0f;

		public RollingMean(in int windowSize = 5)
		{
			_maxSize = windowSize;
			_accumulate = new Queue<double>(windowSize);
		}

		public void Accumulate(in double value)
		{
			if (_accumulate.Count == _maxSize)
			{
				_accumulateSum -= _accumulate.Dequeue();
			}

			_accumulate.Enqueue(value);
			_accumulateSum += value;
		}

		public void Reset()
		{
			_accumulate.Clear();
			_accumulateSum = 0;
		}

		public double Get()
		{
			return _accumulateSum / (double)_accumulate.Count;
		}
	}
}