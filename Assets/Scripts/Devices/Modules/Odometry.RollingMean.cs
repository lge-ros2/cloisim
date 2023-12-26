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
		private Queue<float> _accumulate = null;
		private float _accumulateSum = 0f;

		public RollingMean(in int windowSize = 5)
		{
			_maxSize = windowSize;
			_accumulate = new Queue<float>(windowSize);
		}

		public void Accumulate(in float value)
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

		public float Get()
		{
			return _accumulateSum / (float)_accumulate.Count;
		}
	}
}