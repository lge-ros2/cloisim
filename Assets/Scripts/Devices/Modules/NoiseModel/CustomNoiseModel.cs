/*
 * Copyright (c) 2025 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Xml;
using System.Collections.Generic;
using System;

public class CustomNoiseModel : GaussianNoiseModel
{
	private struct CustomNoise
	{
		public enum Type
		{
			NONE,
			GAUSSIAN,
			DISTANCE
		}

		public Type type;
		public MathUtil.MinMax range;

		public double mean;
		public double ratio;
		public double stddev;

		public CustomNoise(Type type, MathUtil.MinMax range, double mean, double ratio, double stddev)
		{
			this.type = type;
			this.range = range;
			this.mean = mean;
			this.ratio = ratio;
			this.stddev = stddev;
		}

		public override string ToString()
		{
			return $"Type: {type}, Range: {range}, Mean/Ratio: {mean}/{ratio}, StdDev: {stddev}";
		}
	}

	private List<CustomNoise> customNoiseSet = new();

	public CustomNoiseModel(in SDF.Noise parameter)
		: base(parameter)
	{
	}

	private float ApplyDistanceNoise(in float distance, in float ratio, in float stddev)
	{
		var gaussian = RandomNumberGenerator.GetNormal();
		var noisyDistance = distance * (1.0f + ratio + stddev * (float)gaussian);
		return noisyDistance;
	}

	private double ApplyGaussianNoise(in float distance, in float mean, in float stddev, in float deltaTime)
	{
		var whiteNoise = RandomNumberGenerator.GetNormal(mean, stddev);

		var bias = ComputeDynamicBias(deltaTime);

		var output = distance + bias + whiteNoise;

		if (_quantized)
		{
			if (Math.Abs(_parameter.precision - 0d) > Epsilon)
			{
				output = Math.Round(output / _parameter.precision) * _parameter.precision;
			}
		}

		return output;
	}

	public override T Generate<T>(T data, float deltaTime)
	{
		for (var i = 0; i < customNoiseSet.Count; i++)
		{
			var customNoise = customNoiseSet[i];
			var typeCastedData = Convert.ToSingle(data);

			if (typeCastedData >= customNoise.range.min && typeCastedData < customNoise.range.max)
			{
				if (customNoise.type == CustomNoise.Type.DISTANCE)
				{
					var noisyDistance = ApplyDistanceNoise(typeCastedData, (float)customNoise.ratio, (float)customNoise.stddev);
					return (T)Convert.ChangeType(Clamp(noisyDistance), typeof(T));
				}
				else if (customNoise.type == CustomNoise.Type.GAUSSIAN)
				{
					var noisyDistance = ApplyGaussianNoise(typeCastedData, (float)customNoise.mean, (float)customNoise.stddev, deltaTime);
					return (T)Convert.ChangeType(Clamp(noisyDistance), typeof(T));
				}
			}
		}

		return data;
	}

	public void ParseParameter(in SDF.Plugin plugin)
	{
		var innerDoc = new XmlDocument();
		innerDoc.LoadXml($"<root>{plugin.RawXml()}</root>");

		var customNoiseNode = innerDoc.SelectSingleNode("//custom_noise");
		if (customNoiseNode == null)
			return;

		var noiseNodes = customNoiseNode.SelectNodes("noise");
		foreach (XmlNode noiseNode in noiseNodes)
		{
			var type = noiseNode.Attributes["type"]?.Value ?? string.Empty;
			var rangeMin = double.Parse(noiseNode.Attributes["range_min"]?.Value ?? "0");
			var rangeMax = double.Parse(noiseNode.Attributes["range_max"]?.Value ?? "0");

			if (type == "gaussian")
			{
				var mean = double.Parse(noiseNode.SelectSingleNode("mean")?.InnerText ?? "0");
				var stddev = double.Parse(noiseNode.SelectSingleNode("stddev")?.InnerText ?? "0");
				var customNoise = new CustomNoise(
					type: CustomNoise.Type.GAUSSIAN,
					range: new MathUtil.MinMax(rangeMin, rangeMax),
					mean: mean,
					ratio: double.NaN,
					stddev: stddev);
				customNoiseSet.Add(customNoise);
			}
			else if (type == "distance")
			{
				var ratio = double.Parse(noiseNode.SelectSingleNode("ratio")?.InnerText ?? "0");
				var stddev = double.Parse(noiseNode.SelectSingleNode("stddev")?.InnerText ?? "0");
				var customNoise = new CustomNoise(
					type: CustomNoise.Type.DISTANCE,
					range: new MathUtil.MinMax(rangeMin, rangeMax),
					mean: double.NaN,
					ratio: ratio,
					stddev: stddev);
				customNoiseSet.Add(customNoise);
			}

			Console.WriteLine(customNoiseSet[customNoiseSet.Count - 1]);
		}
	}
}