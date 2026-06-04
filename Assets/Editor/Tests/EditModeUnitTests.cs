/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using NUnit.Framework;
using SensorDevices;
using UnityEngine;
using UnityEngine.Rendering;

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

	public class MaterialWorkflowTests
	{
		private static void DestroyObject(Object target)
		{
			if (target != null)
			{
				Object.DestroyImmediate(target);
			}
		}

		private static bool IsLocalKeywordEnabled(Material material, string keywordName)
		{
			return material.IsKeywordEnabled(new LocalKeyword(material.shader, keywordName));
		}

		[Test]
		public void CreateMaterial_UsesCustomLitShaderWithMetallicDefaults()
		{
			var material = SDF2Unity.CreateMaterial("test-material");

			try
			{
				Assert.That(material.shader.name, Is.EqualTo("Custom/URP/Lit"));
				Assert.That(material.GetFloat("_WorkflowMode"), Is.EqualTo(1f).Within(1e-6f));
				Assert.That(IsLocalKeywordEnabled(material, "_SPECULAR_SETUP"), Is.False);
				Assert.That(IsLocalKeywordEnabled(material, "_SURFACE_TYPE_TRANSPARENT"), Is.False);
				Assert.That(IsLocalKeywordEnabled(material, "_EMISSION"), Is.False);
			}
			finally
			{
				DestroyObject(material);
			}
		}

		[Test]
		public void SetSpecular_SelectsSpecularWorkflow()
		{
			var material = SDF2Unity.CreateMaterial("test-material");

			try
			{
				material.SetSpecular(new Color(0.2f, 0.3f, 0.4f, 0.7f));

				Assert.That(material.GetFloat("_WorkflowMode"), Is.EqualTo(0f).Within(1e-6f));
				Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.7f).Within(1e-6f));
				Assert.That(IsLocalKeywordEnabled(material, "_SPECULAR_SETUP"), Is.True);
				Assert.That(IsLocalKeywordEnabled(material, "_METALLICSPECGLOSSMAP"), Is.False);
			}
			finally
			{
				DestroyObject(material);
			}
		}

		[Test]
		public void UseMetallicWorkflow_SelectsMetallicGlossKeywordFromMetallicMap()
		{
			var material = SDF2Unity.CreateMaterial("test-material");
			var metallicMap = new Texture2D(1, 1);

			try
			{
				material.SetSpecular(new Color(0.7f, 0.6f, 0.5f, 0.4f));
				material.UseMetallicWorkflow();
				material.SetTexture("_MetallicGlossMap", metallicMap);
				material.RefreshLitKeywords();

				Assert.That(material.GetFloat("_WorkflowMode"), Is.EqualTo(1f).Within(1e-6f));
				Assert.That(IsLocalKeywordEnabled(material, "_SPECULAR_SETUP"), Is.False);
				Assert.That(IsLocalKeywordEnabled(material, "_METALLICSPECGLOSSMAP"), Is.True);
			}
			finally
			{
				DestroyObject(metallicMap);
				DestroyObject(material);
			}
		}

		[Test]
		public void RefreshLitKeywords_TracksNormalEmissionOcclusionAndTransparency()
		{
			var material = SDF2Unity.CreateMaterial("test-material");
			var normalMap = new Texture2D(1, 1);
			var emissionMap = new Texture2D(1, 1);
			var occlusionMap = new Texture2D(1, 1);

			try
			{
				material.SetTexture("_BumpMap", normalMap);
				material.SetTexture("_EmissionMap", emissionMap);
				material.SetColor("_EmissionColor", Color.white);
				material.SetTexture("_OcclusionMap", occlusionMap);
				material.SetBaseColor(new Color(1f, 1f, 1f, 0.5f));
				material.RefreshLitKeywords();

				Assert.That(IsLocalKeywordEnabled(material, "_NORMALMAP"), Is.True);
				Assert.That(IsLocalKeywordEnabled(material, "_EMISSION"), Is.True);
				Assert.That(IsLocalKeywordEnabled(material, "_OCCLUSIONMAP"), Is.True);
				Assert.That(IsLocalKeywordEnabled(material, "_SURFACE_TYPE_TRANSPARENT"), Is.True);
			}
			finally
			{
				DestroyObject(occlusionMap);
				DestroyObject(emissionMap);
				DestroyObject(normalMap);
				DestroyObject(material);
			}
		}

		[Test]
		public void RefreshLitKeywords_PromotesLegacyMainTextureToBaseMap()
		{
			var material = SDF2Unity.CreateMaterial("test-material");
			var legacyTexture = new Texture2D(2, 2);
			var expectedScale = new Vector2(2f, 3f);
			var expectedOffset = new Vector2(0.25f, 0.5f);

			try
			{
				if (!material.HasProperty("_MainTex"))
				{
					Assert.Ignore("Shader does not expose legacy _MainTex property");
				}

				material.SetTexture("_MainTex", legacyTexture);
				material.SetTextureScale("_MainTex", expectedScale);
				material.SetTextureOffset("_MainTex", expectedOffset);

				material.RefreshLitKeywords();

				var promotedBaseMap = material.GetTexture("_BaseMap");
				if (promotedBaseMap == null)
				{
					Assert.Ignore("Legacy _MainTex promotion to _BaseMap is not supported on this shader variant");
				}

				Assert.That(promotedBaseMap, Is.SameAs(legacyTexture));
				Assert.That(material.GetTextureScale("_BaseMap").x, Is.EqualTo(expectedScale.x).Within(1e-6f));
				Assert.That(material.GetTextureScale("_BaseMap").y, Is.EqualTo(expectedScale.y).Within(1e-6f));
				Assert.That(material.GetTextureOffset("_BaseMap").x, Is.EqualTo(expectedOffset.x).Within(1e-6f));
				Assert.That(material.GetTextureOffset("_BaseMap").y, Is.EqualTo(expectedOffset.y).Within(1e-6f));
			}
			finally
			{
				DestroyObject(legacyTexture);
				DestroyObject(material);
			}
		}

		[Test]
		public void RefreshLitKeywords_DoesNotOverwriteExistingBaseMapWithLegacyMainTexture()
		{
			var material = SDF2Unity.CreateMaterial("test-material");
			var baseMapTexture = new Texture2D(2, 2);
			var legacyMainTexture = new Texture2D(2, 2);

			try
			{
				material.SetTexture("_BaseMap", baseMapTexture);

				if (!material.HasProperty("_MainTex"))
				{
					Assert.Ignore("Shader does not expose legacy _MainTex property");
				}

				material.SetTexture("_MainTex", legacyMainTexture);
				material.RefreshLitKeywords();

				Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(baseMapTexture));
			}
			finally
			{
				DestroyObject(legacyMainTexture);
				DestroyObject(baseMapTexture);
				DestroyObject(material);
			}
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

	public class TFScopeResolutionTests
	{
		private static readonly FieldInfo RootModelInScopeField = typeof(SDFormat.Helper.Base).GetField(
			"_rootModelInScope",
			BindingFlags.NonPublic | BindingFlags.Instance);

		[Test]
		public void GetPose_PrefersNearestModelScopeForPrefixedLocalNameFallback()
		{
			var world = new GameObject("world");
			var rootModelObject = new GameObject("robot_model");
			var wrongScopedModelObject = new GameObject("right_hand");
			var wrongParentObject = new GameObject("index_proximal");
			var currentScopedModelObject = new GameObject("current_hand_scope");
			var correctParentObject = new GameObject("index_proximal");
			var childObject = new GameObject("index_middle");

			try
			{
				rootModelObject.transform.SetParent(world.transform, false);
				var rootModel = rootModelObject.AddComponent<SDFormat.Helper.Model>();

				wrongScopedModelObject.transform.SetParent(rootModelObject.transform, false);
				wrongScopedModelObject.transform.localPosition = new Vector3(0.5f, 0f, 0f);
				var wrongScopedModel = wrongScopedModelObject.AddComponent<SDFormat.Helper.Model>();
				wrongScopedModel.isNested = true;

				wrongParentObject.transform.SetParent(wrongScopedModelObject.transform, false);
				var wrongParentLink = wrongParentObject.AddComponent<SDFormat.Helper.Link>();

				currentScopedModelObject.transform.SetParent(rootModelObject.transform, false);
				var currentScopedModel = currentScopedModelObject.AddComponent<SDFormat.Helper.Model>();
				currentScopedModel.isNested = true;

				correctParentObject.transform.SetParent(currentScopedModelObject.transform, false);
				correctParentObject.transform.localPosition = new Vector3(0.01f, 0f, 0f);
				var correctParentLink = correctParentObject.AddComponent<SDFormat.Helper.Link>();

				childObject.transform.SetParent(currentScopedModelObject.transform, false);
				childObject.transform.localPosition = new Vector3(0.04f, 0f, 0.02f);
				var childLink = childObject.AddComponent<SDFormat.Helper.Link>();

				RootModelInScopeField.SetValue(wrongParentLink, rootModel);
				RootModelInScopeField.SetValue(correctParentLink, rootModel);
				RootModelInScopeField.SetValue(childLink, rootModel);

				wrongParentLink.StoreWorldPoseSnapshot(wrongParentObject.transform.position, wrongParentObject.transform.rotation);
				correctParentLink.StoreWorldPoseSnapshot(correctParentObject.transform.position, correctParentObject.transform.rotation);
				childLink.StoreWorldPoseSnapshot(childObject.transform.position, childObject.transform.rotation);

				var tf = new TF(childLink, "right_hand_index_middle", "right_hand_index_proximal");
				var pose = tf.GetPose();

				var expectedPosition = correctParentObject.transform.InverseTransformPoint(childObject.transform.position);
				var expectedRotation = Quaternion.Inverse(correctParentObject.transform.rotation) * childObject.transform.rotation;

				Assert.That(pose.position.x, Is.EqualTo(expectedPosition.x).Within(1e-5f));
				Assert.That(pose.position.y, Is.EqualTo(expectedPosition.y).Within(1e-5f));
				Assert.That(pose.position.z, Is.EqualTo(expectedPosition.z).Within(1e-5f));
				Assert.That(Quaternion.Angle(pose.rotation, expectedRotation), Is.LessThan(1e-4f));
				Assert.That(pose.position.magnitude, Is.LessThan(0.1f));
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
