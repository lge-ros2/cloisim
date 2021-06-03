/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

public class ImageGaussianNoiseModel : GaussianNoiseModel
{
	public override void Apply(ref float[] data)
	{

	}
}

// void ImageGaussianNoiseModel::Load(sdf::ElementPtr _sdf)
// {
//   GaussianNoiseModel::Load(_sdf);
// }


// void ImageGaussianNoiseModel::SetCamera(rendering::CameraPtr _camera)
// {
//   GZ_ASSERT(_camera, "Unable to apply gaussian noise, camera is null");

//   this->gaussianNoiseCompositorListener.reset(new
//         GaussianNoiseCompositorListener(this->mean, this->stdDev));

//   this->gaussianNoiseInstance =
//     Ogre::CompositorManager::getSingleton().addCompositor(
//       _camera->OgreViewport(), "CameraNoise/Gaussian");
//   this->gaussianNoiseInstance->setEnabled(true);
//   this->gaussianNoiseInstance->addListener(
//     this->gaussianNoiseCompositorListener.get());
// }

// void ImageGaussianNoiseModel::Print(std::ostream &_out) const
// {
//   _out << "Image Gaussian noise, mean[" << this->mean << "], "
//     << "stdDev[" << this->stdDev << "] "
//     << "bias[" << this->bias << "] "
//     << "precision[" << this->precision << "] "
//     << "quantized[" << this->quantized << "]";
// }



// namespace gazebo
// {
//   class GaussianNoiseCompositorListener
//     : public Ogre::CompositorInstance::Listener
//   {
//     /// \brief Constructor, setting mean and standard deviation.
//     public: GaussianNoiseCompositorListener(const double &_mean,
//                                             const double &_stddev):
//         mean(_mean), stddev(_stddev) {}

//     /// \brief Callback that OGRE will invoke for us on each render call
//     /// \param[in] _passID OGRE material pass ID.
//     /// \param[in] _mat Pointer to OGRE material.
//     public: virtual void notifyMaterialRender(unsigned int _passId,
//                                               Ogre::MaterialPtr &_mat)
//     {
//       GZ_ASSERT(!_mat.isNull(), "Null OGRE material");
//       // modify material here (wont alter the base material!), called for
//       // every drawn geometry instance (i.e. compositor render_quad)

//       // Sample three values within the range [0,1.0] and set them for use in
//       // the fragment shader, which will interpret them as offsets from (0,0)
//       // to use when computing pseudo-random values.
//       Ogre::Vector3 offsets(ignition::math::Rand::DblUniform(0.0, 1.0),
//                             ignition::math::Rand::DblUniform(0.0, 1.0),
//                             ignition::math::Rand::DblUniform(0.0, 1.0));
//       // These calls are setting parameters that are declared in two places:
//       // 1. media/materials/scripts/gazebo.material, in
//       //    fragment_program Gazebo/GaussianCameraNoiseFS
//       // 2. media/materials/scripts/camera_noise_gaussian_fs.glsl
//       Ogre::Technique *technique = _mat->getTechnique(0);
//       GZ_ASSERT(technique, "Null OGRE material technique");
//       Ogre::Pass *pass = technique->getPass(_passId);
//       GZ_ASSERT(pass, "Null OGRE material pass");
//       Ogre::GpuProgramParametersSharedPtr params =
//           pass->getFragmentProgramParameters();
//       GZ_ASSERT(!params.isNull(), "Null OGRE material GPU parameters");

//       params->setNamedConstant("offsets", offsets);
//       params->setNamedConstant("mean", static_cast<Ogre::Real>(this->mean));
//       params->setNamedConstant("stddev", static_cast<Ogre::Real>(this->stddev));
//     }

//     /// \brief Mean that we'll pass down to the GLSL fragment shader.
//     private: const double &mean;
//     /// \brief Standard deviation that we'll pass down to the GLSL fragment
//     /// shader.
//     private: const double &stddev;
//   };
// }  // namespace gazebo