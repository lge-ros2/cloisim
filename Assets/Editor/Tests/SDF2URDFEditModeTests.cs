/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Globalization;
using System.Threading;
using NUnit.Framework;
using SDFormat;

namespace CLOiSim.Tests.EditMode
{
	public class SDF2URDFTests
	{
		private CultureInfo _originalCulture;

		[SetUp]
		public void SetUp()
		{
			_originalCulture = Thread.CurrentThread.CurrentCulture;
		}

		[TearDown]
		public void TearDown()
		{
			Thread.CurrentThread.CurrentCulture = _originalCulture;
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithoutCapsule_EmitsVersion10()
		{
			var sdf = @"
				<model name='box_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><box><size>1 1 1</size></box></geometry>
				    </visual>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("version=\"1.0\""));
			Assert.That(urdf, Does.Not.Contain("<capsule"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleInVisual_EmitsVersion11AndCapsuleElement()
		{
			var sdf = @"
				<model name='capsule_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><capsule><radius>0.1</radius><length>0.4</length></capsule></geometry>
				    </visual>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("version=\"1.1\""));
			Assert.That(urdf, Does.Contain("<capsule radius=\"0.1\" length=\"0.4\"/>"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleOnlyInCollision_EmitsVersion11()
		{
			var sdf = @"
				<model name='capsule_robot'>
				  <link name='base_link'>
				    <collision name='collision'>
				      <geometry><capsule><radius>0.2</radius><length>0.5</length></capsule></geometry>
				    </collision>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("version=\"1.1\""));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleInNestedModel_EmitsVersion11()
		{
			var sdf = @"
				<model name='parent_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><box><size>1 1 1</size></box></geometry>
				    </visual>
				  </link>
				  <model name='child_model'>
				    <link name='child_link'>
				      <visual name='visual'>
				        <geometry><capsule><radius>0.05</radius><length>0.2</length></capsule></geometry>
				      </visual>
				    </link>
				  </model>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("version=\"1.1\""));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithEllipsoid_ApproximatesAsSphereWithoutNonStandardElement()
		{
			var sdf = @"
				<model name='ellipsoid_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><ellipsoid><radii>0.1 0.2 0.3</radii></ellipsoid></geometry>
				    </visual>
				  </link>
				</model>";

			string urdf = null;
			Assert.DoesNotThrow(() => urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf));

			Assert.That(urdf, Does.Not.Contain("<ellipsoid"));
			Assert.That(urdf, Does.Contain("<sphere radius=\"0.2\"/>"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_CapsuleFormatting_IsCultureInvariant()
		{
			Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");

			var sdf = @"
				<model name='capsule_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><capsule><radius>0.1</radius><length>0.4</length></capsule></geometry>
				    </visual>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("<capsule radius=\"0.1\" length=\"0.4\"/>"));
		}
	}
}
