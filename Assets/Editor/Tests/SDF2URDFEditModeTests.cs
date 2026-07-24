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
		public void ConvertModelXmlToUrdf_NeverEmitsVersionAttribute()
		{
			// The deployed urdf_xml_parser (robot_state_publisher/rviz2) hard-rejects
			// any <robot version="..."> other than "1.0", so no version attribute is
			// emitted at all (an absent attribute is treated as 1.0 by URDF consumers).
			var sdf = @"
				<model name='box_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><box><size>1 1 1</size></box></geometry>
				    </visual>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Not.Contain("<robot name=\"box_robot\" version="));
			Assert.That(urdf, Does.Contain("<robot name=\"box_robot\">"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleInVisual_ApproximatesAsCylinderWithoutNonStandardElement()
		{
			// <capsule> is not a standard URDF 1.0 primitive and is not recognized by
			// the target urdf_xml_parser, so it is approximated with a <cylinder> of
			// the same radius/length instead of being emitted as-is.
			var sdf = @"
				<model name='capsule_robot'>
				  <link name='base_link'>
				    <visual name='visual'>
				      <geometry><capsule><radius>0.1</radius><length>0.4</length></capsule></geometry>
				    </visual>
				  </link>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Not.Contain("<capsule"));
			Assert.That(urdf, Does.Contain("<cylinder radius=\"0.1\" length=\"0.4\"/>"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleOnlyInCollision_ApproximatesAsCylinder()
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

			Assert.That(urdf, Does.Not.Contain("<capsule"));
			Assert.That(urdf, Does.Contain("<cylinder radius=\"0.2\" length=\"0.5\"/>"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithCapsuleInNestedModel_ApproximatesAsCylinder()
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

			Assert.That(urdf, Does.Not.Contain("<capsule"));
			Assert.That(urdf, Does.Contain("<cylinder radius=\"0.05\" length=\"0.2\"/>"));
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
		public void ConvertModelXmlToUrdf_CapsuleApproximationFormatting_IsCultureInvariant()
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

			Assert.That(urdf, Does.Contain("<cylinder radius=\"0.1\" length=\"0.4\"/>"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithUnboundedRevoluteLimit_EmitsContinuousJointWithoutLimit()
		{
			// SDF expresses an unbounded revolute joint as lower=-inf/upper=inf. The
			// target urdf_xml_parser rejects "-inf"/"inf" as a limit value, so this
			// must become a URDF "continuous" joint with no <limit> element at all
			// (no effort/velocity was specified either).
			var sdf = @"
				<model name='caster_robot'>
				  <link name='base_link'/>
				  <joint name='caster_joint' type='revolute'>
				    <parent>base_link</parent>
				    <child>caster_link</child>
				    <axis>
				      <xyz>0 0 1</xyz>
				      <limit>
				        <lower>-inf</lower>
				        <upper>inf</upper>
				      </limit>
				    </axis>
				  </joint>
				  <link name='caster_link'/>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("<joint name=\"caster_joint\" type=\"continuous\">"));
			Assert.That(urdf, Does.Not.Contain("-inf"));
			Assert.That(urdf, Does.Not.Contain("<limit"));
		}

		[Test]
		public void ConvertModelXmlToUrdf_WithUnboundedRevoluteLimitAndEffort_EmitsContinuousJointWithEffortVelocityOnly()
		{
			var sdf = @"
				<model name='wheel_robot'>
				  <link name='base_link'/>
				  <joint name='wheel_joint' type='revolute'>
				    <parent>base_link</parent>
				    <child>wheel_link</child>
				    <axis>
				      <xyz>0 1 0</xyz>
				      <limit>
				        <lower>-inf</lower>
				        <upper>inf</upper>
				        <effort>11.7</effort>
				        <velocity>11.765</velocity>
				      </limit>
				    </axis>
				  </joint>
				  <link name='wheel_link'/>
				</model>";

			var urdf = SDF2URDF.ConvertModelXmlToUrdf(sdf);

			Assert.That(urdf, Does.Contain("<joint name=\"wheel_joint\" type=\"continuous\">"));
			Assert.That(urdf, Does.Not.Contain("-inf"));
			Assert.That(urdf, Does.Contain("<limit effort=\"11.7\" velocity=\"11.765\"/>"));
		}
	}
}
