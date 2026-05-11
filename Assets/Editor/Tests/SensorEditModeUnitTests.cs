/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using NUnit.Framework;
using SensorDevices;
using messages = cloisim.msgs;

namespace CLOiSim.Tests.EditMode
{
	internal static class NoiseTestHelper
	{
		public static SDFormat.Noise CreateNoise(
			double mean = 0d,
			double stdDev = 0d,
			double biasMean = 0d,
			double biasStdDev = 0d,
			double precision = 0d,
			double dynamicBiasStdDev = 0d,
			double dynamicBiasCorrelationTime = 0d)
		{
			return new SDFormat.Noise
			{
				Mean = mean,
				StdDev = stdDev,
				BiasMean = biasMean,
				BiasStdDev = biasStdDev,
				Precision = precision,
				DynamicBiasStdDev = dynamicBiasStdDev,
				DynamicBiasCorrelationTime = dynamicBiasCorrelationTime
			};
		}
	}

	public class RandomNumberGeneratorTests
	{
		[Test]
		public void SetSeed_ReplaysTheSameUniformSequence()
		{
			RandomNumberGenerator.SetSeed(123u, 456u);
			var first = RandomNumberGenerator.GetUniform();
			var second = RandomNumberGenerator.GetUniform();

			RandomNumberGenerator.SetSeed(123u, 456u);

			Assert.That(RandomNumberGenerator.GetUniform(), Is.EqualTo(first));
			Assert.That(RandomNumberGenerator.GetUniform(), Is.EqualTo(second));
			Assert.That(first, Is.GreaterThan(0d).And.LessThan(1d));
			Assert.That(second, Is.GreaterThan(0d).And.LessThan(1d));
		}

		[Test]
		public void GetNormal_ReturnsMeanWhenStandardDeviationIsZero()
		{
			RandomNumberGenerator.SetSeed(321u, 654u);

			Assert.That(RandomNumberGenerator.GetNormal(2.5d, 0d), Is.EqualTo(2.5d));
		}
	}

	public class CameraDataTests
	{
		[Test]
		public void GetPixelFormat_MapsLegacyAliases()
		{
			Assert.That(CameraData.GetPixelFormat("L8"), Is.EqualTo(CameraData.PixelFormat.L_INT8));
			Assert.That(CameraData.GetPixelFormat("RGB_UINT16"), Is.EqualTo(CameraData.PixelFormat.RGB_INT16));
		}

		[Test]
		public void GetPixelFormat_IsCaseInsensitiveForEnumNamesAndDefaultsUnknownFormats()
		{
			Assert.That(CameraData.GetPixelFormat("bgr_int16"), Is.EqualTo(CameraData.PixelFormat.BGR_INT16));
			Assert.That(CameraData.GetPixelFormat("not-a-format"), Is.EqualTo(CameraData.PixelFormat.RGB_INT8));
		}

		[Test]
		public void GetImageDepth_UsesExpectedStepSizePerFormat()
		{
			Assert.That(CameraData.GetImageDepth(CameraData.PixelFormat.L_INT16), Is.EqualTo(2));
			Assert.That(CameraData.GetImageDepth(CameraData.PixelFormat.R_FLOAT32), Is.EqualTo(4));
			Assert.That(CameraData.GetImageDepth(CameraData.PixelFormat.UNKNOWN_PIXEL_FORMAT), Is.EqualTo(3));
		}
	}

	public class LaserFilterTests
	{
		[Test]
		public void DoFilter_AppliesAngleAndRangeFilteringToSyntheticScan()
		{
			var laserScan = CreateLaserScan(
				count: 4,
				verticalCount: 1,
				angleMin: -1d,
				angleMax: 1d,
				ranges: new[] { 1d, 2d, 3d, 4d },
				intensities: new[] { 10d, 20d, 30d, 40d });

			var filter = new LaserFilter(laserScan);
			filter.SetupAngleFilter(-0.25d, 0.25d);
			filter.SetupRangeFilter(2.5d, 3.5d);
			filter.DoFilter(ref laserScan);

			Assert.That(double.IsNaN(laserScan.Ranges[0]), Is.True);
			Assert.That(double.IsNaN(laserScan.Ranges[1]), Is.True);
			Assert.That(laserScan.Ranges[2], Is.EqualTo(3d));
			Assert.That(double.IsNaN(laserScan.Ranges[3]), Is.True);

			Assert.That(double.IsNaN(laserScan.Intensities[0]), Is.True);
			Assert.That(laserScan.Intensities[1], Is.EqualTo(20d));
			Assert.That(laserScan.Intensities[2], Is.EqualTo(30d));
			Assert.That(double.IsNaN(laserScan.Intensities[3]), Is.True);
		}

		private static messages.LaserScan CreateLaserScan(
			uint count,
			uint verticalCount,
			double angleMin,
			double angleMax,
			double[] ranges,
			double[] intensities)
		{
			return new messages.LaserScan
			{
				Count = count,
				VerticalCount = verticalCount,
				AngleMin = angleMin,
				AngleMax = angleMax,
				Ranges = ranges,
				Intensities = intensities
			};
		}
	}

	public class GaussianNoiseModelTests
	{
		[Test]
		public void Generate_ReturnsInputWhenNoiseAndBiasAreZero()
		{
			RandomNumberGenerator.SetSeed(11u, 22u);
			var model = new GaussianNoiseModel(NoiseTestHelper.CreateNoise());

			Assert.That(model.Generate(5d, 0.1f), Is.EqualTo(5d).Within(1e-12d));
		}

		[Test]
		public void Generate_RespectsConfiguredClampBounds()
		{
			RandomNumberGenerator.SetSeed(33u, 44u);
			var model = new GaussianNoiseModel(NoiseTestHelper.CreateNoise());
			model.SetClampMin(0d);
			model.SetClampMax(1d);

			Assert.That(model.Generate(5d, 0.1f), Is.EqualTo(1d).Within(1e-12d));
			Assert.That(model.Generate(-5d, 0.1f), Is.EqualTo(0d).Within(1e-12d));
		}

		[Test]
		public void Generate_QuantizesOutputWhenQuantizationIsEnabled()
		{
			RandomNumberGenerator.SetSeed(55u, 66u);
			var model = new GaussianNoiseModel(NoiseTestHelper.CreateNoise(precision: 0.25d));
			model.SetQuantization(true);

			Assert.That(model.Generate(0.36d, 0.1f), Is.EqualTo(0.25d).Within(1e-12d));
		}
	}

	public class CustomNoiseModelTests
	{
		[Test]
		public void Generate_AppliesGaussianRuleWithinMatchingRange()
		{
			var model = new CustomNoiseModel(NoiseTestHelper.CreateNoise());
			model.ParseParameter(@"<noise type=""gaussian"" range_min=""0"" range_max=""5""><mean>1.25</mean><stddev>0</stddev></noise>");

			Assert.That(model.Generate(2d, 0.1f), Is.EqualTo(3.25d).Within(1e-12d));
		}

		[Test]
		public void Generate_AppliesDistanceRuleWithinMatchingRange()
		{
			var model = new CustomNoiseModel(NoiseTestHelper.CreateNoise());
			model.ParseParameter(@"<noise type=""distance"" range_min=""5"" range_max=""20""><ratio>0.1</ratio><stddev>0</stddev></noise>");

			Assert.That(model.Generate(10d, 0.1f), Is.EqualTo(11d).Within(1e-12d));
		}

		[Test]
		public void Generate_ReturnsInputWhenNoCustomRangeMatches()
		{
			var model = new CustomNoiseModel(NoiseTestHelper.CreateNoise());
			model.ParseParameter(@"<noise type=""gaussian"" range_min=""0"" range_max=""5""><mean>1.25</mean><stddev>0</stddev></noise>");

			Assert.That(model.Generate(8d, 0.1f), Is.EqualTo(8d).Within(1e-12d));
		}
	}
}