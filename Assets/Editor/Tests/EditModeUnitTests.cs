/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using NUnit.Framework;
using SensorDevices;
using UnityEngine;

namespace CLOiSim.Tests.EditMode
{
	public class SDF2UnityTests
	{
		[Test]
		public void Position_MapsRightHandedAxesToUnityCoordinates()
		{
			var position = SDF2Unity.Position(1d, 2d, 3d);

			Assert.That(position.x, Is.EqualTo(-2f).Within(1e-6f));
			Assert.That(position.y, Is.EqualTo(3f).Within(1e-6f));
			Assert.That(position.z, Is.EqualTo(1f).Within(1e-6f));
		}

		[Test]
		public void Rotation_MapsQuaternionComponentsToUnityOrder()
		{
			var rotation = SDF2Unity.Rotation(0.5d, 1d, 2d, 3d);

			Assert.That(rotation.x, Is.EqualTo(2f).Within(1e-6f));
			Assert.That(rotation.y, Is.EqualTo(-3f).Within(1e-6f));
			Assert.That(rotation.z, Is.EqualTo(-1f).Within(1e-6f));
			Assert.That(rotation.w, Is.EqualTo(0.5f).Within(1e-6f));
		}

		[Test]
		public void CurveOrientationAngle_NegatesInputAngle()
		{
			Assert.That(SDF2Unity.CurveOrientationAngle(0.75f), Is.EqualTo(-0.75f).Within(1e-6f));
		}
	}

	public class PoseRelativeLookupTests
	{
		[Test]
		public void FindPoseRelativeObject_ResolvesUnscopedLinkFromParentModelScope()
		{
			var world = new GameObject("world");
			var rootModel = new GameObject("robot_model");
			var chestLink = new GameObject("chest_link");

			try
			{
				rootModel.transform.SetParent(world.transform, false);
				rootModel.AddComponent<SDFormat.Helper.Model>();

				chestLink.transform.SetParent(rootModel.transform, false);
				var chestLinkHelper = chestLink.AddComponent<SDFormat.Helper.Link>();

				var relativeObject = SDFormat.Import.Util.FindPoseRelativeObject(rootModel.transform, "chest_link");

				Assert.That(relativeObject, Is.SameAs(chestLinkHelper));
			}
			finally
			{
				Object.DestroyImmediate(world);
			}
		}

		[Test]
		public void FindPoseRelativeObject_ResolvesScopedLinkFromNestedModelScope()
		{
			var world = new GameObject("world");
			var rootModel = new GameObject("robot_model");
			var chestLink = new GameObject("chest_link");
			var nestedModel = new GameObject("sensor_mount");

			try
			{
				rootModel.transform.SetParent(world.transform, false);
				rootModel.AddComponent<SDFormat.Helper.Model>();

				chestLink.transform.SetParent(rootModel.transform, false);
				var chestLinkHelper = chestLink.AddComponent<SDFormat.Helper.Link>();

				nestedModel.transform.SetParent(rootModel.transform, false);
				var nestedModelHelper = nestedModel.AddComponent<SDFormat.Helper.Model>();
				nestedModelHelper.isNested = true;

				var relativeObject = SDFormat.Import.Util.FindPoseRelativeObject(nestedModel.transform, "robot_model::chest_link");

				Assert.That(relativeObject, Is.SameAs(chestLinkHelper));
			}
			finally
			{
				Object.DestroyImmediate(world);
			}
		}
	}

	public class ColorEncodingTests
	{
		[Test]
		public void SparsifyBits_ReturnsZeroForZeroInput()
		{
			Assert.That(ColorEncoding.SparsifyBits(0, 3), Is.EqualTo(0));
		}

		[Test]
		public void EncodeIdAsColor_EncodesZeroIdAsOpaqueBlack()
		{
			var color = (Color32)ColorEncoding.EncodeIDAsColor(0);

			Assert.That(color.r, Is.EqualTo(0));
			Assert.That(color.g, Is.EqualTo(0));
			Assert.That(color.b, Is.EqualTo(0));
			Assert.That(color.a, Is.EqualTo(255));
		}

		[Test]
		public void EncodeLayerAsColor_ScalesWrappedPaletteEntries()
		{
			var color = ColorEncoding.EncodeLayerAsColor(16);

			Assert.That(color.r, Is.EqualTo(0.5f).Within(1e-6f));
			Assert.That(color.g, Is.EqualTo(0.5f).Within(1e-6f));
			Assert.That(color.b, Is.EqualTo(0.5f).Within(1e-6f));
			Assert.That(color.a, Is.EqualTo(0.5f).Within(1e-6f));
		}
	}

	public class BatteryTests
	{
		[Test]
		public void SetMax_InitializesCurrentVoltage()
		{
			var battery = new Battery("test");
			battery.SetMax(10f);

			Assert.That(battery.CurrentVoltage, Is.EqualTo(10f).Within(1e-6f));
		}

		[Test]
		public void Update_AppliesDischargeAfterOneSecond()
		{
			var battery = new Battery("test");
			battery.SetMax(10f);
			battery.Discharge(3f);

			Assert.That(battery.Update(0.5f), Is.EqualTo(10f).Within(1e-6f));
			Assert.That(battery.Update(0.6f), Is.EqualTo(7f).Within(1e-6f));
		}

		[Test]
		public void Update_ClampsVoltageToMinimumThreshold()
		{
			var battery = new Battery("test");
			battery.SetMax(10f);
			battery.Discharge(25f);

			Assert.That(battery.Update(1.1f), Is.EqualTo(1f).Within(1e-6f));
		}
	}

	public class PIDTests
	{
		[Test]
		public void Update_ReturnsZeroWhenDeltaTimeIsNotPositive()
		{
			var pid = new PID(1d, 0d, 0d);

			Assert.That(pid.Update(5d, 0d), Is.EqualTo(0d));
		}

		[Test]
		public void Update_ClampsCommandToConfiguredOutputRange()
		{
			var pid = new PID(1d, 0d, 0d, -100d, 100d, -10d, 10d);

			Assert.That(pid.Update(20d, 1d), Is.EqualTo(-10d));
		}

		[Test]
		public void Reset_RestoresInitialConfigurationAndClearsIntegralState()
		{
			var pid = new PID(1d, 1d, 0d, -5d, 5d, -10d, 10d);
			pid.Update(2d, 1d);
			pid.Change(4d, 0d, 3d);
			pid.SetIntegralRange(-1d, 1d);
			pid.SetOutputRange(-2d, 2d);
			pid.Reset();

			Assert.That(pid.PGain, Is.EqualTo(1d));
			Assert.That(pid.IGain, Is.EqualTo(1d));
			Assert.That(pid.DGain, Is.EqualTo(0d));
			Assert.That(pid.IntegralRangeMin, Is.EqualTo(-5d));
			Assert.That(pid.IntegralRangeMax, Is.EqualTo(5d));
			Assert.That(pid.OutputRangeMin, Is.EqualTo(-10d));
			Assert.That(pid.OutputRangeMax, Is.EqualTo(10d));
			Assert.That(pid.Update(0d, 1d), Is.EqualTo(0d));
		}
	}
}
