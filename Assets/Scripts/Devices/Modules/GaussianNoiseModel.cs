/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */


public class GaussianNoiseModel : NoiseModel
{
	public override void Apply(ref float[] data)
	{

	}
}

// GaussianNoiseModel::GaussianNoiseModel()
//   : Noise(Noise::GAUSSIAN),
//     mean(0.0),
//     stdDev(0.0),
//     bias(0.0),
//     precision(0.0),
//     quantized(false),
//     biasMean(0),
//     biasStdDev(0)
// {
// }

// void GaussianNoiseModel::Load(sdf::ElementPtr _sdf)
// {
//   Noise::Load(_sdf);
//   this->mean = _sdf->Get<double>("mean");
//   this->stdDev = _sdf->Get<double>("stddev");
//   if (_sdf->HasElement("bias_mean"))
//     this->biasMean = _sdf->Get<double>("bias_mean");
//   if (_sdf->HasElement("bias_stddev"))
//     this->biasStdDev = _sdf->Get<double>("bias_stddev");
//   this->SampleBias();

//   /// \todo Remove this, and use Noise::Print. See ImuSensor for an example
//   gzlog << "applying Gaussian noise model with mean " << this->mean
//     << ", stddev " << this->stdDev
//     << ", bias " << this->bias << std::endl;

//   if (_sdf->HasElement("precision"))
//   {
//     this->precision = _sdf->Get<double>("precision");
//     if (this->precision < 0)
//     {
//       gzerr << "Noise precision cannot be less than 0" << std::endl;
//     }
//     else if (!ignition::math::equal(this->precision, 0.0, 1e-6))
//     {
//       this->quantized = true;
//     }
//   }
// }

// double GaussianNoiseModel::ApplyImpl(double _in)
// {
//   // Add independent (uncorrelated) Gaussian noise to each input value.
//   double whiteNoise = ignition::math::Rand::DblNormal(this->mean, this->stdDev);
//   double output = _in + this->bias + whiteNoise;
//   if (this->quantized)
//   {
//     // Apply this->precision
//     if (!ignition::math::equal(this->precision, 0.0, 1e-6))
//     {
//       output = std::round(output / this->precision) * this->precision;
//     }
//   }
//   return output;
// }


// void GaussianNoiseModel::SetMean(const double _mean)
// {
//   this->mean = _mean;
//   this->SampleBias();
// }


// void GaussianNoiseModel::SetStdDev(const double _stddev)
// {
//   this->stdDev = _stddev;
//   this->SampleBias();
// }


// double GaussianNoiseModel::GetBias() const
// {
//   return this->bias;
// }


// void GaussianNoiseModel::SampleBias()
// {
//   this->bias =
//       ignition::math::Rand::DblNormal(this->biasMean, this->biasStdDev);
//   // With equal probability, we pick a negative bias (by convention,
//   // rateBiasMean should be positive, though it would work fine if
//   // negative).
//   if (ignition::math::Rand::DblUniform() < 0.5)
//     this->bias = -this->bias;
// }


// void GaussianNoiseModel::Print(std::ostream &_out) const
// {
//   _out << "Gaussian noise, mean[" << this->mean << "], "
//     << "stdDev[" << this->stdDev << "] "
//     << "bias[" << this->bias << "] "
//     << "precision[" << this->precision << "] "
//     << "quantized[" << this->quantized << "]";
// }