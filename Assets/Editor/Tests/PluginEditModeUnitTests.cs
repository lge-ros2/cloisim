/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using NUnit.Framework;
using CLOiSim.Cloth;
using Unity.Mathematics;
using UnityEngine;

namespace CLOiSim.Tests.EditMode
{
	internal static class PluginTestReflection
	{
		private static readonly System.Type[] HandleRequestParameterTypes =
		{
			typeof(string).MakeByRefType(),
			typeof(cloisim.msgs.Any).MakeByRefType(),
			typeof(DeviceMessage).MakeByRefType()
		};

		public static MethodInfo GetHandleRequestMethod(System.Type pluginType)
		{
			return pluginType.GetMethod(
				"HandleCustomRequestMessage",
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				HandleRequestParameterTypes,
				null);
		}
	}

	public class GroundTruthPluginTests
	{
		private static readonly MethodInfo TryParsePropNameAndIdMethod = typeof(GroundTruthPlugin).GetMethod(
			"TryParsePropNameAndId",
			BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo TryCreatePropPerceptionMethod = typeof(GroundTruthPlugin).GetMethod(
			"TryCreatePropPerception",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly System.Type PropSnapshotType = typeof(GroundTruthPlugin).GetNestedType(
			"PropSnapshot",
			BindingFlags.NonPublic);
		private static readonly FieldInfo PropsClassIdField = typeof(GroundTruthPlugin).GetField(
			"_propsClassId",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo AllLoadedModelListField = typeof(GroundTruthPlugin).GetField(
			"_allLoadedModelList",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo GetTrackingObjectMethod = typeof(GroundTruthPlugin).GetMethod(
			"GetTrackingObject",
			BindingFlags.NonPublic | BindingFlags.Instance);

		[Test]
		public void TryParsePropNameAndId_ParsesValidNameAndNumericId()
		{
			var arguments = new object[] { "chair-12", null, 0 };

			var result = (bool)TryParsePropNameAndIdMethod.Invoke(null, arguments);

			Assert.That(result, Is.True);
			Assert.That(arguments[1], Is.EqualTo("chair"));
			Assert.That(arguments[2], Is.EqualTo(12));
		}

		[Test]
		public void TryParsePropNameAndId_RejectsMissingSeparator()
		{
			var arguments = new object[] { "chair12", null, 0 };

			var result = (bool)TryParsePropNameAndIdMethod.Invoke(null, arguments);

			Assert.That(result, Is.False);
			Assert.That(arguments[1], Is.EqualTo(string.Empty));
			Assert.That(arguments[2], Is.EqualTo(0));
		}

		[Test]
		public void TryParsePropNameAndId_RejectsNonNumericIdButKeepsParsedName()
		{
			var arguments = new object[] { "chair-blue", null, 0 };

			var result = (bool)TryParsePropNameAndIdMethod.Invoke(null, arguments);

			Assert.That(result, Is.False);
			Assert.That(arguments[1], Is.EqualTo("chair"));
			Assert.That(arguments[2], Is.EqualTo(0));
		}

		[Test]
		public void TryParsePropNameAndId_UsesLastSeparatorForNamesContainingHyphens()
		{
			var arguments = new object[] { "office-chair-12", null, 0 };

			var result = (bool)TryParsePropNameAndIdMethod.Invoke(null, arguments);

			Assert.That(result, Is.True);
			Assert.That(arguments[1], Is.EqualTo("office-chair"));
			Assert.That(arguments[2], Is.EqualTo(12));
		}

		[Test]
		public void TryCreatePropPerception_CreatesPerceptionForKnownProp()
		{
			var root = new GameObject("ground-truth-root");
			var plugin = root.AddComponent<GroundTruthPlugin>();
			var propsClassId = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase)
			{
				["chair"] = 7
			};
			PropsClassIdField.SetValue(plugin, propsClassId);
			var snapshot = CreatePropSnapshot("chair-12", new Vector3(1f, 2f, 3f), new Vector3(1f, 2f, 3f));

			try
			{
				var arguments = new object[] { snapshot, null };
				var result = (bool)TryCreatePropPerceptionMethod.Invoke(plugin, arguments);
				var perception = (cloisim.msgs.Perception)arguments[1];

				Assert.That(result, Is.True);
				Assert.That(perception, Is.Not.Null);
				Assert.That(perception.ClassId, Is.EqualTo(7));
				Assert.That(perception.TrackingId, Is.EqualTo("chair".GetHashCode() + 12));
				Assert.That(perception.Header, Is.Not.Null);
				Assert.That(perception.Header.Stamp, Is.Not.Null);
				Assert.That(perception.Position, Is.Not.Null);
				Assert.That(perception.Velocity, Is.Not.Null);
				Assert.That(perception.Size, Is.Not.Null);
				Assert.That(perception.Position.X, Is.EqualTo(3d).Within(1e-12d));
				Assert.That(perception.Position.Y, Is.EqualTo(-1d).Within(1e-12d));
				Assert.That(perception.Position.Z, Is.EqualTo(2d).Within(1e-12d));
				Assert.That(perception.Size.X, Is.EqualTo(3d).Within(1e-12d));
				Assert.That(perception.Size.Y, Is.EqualTo(1d).Within(1e-12d));
				Assert.That(perception.Size.Z, Is.EqualTo(2d).Within(1e-12d));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void TryCreatePropPerception_ReturnsFalseWhenPropClassIsUnknown()
		{
			var root = new GameObject("ground-truth-root");
			var plugin = root.AddComponent<GroundTruthPlugin>();
			PropsClassIdField.SetValue(
				plugin,
				new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase));
			var snapshot = CreatePropSnapshot("chair-12", Vector3.zero, Vector3.one);

			try
			{
				var arguments = new object[] { snapshot, null };
				var result = (bool)TryCreatePropPerceptionMethod.Invoke(plugin, arguments);

				Assert.That(result, Is.False);
				Assert.That(arguments[1], Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void GetTrackingObject_ReturnsMappedModelGameObject()
		{
			var root = new GameObject("ground-truth-root");
			var plugin = root.AddComponent<GroundTruthPlugin>();
			var modelObject = new GameObject("tracked-model");
			var helper = modelObject.AddComponent<SDFormat.Helper.Base>();
			var modelMap = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Base>
			{
				["tracked-model"] = helper
			};
			AllLoadedModelListField.SetValue(plugin, modelMap);

			try
			{
				var result = (GameObject)GetTrackingObjectMethod.Invoke(plugin, new object[] { "tracked-model" });

				Assert.That(result, Is.EqualTo(modelObject));
			}
			finally
			{
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(modelObject);
			}
		}

		[Test]
		public void GetTrackingObject_RemovesStaleNullEntryAndReturnsNull()
		{
			var root = new GameObject("ground-truth-root");
			var plugin = root.AddComponent<GroundTruthPlugin>();
			var modelMap = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Base>
			{
				["stale-model"] = null
			};
			AllLoadedModelListField.SetValue(plugin, modelMap);

			try
			{
				var result = (GameObject)GetTrackingObjectMethod.Invoke(plugin, new object[] { "stale-model" });

				Assert.That(result, Is.Null);
				Assert.That(modelMap.ContainsKey("stale-model"), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		private static object CreatePropSnapshot(string name, Vector3 localPosition, Vector3 localScale)
		{
			var snapshot = System.Activator.CreateInstance(PropSnapshotType);
			PropSnapshotType.GetField("name", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(snapshot, name);
			PropSnapshotType.GetField("localPosition", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(snapshot, localPosition);
			PropSnapshotType.GetField("localScale", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
				.SetValue(snapshot, localScale);
			return snapshot;
		}
	}

	public class ClothGrabberPluginTests
	{
		private static readonly MethodInfo FindInHierarchyMethod = typeof(ClothGrabberPlugin).GetMethod(
			"FindInHierarchy",
			BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo FindCollisionMeshColliderMethod = typeof(ClothGrabberPlugin).GetMethod(
			"FindCollisionMeshCollider",
			BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo ResolveTargetTransformMethod = typeof(ClothGrabberPlugin).GetMethod(
			"ResolveTargetTransform",
			BindingFlags.NonPublic | BindingFlags.Instance);

		private static MeshCollider AddTestMeshCollider(GameObject target, bool isTrigger = false)
		{
			var collider = target.AddComponent<MeshCollider>();
			collider.sharedMesh = CreateTestMesh();

			if (isTrigger)
			{
				collider.convex = true;
				collider.isTrigger = true;
			}

			return collider;
		}

		private static Mesh CreateTestMesh()
		{
			var mesh = new Mesh
			{
				vertices = new[]
				{
					new Vector3(0f, 0f, 0f),
					new Vector3(1f, 0f, 0f),
					new Vector3(0f, 1f, 0f),
					new Vector3(0f, 0f, 1f)
				},
				triangles = new[]
				{
					0, 1, 2,
					0, 3, 1,
					0, 2, 3,
					1, 3, 2
				}
			};

			mesh.RecalculateNormals();
			return mesh;
		}

		private static void DestroyTestMesh(MeshCollider collider)
		{
			if (collider?.sharedMesh != null)
			{
				Object.DestroyImmediate(collider.sharedMesh);
			}
		}

		[Test]
		public void FindInHierarchy_ReturnsRootWhenNameMatches()
		{
			var root = new GameObject("root");

			try
			{
				var result = (Transform)FindInHierarchyMethod.Invoke(null, new object[] { root.transform, "root" });

				Assert.That(result, Is.EqualTo(root.transform));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindInHierarchy_SearchesChildrenRecursively()
		{
			var root = new GameObject("root");
			var child = new GameObject("child");
			var grandChild = new GameObject("grandchild");

			child.transform.SetParent(root.transform);
			grandChild.transform.SetParent(child.transform);

			try
			{
				var result = (Transform)FindInHierarchyMethod.Invoke(null, new object[] { root.transform, "grandchild" });

				Assert.That(result, Is.EqualTo(grandChild.transform));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindInHierarchy_ReturnsNullWhenNameDoesNotExist()
		{
			var root = new GameObject("root");

			try
			{
				var result = (Transform)FindInHierarchyMethod.Invoke(null, new object[] { root.transform, "missing" });

				Assert.That(result, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindCollisionMeshCollider_ReturnsNamedColliderWhenRequested()
		{
			var root = new GameObject("root");
			var preferredChild = new GameObject("preferred");
			preferredChild.transform.SetParent(root.transform);
			var namedCollider = AddTestMeshCollider(preferredChild);

			try
			{
				var result = (MeshCollider)FindCollisionMeshColliderMethod.Invoke(null, new object[] { root.transform, "preferred" });

				Assert.That(result, Is.EqualTo(namedCollider));
			}
			finally
			{
				DestroyTestMesh(namedCollider);
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindCollisionMeshCollider_PrefersNonTriggerColliderWhenNoNameIsSpecified()
		{
			var root = new GameObject("root");
			var triggerChild = new GameObject("trigger");
			var solidChild = new GameObject("solid");
			triggerChild.transform.SetParent(root.transform);
			solidChild.transform.SetParent(root.transform);
			var triggerCollider = AddTestMeshCollider(triggerChild, isTrigger: true);
			var solidCollider = AddTestMeshCollider(solidChild);

			try
			{
				var result = (MeshCollider)FindCollisionMeshColliderMethod.Invoke(null, new object[] { root.transform, string.Empty });

				Assert.That(result, Is.EqualTo(solidCollider));
			}
			finally
			{
				DestroyTestMesh(triggerCollider);
				DestroyTestMesh(solidCollider);
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindCollisionMeshCollider_FallsBackToTriggerWhenNoSolidColliderExists()
		{
			var root = new GameObject("root");
			var child = new GameObject("trigger");
			child.transform.SetParent(root.transform);
			var triggerCollider = AddTestMeshCollider(child, isTrigger: true);

			try
			{
				var result = (MeshCollider)FindCollisionMeshColliderMethod.Invoke(null, new object[] { root.transform, string.Empty });

				Assert.That(result, Is.EqualTo(triggerCollider));
			}
			finally
			{
				DestroyTestMesh(triggerCollider);
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void FindCollisionMeshCollider_ReturnsNullWhenNoMeshColliderExists()
		{
			var root = new GameObject("root");
			var child = new GameObject("child");
			child.transform.SetParent(root.transform);

			try
			{
				var result = (MeshCollider)FindCollisionMeshColliderMethod.Invoke(null, new object[] { root.transform, string.Empty });

				Assert.That(result, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ResolveTargetTransform_ReturnsNullForEmptyTarget()
		{
			var root = new GameObject("root");
			var plugin = root.AddComponent<ClothGrabberPlugin>();

			try
			{
				var result = (Transform)ResolveTargetTransformMethod.Invoke(plugin, new object[] { string.Empty });

				Assert.That(result, Is.Null);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void ResolveTargetTransform_ResolvesLinkNameFromScopedTarget()
		{
			var root = new GameObject("root");
			var plugin = root.AddComponent<ClothGrabberPlugin>();
			var child = new GameObject("finger_link");
			child.transform.SetParent(root.transform);

			try
			{
				var scopedResult = (Transform)ResolveTargetTransformMethod.Invoke(plugin, new object[] { "robot::finger_link" });
				var plainResult = (Transform)ResolveTargetTransformMethod.Invoke(plugin, new object[] { "finger_link" });

				Assert.That(scopedResult, Is.EqualTo(child.transform));
				Assert.That(plainResult, Is.EqualTo(child.transform));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class ObjectTrackingTests
	{
		private static readonly MethodInfo Set2DFootprintMethod = typeof(ObjectTracking).GetMethod(
			"Set2DFootprint",
			BindingFlags.NonPublic | BindingFlags.Instance);

		[Test]
		public void Update_CopiesTransformPositionAndRotation()
		{
			var root = new GameObject("tracked-object");
			var expectedPosition = new Vector3(1f, 2f, 3f);
			var expectedRotation = Quaternion.Euler(10f, 20f, 30f);
			root.transform.SetPositionAndRotation(expectedPosition, expectedRotation);
			var tracking = new ObjectTracking(root);

			try
			{
				tracking.Update();

				Assert.That(tracking.Position.x, Is.EqualTo(expectedPosition.x).Within(1e-6f));
				Assert.That(tracking.Position.y, Is.EqualTo(expectedPosition.y).Within(1e-6f));
				Assert.That(tracking.Position.z, Is.EqualTo(expectedPosition.z).Within(1e-6f));
				Assert.That(Quaternion.Angle(tracking.Rotation, expectedRotation), Is.EqualTo(0f).Within(1e-6f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void UpdateVelocity_AppliesConfiguredSmoothingFactor()
		{
			var root = new GameObject("tracked-object");
			var tracking = new ObjectTracking(root);

			try
			{
				root.transform.position = new Vector3(4f, 0f, 0f);
				tracking.UpdateVelocity(2f);

				Assert.That(tracking.Velocity.x, Is.EqualTo(0.5f).Within(1e-6f));
				Assert.That(tracking.Velocity.y, Is.EqualTo(0f).Within(1e-6f));
				Assert.That(tracking.Velocity.z, Is.EqualTo(0f).Within(1e-6f));

				root.transform.position = new Vector3(8f, 0f, 0f);
				tracking.UpdateVelocity(2f);

				Assert.That(tracking.Velocity.x, Is.EqualTo(0.875f).Within(1e-6f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void Reset_ClearsStateAndUsesCurrentPositionAsNextVelocityBaseline()
		{
			var root = new GameObject("tracked-object");
			var tracking = new ObjectTracking(root);

			try
			{
				root.transform.SetPositionAndRotation(new Vector3(4f, 0f, 0f), Quaternion.Euler(0f, 45f, 0f));
				tracking.UpdateVelocity(2f);
				tracking.Update();

				root.transform.position = new Vector3(10f, 0f, 0f);
				tracking.Reset();

				Assert.That(tracking.Velocity.x, Is.EqualTo(0f).Within(1e-6f));
				Assert.That(tracking.Position.x, Is.EqualTo(0f).Within(1e-6f));
				Assert.That(tracking.Position.y, Is.EqualTo(0f).Within(1e-6f));
				Assert.That(tracking.Position.z, Is.EqualTo(0f).Within(1e-6f));
				Assert.That(Quaternion.Angle(tracking.Rotation, Quaternion.identity), Is.EqualTo(0f).Within(1e-6f));

				root.transform.position = new Vector3(14f, 0f, 0f);
				tracking.UpdateVelocity(2f);

				Assert.That(tracking.Velocity.x, Is.EqualTo(0.5f).Within(1e-6f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void Update_RotatesCachedFootprintPoints()
		{
			var root = new GameObject("tracked-object");
			var tracking = new ObjectTracking(root);
			var localFootprint = new[]
			{
				new Vector3(1f, 0f, 0f),
				new Vector3(0f, 0f, 1f)
			};
			Set2DFootprintMethod.Invoke(tracking, new object[] { localFootprint });
			var expectedRotation = Quaternion.Euler(0f, 90f, 0f);
			root.transform.rotation = expectedRotation;

			try
			{
				tracking.Update();
				var rotated = tracking.Footprint();

				Assert.That(rotated.Length, Is.EqualTo(2));
				Assert.That(Vector3.Distance(rotated[0], expectedRotation * localFootprint[0]), Is.EqualTo(0f).Within(1e-6f));
				Assert.That(Vector3.Distance(rotated[1], expectedRotation * localFootprint[1]), Is.EqualTo(0f).Within(1e-6f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void CopyFootprintTo_ReplacesDestinationContentsAndUpdatesCount()
		{
			var root = new GameObject("tracked-object");
			var tracking = new ObjectTracking(root);
			var localFootprint = new[]
			{
				new Vector3(1f, 0f, 0f),
				new Vector3(0f, 0f, 1f),
				new Vector3(-1f, 0f, 0f)
			};
			Set2DFootprintMethod.Invoke(tracking, new object[] { localFootprint });
			var destination = new System.Collections.Generic.List<Vector3> { Vector3.one * 99f };

			try
			{
				tracking.CopyFootprintTo(destination);

				Assert.That(tracking.FootprintCount, Is.EqualTo(3));
				Assert.That(destination.Count, Is.EqualTo(3));
				Assert.That(Vector3.Distance(destination[0], localFootprint[0]), Is.EqualTo(0f).Within(1e-6f));
				Assert.That(Vector3.Distance(destination[1], localFootprint[1]), Is.EqualTo(0f).Within(1e-6f));
				Assert.That(Vector3.Distance(destination[2], localFootprint[2]), Is.EqualTo(0f).Within(1e-6f));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class ClothPluginTests
	{
		private static readonly MethodInfo BuildWeldConstraintsMethod = typeof(ClothPlugin).GetMethod(
			"BuildWeldConstraints",
			BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo TryAddEdgeMethod = typeof(ClothPlugin).GetMethod(
			"TryAddEdge",
			BindingFlags.NonPublic | BindingFlags.Static);
		private static readonly MethodInfo TryAddBendingMethod = typeof(ClothPlugin).GetMethod(
			"TryAddBending",
			BindingFlags.NonPublic | BindingFlags.Static);

		[Test]
		public void BuildWeldConstraints_CreatesZeroLengthConstraintAndPropagatesPinnedMass()
		{
			var worldVerts = new[]
			{
				new float3(0f, 0f, 0f),
				new float3(0f, 0f, 0f),
				new float3(1f, 0f, 0f)
			};
			var masses = new[] { 0f, 1f, 1f };
			var arguments = new object[] { worldVerts, masses, 0 };

			var weldConstraints = (System.Collections.Generic.List<DistanceConstraint>)BuildWeldConstraintsMethod.Invoke(null, arguments);

			Assert.That((int)arguments[2], Is.EqualTo(1));
			Assert.That(weldConstraints.Count, Is.EqualTo(1));
			Assert.That(weldConstraints[0].IndexA, Is.EqualTo(0));
			Assert.That(weldConstraints[0].IndexB, Is.EqualTo(1));
			Assert.That(weldConstraints[0].RestLength, Is.EqualTo(0f).Within(1e-6f));
			Assert.That(weldConstraints[0].Stiffness, Is.EqualTo(1f).Within(1e-6f));
			Assert.That(masses[1], Is.EqualTo(0f).Within(1e-6f));
		}

		[Test]
		public void TryAddEdge_AddsSortedConstraintAndIgnoresDuplicateEdge()
		{
			var edgeSet = new System.Collections.Generic.HashSet<long>();
			var constraints = new System.Collections.Generic.List<DistanceConstraint>();
			var vertices = new[]
			{
				new float3(0f, 0f, 0f),
				new float3(3f, 4f, 0f)
			};

			TryAddEdgeMethod.Invoke(null, new object[] { edgeSet, constraints, vertices, 1, 0, 0.75f });
			TryAddEdgeMethod.Invoke(null, new object[] { edgeSet, constraints, vertices, 0, 1, 0.75f });

			Assert.That(constraints.Count, Is.EqualTo(1));
			Assert.That(constraints[0].IndexA, Is.EqualTo(0));
			Assert.That(constraints[0].IndexB, Is.EqualTo(1));
			Assert.That(constraints[0].RestLength, Is.EqualTo(5f).Within(1e-6f));
			Assert.That(constraints[0].Stiffness, Is.EqualTo(0.75f).Within(1e-6f));
		}

		[Test]
		public void TryAddBending_AddsConstraintWhenSecondTriangleSharesEdge()
		{
			var edgeToOpposite = new System.Collections.Generic.Dictionary<long, int>();
			var bendingConstraints = new System.Collections.Generic.List<BendingConstraint>();
			var vertices = new[]
			{
				new float3(0f, 0f, 0f),
				new float3(1f, 0f, 0f),
				new float3(0f, 1f, 0f),
				new float3(0f, 0f, 2f)
			};

			TryAddBendingMethod.Invoke(null, new object[] { edgeToOpposite, bendingConstraints, vertices, 1, 2, 0, 0.5f });
			TryAddBendingMethod.Invoke(null, new object[] { edgeToOpposite, bendingConstraints, vertices, 2, 1, 3, 0.5f });

			Assert.That(bendingConstraints.Count, Is.EqualTo(1));
			Assert.That(bendingConstraints[0].IndexA, Is.EqualTo(0));
			Assert.That(bendingConstraints[0].IndexB, Is.EqualTo(3));
			Assert.That(bendingConstraints[0].RestLength, Is.EqualTo(2f).Within(1e-6f));
			Assert.That(bendingConstraints[0].Stiffness, Is.EqualTo(0.5f).Within(1e-6f));
		}
	}

	public class CLOiSimPluginTests
	{
		private static readonly MethodInfo ResolvePluginParentFrameNameMethod = typeof(CLOiSimPlugin).GetMethod(
			"ResolvePluginParentFrameName",
			BindingFlags.NonPublic | BindingFlags.Static);

		[Test]
		public void ResolvePluginParentFrameName_UsesOwningLinkFrameInsteadOfParentLinkFrame()
		{
			var linkObject = new GameObject("chest_link");
			var link = linkObject.AddComponent<SDFormat.Helper.Link>();

			link.JointChildLinkName = "chest_link";
			link.JointParentLinkName = "waist_link_3";

			try
			{
				var resolvedParentFrameName = (string)ResolvePluginParentFrameNameMethod.Invoke(null, new object[] { link });

				Assert.That(resolvedParentFrameName, Is.EqualTo("chest_link"));
			}
			finally
			{
				Object.DestroyImmediate(linkObject);
			}
		}

		[Test]
		public void ResolvePluginParentFrameName_FallsBackToLinkObjectNameWhenJointChildFrameIsMissing()
		{
			var linkObject = new GameObject("sensor_mount");
			var link = linkObject.AddComponent<SDFormat.Helper.Link>();

			link.JointChildLinkName = string.Empty;
			link.JointParentLinkName = "base_link";

			try
			{
				var resolvedParentFrameName = (string)ResolvePluginParentFrameNameMethod.Invoke(null, new object[] { link });

				Assert.That(resolvedParentFrameName, Is.EqualTo("sensor_mount"));
			}
			finally
			{
				Object.DestroyImmediate(linkObject);
			}
		}
	}

	public class CameraPluginTests
	{
		private static readonly FieldInfo CameraField = typeof(CameraPlugin).GetField(
			"_cam",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo CameraSensorInfoField = typeof(SensorDevices.Camera).GetField(
			"_sensorInfo",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleCameraRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(CameraPlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesCameraInfoResponseForInfoRequest()
		{
			var root = new GameObject("camera-root");
			var sensor = root.AddComponent<SensorDevices.Camera>();
			var plugin = root.AddComponent<CameraPlugin>();
			var response = new DeviceMessage();
			var cameraInfo = new cloisim.msgs.CameraSensor
			{
				HorizontalFov = 1.1d,
				ImageSize = new cloisim.msgs.Vector2d { X = 640d, Y = 480d },
				NearClip = 0.1d,
				FarClip = 25d,
				SaveEnabled = true,
				SavePath = "captures/front"
			};

			CameraSensorInfoField.SetValue(sensor, cameraInfo);
			CameraField.SetValue(plugin, sensor);

			try
			{
				HandleCameraRequestMethod.Invoke(plugin, new object[] { "request_camera_info", new cloisim.msgs.Any(), response });
				var result = response.GetMessage<cloisim.msgs.CameraSensor>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(result.HorizontalFov, Is.EqualTo(1.1d).Within(1e-6d));
				Assert.That(result.ImageSize.X, Is.EqualTo(640d).Within(1e-6d));
				Assert.That(result.ImageSize.Y, Is.EqualTo(480d).Within(1e-6d));
				Assert.That(result.NearClip, Is.EqualTo(0.1d).Within(1e-6d));
				Assert.That(result.FarClip, Is.EqualTo(25d).Within(1e-6d));
				Assert.That(result.SaveEnabled, Is.True);
				Assert.That(result.SavePath, Is.EqualTo("captures/front"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownCameraRequestType()
		{
			var root = new GameObject("camera-root");
			var plugin = root.AddComponent<CameraPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleCameraRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class MultiCameraPluginTests
	{
		private static readonly FieldInfo MultiCameraField = typeof(MultiCameraPlugin).GetField(
			"multiCam",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo CameraSensorInfoField = typeof(SensorDevices.Camera).GetField(
			"_sensorInfo",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleMultiCameraRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(MultiCameraPlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesCameraInfoResponseForNamedCamera()
		{
			var root = new GameObject("multi-camera-root");
			var multiCamera = root.AddComponent<SensorDevices.MultiCamera>();
			var plugin = root.AddComponent<MultiCameraPlugin>();
			var child = new GameObject("front-camera");
			var response = new DeviceMessage();
			var cameraInfo = new cloisim.msgs.CameraSensor
			{
				HorizontalFov = 0.75d,
				ImageSize = new cloisim.msgs.Vector2d { X = 1280d, Y = 720d },
				NearClip = 0.2d,
				FarClip = 50d
			};

			child.transform.SetParent(root.transform, false);
			var camera = child.AddComponent<SensorDevices.Camera>();
			camera.DeviceName = "robot::front_camera";
			CameraSensorInfoField.SetValue(camera, cameraInfo);
			multiCamera.AddCamera(camera);
			MultiCameraField.SetValue(plugin, multiCamera);

			try
			{
				var request = new cloisim.msgs.Any { Type = cloisim.msgs.Any.ValueType.String, StringValue = "front_camera" };
				HandleMultiCameraRequestMethod.Invoke(plugin, new object[] { "request_camera_info", request, response });
				var result = response.GetMessage<cloisim.msgs.CameraSensor>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(result.HorizontalFov, Is.EqualTo(0.75d).Within(1e-6d));
				Assert.That(result.ImageSize.X, Is.EqualTo(1280d).Within(1e-6d));
				Assert.That(result.ImageSize.Y, Is.EqualTo(720d).Within(1e-6d));
				Assert.That(result.NearClip, Is.EqualTo(0.2d).Within(1e-6d));
				Assert.That(result.FarClip, Is.EqualTo(50d).Within(1e-6d));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresMissingMultiCameraTarget()
		{
			var root = new GameObject("multi-camera-root");
			var multiCamera = root.AddComponent<SensorDevices.MultiCamera>();
			var plugin = root.AddComponent<MultiCameraPlugin>();
			var response = new DeviceMessage();

			MultiCameraField.SetValue(plugin, multiCamera);

			try
			{
				var request = new cloisim.msgs.Any { Type = cloisim.msgs.Any.ValueType.String, StringValue = "missing_camera" };
				HandleMultiCameraRequestMethod.Invoke(plugin, new object[] { "request_camera_info", request, response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class GpsPluginTests
	{
		private class TestGps : SensorDevices.GPS
		{
			protected override void OnAwake() { }
			protected override void OnStart() { }
			protected override void OnReset() { }
		}

		private static readonly FieldInfo GpsField = typeof(GpsPlugin).GetField(
			"_gps",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentLinkNameField = typeof(CLOiSimPlugin).GetField(
			"_parentLinkName",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleGpsRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(GpsPlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesTransformResponseForGps()
		{
			var root = new GameObject("gps-root");
			var sensor = root.AddComponent<TestGps>();
			var plugin = root.AddComponent<GpsPlugin>();
			var response = new DeviceMessage();
			var localPosition = new Vector3(1f, 2f, 3f);
			var localRotation = Quaternion.Euler(10f, 20f, 30f);

			root.transform.localPosition = localPosition;
			root.transform.localRotation = localRotation;
			sensor.DeviceName = "gps_sensor";
			sensor.UpdatePose();
			GpsField.SetValue(plugin, sensor);
			ParentLinkNameField.SetValue(plugin, "base_link");

			try
			{
				HandleGpsRequestMethod.Invoke(plugin, new object[] { "request_transform", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();
				var transformValue = param.Params["transform"];
				var pose = transformValue.Pose3dValue;
				var expectedPosition = Unity2SDF.Position(localPosition);
				var expectedRotation = Unity2SDF.Rotation(localRotation);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(transformValue.Type, Is.EqualTo(cloisim.msgs.Any.ValueType.Pose3d));
				Assert.That(pose.Name, Is.EqualTo("gps_sensor"));
				Assert.That(pose.Position.X, Is.EqualTo(expectedPosition.X).Within(1e-6d));
				Assert.That(pose.Position.Y, Is.EqualTo(expectedPosition.Y).Within(1e-6d));
				Assert.That(pose.Position.Z, Is.EqualTo(expectedPosition.Z).Within(1e-6d));
				Assert.That(pose.Orientation.X, Is.EqualTo(expectedRotation.X).Within(1e-6d));
				Assert.That(pose.Orientation.Y, Is.EqualTo(expectedRotation.Y).Within(1e-6d));
				Assert.That(pose.Orientation.Z, Is.EqualTo(expectedRotation.Z).Within(1e-6d));
				Assert.That(pose.Orientation.W, Is.EqualTo(expectedRotation.W).Within(1e-6d));
				Assert.That(param.Childrens.Count, Is.EqualTo(1));
				Assert.That(param.Childrens[0].Params["parent_frame_id"].StringValue, Is.EqualTo("base_link"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownGpsRequestType()
		{
			var root = new GameObject("gps-root");
			var plugin = root.AddComponent<GpsPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleGpsRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class RangePluginTests
	{
		private class TestSonar : SensorDevices.Sonar
		{
			protected override void OnAwake() { }
			protected override void OnStart() { }
			protected override void OnReset() { }
		}

		private static readonly FieldInfo SonarField = typeof(RangePlugin).GetField(
			"_sonar",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentLinkNameField = typeof(CLOiSimPlugin).GetField(
			"_parentLinkName",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleRangeRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(RangePlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesTransformResponseForRangeSensor()
		{
			var root = new GameObject("range-root");
			var sensor = root.AddComponent<TestSonar>();
			var plugin = root.AddComponent<RangePlugin>();
			var response = new DeviceMessage();
			var localPosition = new Vector3(-2f, 0.5f, 4f);
			var localRotation = Quaternion.Euler(-15f, 40f, 5f);

			root.transform.localPosition = localPosition;
			root.transform.localRotation = localRotation;
			sensor.DeviceName = "range_sensor";
			sensor.UpdatePose();
			SonarField.SetValue(plugin, sensor);
			ParentLinkNameField.SetValue(plugin, "sensor_mount");

			try
			{
				HandleRangeRequestMethod.Invoke(plugin, new object[] { "request_transform", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();
				var transformValue = param.Params["transform"];
				var pose = transformValue.Pose3dValue;
				var expectedPosition = Unity2SDF.Position(localPosition);
				var expectedRotation = Unity2SDF.Rotation(localRotation);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(transformValue.Type, Is.EqualTo(cloisim.msgs.Any.ValueType.Pose3d));
				Assert.That(pose.Name, Is.EqualTo("range_sensor"));
				Assert.That(pose.Position.X, Is.EqualTo(expectedPosition.X).Within(1e-6d));
				Assert.That(pose.Position.Y, Is.EqualTo(expectedPosition.Y).Within(1e-6d));
				Assert.That(pose.Position.Z, Is.EqualTo(expectedPosition.Z).Within(1e-6d));
				Assert.That(pose.Orientation.X, Is.EqualTo(expectedRotation.X).Within(1e-6d));
				Assert.That(pose.Orientation.Y, Is.EqualTo(expectedRotation.Y).Within(1e-6d));
				Assert.That(pose.Orientation.Z, Is.EqualTo(expectedRotation.Z).Within(1e-6d));
				Assert.That(pose.Orientation.W, Is.EqualTo(expectedRotation.W).Within(1e-6d));
				Assert.That(param.Childrens.Count, Is.EqualTo(1));
				Assert.That(param.Childrens[0].Params["parent_frame_id"].StringValue, Is.EqualTo("sensor_mount"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownRangeRequestType()
		{
			var root = new GameObject("range-root");
			var plugin = root.AddComponent<RangePlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleRangeRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class LaserPluginTests
	{
		private class TestLidar : SensorDevices.Lidar
		{
			protected override void OnAwake() { }
			protected override void OnStart() { }
		}

		private static readonly FieldInfo LidarField = typeof(LaserPlugin).GetField(
			"_lidar",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentLinkNameField = typeof(CLOiSimPlugin).GetField(
			"_parentLinkName",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleLaserRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(LaserPlugin));

		private static SDFormat.Plugin CreatePluginParameters(string outputType)
		{
			var pluginParameters = new SDFormat.Plugin("libLaserPlugin.so", "laser_plugin");
			var pluginElement = pluginParameters.ToElement();
			var outputTypeElement = pluginElement.AddElement("output_type");
			outputTypeElement.AddValue("string", string.Empty, false);
			outputTypeElement.GetValue()?.SetFromString(outputType);
			pluginParameters.Element = pluginElement;

			return pluginParameters;
		}

		[Test]
		public void HandleCustomRequestMessage_PopulatesOutputTypeResponseForLaser()
		{
			var root = new GameObject("laser-root");
			var plugin = root.AddComponent<LaserPlugin>();
			var response = new DeviceMessage();

			plugin.SetPluginParameters(CreatePluginParameters("PointCloudPacked"));

			try
			{
				HandleLaserRequestMethod.Invoke(plugin, new object[] { "request_output_type", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(param.Params["output_type"].Type, Is.EqualTo(cloisim.msgs.Any.ValueType.String));
				Assert.That(param.Params["output_type"].StringValue, Is.EqualTo("PointCloudPacked"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_PopulatesTransformResponseForLaser()
		{
			var root = new GameObject("laser-root");
			var sensor = root.AddComponent<TestLidar>();
			var plugin = root.AddComponent<LaserPlugin>();
			var response = new DeviceMessage();
			var localPosition = new Vector3(2f, -1f, 5f);
			var localRotation = Quaternion.Euler(5f, -35f, 12f);

			root.transform.localPosition = localPosition;
			root.transform.localRotation = localRotation;
			sensor.DeviceName = "laser_sensor";
			sensor.UpdatePose();
			LidarField.SetValue(plugin, sensor);
			ParentLinkNameField.SetValue(plugin, "laser_mount");

			try
			{
				HandleLaserRequestMethod.Invoke(plugin, new object[] { "request_transform", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();
				var transformValue = param.Params["transform"];
				var pose = transformValue.Pose3dValue;
				var expectedPosition = Unity2SDF.Position(localPosition);
				var expectedRotation = Unity2SDF.Rotation(localRotation);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(transformValue.Type, Is.EqualTo(cloisim.msgs.Any.ValueType.Pose3d));
				Assert.That(pose.Name, Is.EqualTo("laser_sensor"));
				Assert.That(pose.Position.X, Is.EqualTo(expectedPosition.X).Within(1e-6d));
				Assert.That(pose.Position.Y, Is.EqualTo(expectedPosition.Y).Within(1e-6d));
				Assert.That(pose.Position.Z, Is.EqualTo(expectedPosition.Z).Within(1e-6d));
				Assert.That(pose.Orientation.X, Is.EqualTo(expectedRotation.X).Within(1e-6d));
				Assert.That(pose.Orientation.Y, Is.EqualTo(expectedRotation.Y).Within(1e-6d));
				Assert.That(pose.Orientation.Z, Is.EqualTo(expectedRotation.Z).Within(1e-6d));
				Assert.That(pose.Orientation.W, Is.EqualTo(expectedRotation.W).Within(1e-6d));
				Assert.That(param.Childrens.Count, Is.EqualTo(1));
				Assert.That(param.Childrens[0].Params["parent_frame_id"].StringValue, Is.EqualTo("laser_mount"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownLaserRequestType()
		{
			var root = new GameObject("laser-root");
			var plugin = root.AddComponent<LaserPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleLaserRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class ImuPluginTests
	{
		private class TestImu : SensorDevices.IMU
		{
			protected override void OnAwake() { }
			protected override void OnStart() { }
			protected override void OnReset() { }
		}

		private static readonly FieldInfo ImuField = typeof(ImuPlugin).GetField(
			"imu",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentLinkNameField = typeof(CLOiSimPlugin).GetField(
			"_parentLinkName",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleImuRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(ImuPlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesTransformResponseForImu()
		{
			var root = new GameObject("imu-root");
			var sensor = root.AddComponent<TestImu>();
			var plugin = root.AddComponent<ImuPlugin>();
			var response = new DeviceMessage();
			var localPosition = new Vector3(0.25f, 1.5f, -2f);
			var localRotation = Quaternion.Euler(-20f, 15f, 45f);

			root.transform.localPosition = localPosition;
			root.transform.localRotation = localRotation;
			sensor.DeviceName = "imu_sensor";
			sensor.UpdatePose();
			ImuField.SetValue(plugin, sensor);
			ParentLinkNameField.SetValue(plugin, "imu_link");

			try
			{
				HandleImuRequestMethod.Invoke(plugin, new object[] { "request_transform", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();
				var transformValue = param.Params["transform"];
				var pose = transformValue.Pose3dValue;
				var expectedPosition = Unity2SDF.Position(localPosition);
				var expectedRotation = Unity2SDF.Rotation(localRotation);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(transformValue.Type, Is.EqualTo(cloisim.msgs.Any.ValueType.Pose3d));
				Assert.That(pose.Name, Is.EqualTo("imu_sensor"));
				Assert.That(pose.Position.X, Is.EqualTo(expectedPosition.X).Within(1e-6d));
				Assert.That(pose.Position.Y, Is.EqualTo(expectedPosition.Y).Within(1e-6d));
				Assert.That(pose.Position.Z, Is.EqualTo(expectedPosition.Z).Within(1e-6d));
				Assert.That(pose.Orientation.X, Is.EqualTo(expectedRotation.X).Within(1e-6d));
				Assert.That(pose.Orientation.Y, Is.EqualTo(expectedRotation.Y).Within(1e-6d));
				Assert.That(pose.Orientation.Z, Is.EqualTo(expectedRotation.Z).Within(1e-6d));
				Assert.That(pose.Orientation.W, Is.EqualTo(expectedRotation.W).Within(1e-6d));
				Assert.That(param.Childrens.Count, Is.EqualTo(1));
				Assert.That(param.Childrens[0].Params["parent_frame_id"].StringValue, Is.EqualTo("imu_link"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownImuRequestType()
		{
			var root = new GameObject("imu-root");
			var plugin = root.AddComponent<ImuPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleImuRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class ContactPluginTests
	{
		private class TestContact : SensorDevices.Contact
		{
			protected override void OnAwake() { }
		}

		private static readonly FieldInfo ContactField = typeof(ContactPlugin).GetField(
			"_contact",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentLinkNameField = typeof(CLOiSimPlugin).GetField(
			"_parentLinkName",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleContactRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(ContactPlugin));

		[Test]
		public void HandleCustomRequestMessage_PopulatesTransformResponseForContactSensor()
		{
			var root = new GameObject("contact-root");
			var sensor = root.AddComponent<TestContact>();
			var plugin = root.AddComponent<ContactPlugin>();
			var response = new DeviceMessage();
			var localPosition = new Vector3(-0.5f, 0.75f, 1.25f);
			var localRotation = Quaternion.Euler(30f, 0f, -25f);

			root.transform.localPosition = localPosition;
			root.transform.localRotation = localRotation;
			sensor.DeviceName = "contact_sensor";
			sensor.UpdatePose();
			ContactField.SetValue(plugin, sensor);
			ParentLinkNameField.SetValue(plugin, "bumper_link");

			try
			{
				HandleContactRequestMethod.Invoke(plugin, new object[] { "request_transform", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();
				var transformValue = param.Params["transform"];
				var pose = transformValue.Pose3dValue;
				var expectedPosition = Unity2SDF.Position(localPosition);
				var expectedRotation = Unity2SDF.Rotation(localRotation);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(transformValue.Type, Is.EqualTo(cloisim.msgs.Any.ValueType.Pose3d));
				Assert.That(pose.Name, Is.EqualTo("contact_sensor"));
				Assert.That(pose.Position.X, Is.EqualTo(expectedPosition.X).Within(1e-6d));
				Assert.That(pose.Position.Y, Is.EqualTo(expectedPosition.Y).Within(1e-6d));
				Assert.That(pose.Position.Z, Is.EqualTo(expectedPosition.Z).Within(1e-6d));
				Assert.That(pose.Orientation.X, Is.EqualTo(expectedRotation.X).Within(1e-6d));
				Assert.That(pose.Orientation.Y, Is.EqualTo(expectedRotation.Y).Within(1e-6d));
				Assert.That(pose.Orientation.Z, Is.EqualTo(expectedRotation.Z).Within(1e-6d));
				Assert.That(pose.Orientation.W, Is.EqualTo(expectedRotation.W).Within(1e-6d));
				Assert.That(param.Childrens.Count, Is.EqualTo(1));
				Assert.That(param.Childrens[0].Params["parent_frame_id"].StringValue, Is.EqualTo("bumper_link"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownContactRequestType()
		{
			var root = new GameObject("contact-root");
			var plugin = root.AddComponent<ContactPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleContactRequestMethod.Invoke(plugin, new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class LogicalCameraPluginTests
	{
		private static readonly FieldInfo LogicalCameraField = typeof(LogicalCameraPlugin).GetField(
			"_logicalCamera",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo SetLogicalCameraSensorInfoResponseMethod = typeof(LogicalCameraPlugin).GetMethod(
			"SetLogicalCameraSensorInfoResponse",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleLogicalCameraRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(LogicalCameraPlugin));

		[Test]
		public void SetLogicalCameraSensorInfoResponse_WritesCurrentSensorSettings()
		{
			var root = new GameObject("logical-camera-root");
			var sensor = root.AddComponent<SensorDevices.LogicalCamera>();
			var plugin = root.AddComponent<LogicalCameraPlugin>();
			var response = new DeviceMessage();

			sensor.Near = 0.15f;
			sensor.Far = 12.5f;
			sensor.HorizontalFov = 1.2f;
			sensor.AspectRatio = 1.7777778f;
			LogicalCameraField.SetValue(plugin, sensor);

			try
			{
				SetLogicalCameraSensorInfoResponseMethod.Invoke(plugin, new object[] { response });
				var sensorInfo = response.GetMessage<cloisim.msgs.LogicalCameraSensor>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(sensorInfo, Is.Not.Null);
				Assert.That(sensorInfo.NearClip, Is.EqualTo(0.15d).Within(1e-6d));
				Assert.That(sensorInfo.FarClip, Is.EqualTo(12.5d).Within(1e-6d));
				Assert.That(sensorInfo.HorizontalFov, Is.EqualTo(1.2d).Within(1e-6d));
				Assert.That(sensorInfo.AspectRatio, Is.EqualTo(1.7777778d).Within(1e-6d));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_PopulatesLogicalCameraResponseForInfoRequest()
		{
			var root = new GameObject("logical-camera-root");
			var sensor = root.AddComponent<SensorDevices.LogicalCamera>();
			var plugin = root.AddComponent<LogicalCameraPlugin>();
			var response = new DeviceMessage();

			sensor.Near = 0.2f;
			sensor.Far = 30f;
			sensor.HorizontalFov = 0.75f;
			sensor.AspectRatio = 1.3333334f;
			LogicalCameraField.SetValue(plugin, sensor);

			try
			{
				HandleLogicalCameraRequestMethod.Invoke(
					plugin,
					new object[] { "request_logical_camera", new cloisim.msgs.Any(), response });
				var sensorInfo = response.GetMessage<cloisim.msgs.LogicalCameraSensor>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(sensorInfo.NearClip, Is.EqualTo(0.2d).Within(1e-6d));
				Assert.That(sensorInfo.FarClip, Is.EqualTo(30d).Within(1e-6d));
				Assert.That(sensorInfo.HorizontalFov, Is.EqualTo(0.75d).Within(1e-6d));
				Assert.That(sensorInfo.AspectRatio, Is.EqualTo(1.3333334d).Within(1e-6d));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresLogicalCameraUnknownRequestType()
		{
			var root = new GameObject("logical-camera-root");
			var plugin = root.AddComponent<LogicalCameraPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleLogicalCameraRequestMethod.Invoke(
					plugin,
					new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class RealSensePluginTests
	{
		private static readonly FieldInfo ActivatedModulesField = typeof(RealSensePlugin).GetField(
			"_activatedModules",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo SetModuleListInfoResponseMethod = typeof(RealSensePlugin).GetMethod(
			"SetModuleListInfoResponse",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleRealSenseRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(RealSensePlugin));

		[Test]
		public void SetModuleListInfoResponse_WritesActivatedModulesInOrder()
		{
			var root = new GameObject("realsense-root");
			var plugin = root.AddComponent<RealSensePlugin>();
			var response = new DeviceMessage();
			ActivatedModulesField.SetValue(
				plugin,
				new System.Collections.Generic.List<System.Tuple<string, string>>
				{
					new("camera", "color"),
					new("imu", "motion_module")
				});

			try
			{
				SetModuleListInfoResponseMethod.Invoke(plugin, new object[] { response });
				var modulesInfo = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(modulesInfo.Params.ContainsKey("activated_modules"), Is.True);
				Assert.That(modulesInfo.Params["activated_modules"].Type, Is.EqualTo(cloisim.msgs.Any.ValueType.None));
				Assert.That(modulesInfo.Childrens.Count, Is.EqualTo(2));
				Assert.That(modulesInfo.Childrens[0].Childrens[0].Params["type"].StringValue, Is.EqualTo("camera"));
				Assert.That(modulesInfo.Childrens[0].Childrens[1].Params["name"].StringValue, Is.EqualTo("color"));
				Assert.That(modulesInfo.Childrens[1].Childrens[0].Params["type"].StringValue, Is.EqualTo("imu"));
				Assert.That(modulesInfo.Childrens[1].Childrens[1].Params["name"].StringValue, Is.EqualTo("motion_module"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_PopulatesModuleListResponseForRequestModuleList()
		{
			var root = new GameObject("realsense-root");
			var plugin = root.AddComponent<RealSensePlugin>();
			var response = new DeviceMessage();
			ActivatedModulesField.SetValue(
				plugin,
				new System.Collections.Generic.List<System.Tuple<string, string>>
				{
					new("camera", "depth")
				});

			try
			{
				HandleRealSenseRequestMethod.Invoke(
					plugin,
					new object[] { "request_module_list", new cloisim.msgs.Any(), response });
				var modulesInfo = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(modulesInfo.Childrens.Count, Is.EqualTo(1));
				Assert.That(modulesInfo.Childrens[0].Childrens[0].Params["type"].StringValue, Is.EqualTo("camera"));
				Assert.That(modulesInfo.Childrens[0].Childrens[1].Params["name"].StringValue, Is.EqualTo("depth"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownRealSenseRequestType()
		{
			var root = new GameObject("realsense-root");
			var plugin = root.AddComponent<RealSensePlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleRealSenseRequestMethod.Invoke(
					plugin,
					new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class ActorControlPluginTests
	{
		private class TestActorControlPlugin : ActorControlPlugin
		{
			protected override void OnAwake() { }

			protected override System.Collections.IEnumerator OnStart()
			{
				yield break;
			}
		}

		private static readonly FieldInfo IsReceivedRequestField = typeof(ActorControlPlugin).GetField(
			"isReceivedRequest",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo TargetActorField = typeof(ActorControlPlugin).GetField(
			"targetActor",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo TargetDestinationField = typeof(ActorControlPlugin).GetField(
			"targetDestination",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleActorControlRequestMethod = PluginTestReflection.GetHandleRequestMethod(typeof(ActorControlPlugin));

		[Test]
		public void HandleCustomRequestMessage_StoresConvertedDestinationForKnownActor()
		{
			var root = new GameObject("actor-control-root");
			var actorObject = new GameObject("walker");
			actorObject.AddComponent<Animation>();
			var actor = actorObject.AddComponent<SDFormat.Helper.Actor>();
			var plugin = root.AddComponent<TestActorControlPlugin>();
			var response = new DeviceMessage();
			var request = new cloisim.msgs.Any
			{
				Type = cloisim.msgs.Any.ValueType.Vector3d,
				Vector3dValue = new cloisim.msgs.Vector3d { X = 1d, Y = 2d, Z = 3d }
			};
			var expectedDestination = SDF2Unity.Position(1d, 2d, 3d);
			ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>
			{
				["walker"] = actor
			};

			try
			{
				HandleActorControlRequestMethod.Invoke(plugin, new object[] { "walker", request, response });
				var result = response.GetMessage<cloisim.msgs.Param>();
				var targetDestination = (Vector3)TargetDestinationField.GetValue(plugin);

				Assert.That(response.IsValid(), Is.True);
				Assert.That(result.Params["result"].BoolValue, Is.True);
				Assert.That(TargetActorField.GetValue(plugin), Is.EqualTo(actor));
				Assert.That((bool)IsReceivedRequestField.GetValue(plugin), Is.True);
				Assert.That(targetDestination.x, Is.EqualTo(expectedDestination.x).Within(1e-6f));
				Assert.That(targetDestination.y, Is.EqualTo(expectedDestination.y).Within(1e-6f));
				Assert.That(targetDestination.z, Is.EqualTo(expectedDestination.z).Within(1e-6f));
			}
			finally
			{
				ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>();
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(actorObject);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_ReturnsFalseForUnknownActor()
		{
			var root = new GameObject("actor-control-root");
			var plugin = root.AddComponent<TestActorControlPlugin>();
			var response = new DeviceMessage();
			var request = new cloisim.msgs.Any
			{
				Type = cloisim.msgs.Any.ValueType.Vector3d,
				Vector3dValue = new cloisim.msgs.Vector3d { X = 5d, Y = 6d, Z = 7d }
			};
			ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>();

			try
			{
				HandleActorControlRequestMethod.Invoke(plugin, new object[] { "missing_actor", request, response });
				var result = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(result.Params["result"].BoolValue, Is.False);
				Assert.That(TargetActorField.GetValue(plugin), Is.Null);
				Assert.That((bool)IsReceivedRequestField.GetValue(plugin), Is.False);
			}
			finally
			{
				ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>();
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_ReturnsFalseWhenRequestPayloadIsMissing()
		{
			var root = new GameObject("actor-control-root");
			var actorObject = new GameObject("walker");
			actorObject.AddComponent<Animation>();
			var actor = actorObject.AddComponent<SDFormat.Helper.Actor>();
			var plugin = root.AddComponent<TestActorControlPlugin>();
			var response = new DeviceMessage();
			ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>
			{
				["walker"] = actor
			};

			try
			{
				HandleActorControlRequestMethod.Invoke(plugin, new object[] { "walker", null, response });
				var result = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(result.Params["result"].BoolValue, Is.False);
				Assert.That(TargetActorField.GetValue(plugin), Is.Null);
				Assert.That((bool)IsReceivedRequestField.GetValue(plugin), Is.False);
			}
			finally
			{
				ActorControlPlugin.actorList = new System.Collections.Generic.Dictionary<string, SDFormat.Helper.Actor>();
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(actorObject);
			}
		}
	}

	public class JointControlPluginTests
	{
		private static readonly FieldInfo RobotDescriptionField = typeof(JointControlPlugin).GetField(
			"_robotDescription",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo SetRobotDescriptionMethod = typeof(JointControlPlugin).GetMethod(
			"SetRobotDescription",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly MethodInfo HandleCustomRequestMessageMethod = PluginTestReflection.GetHandleRequestMethod(typeof(JointControlPlugin));

		[Test]
		public void SetRobotDescription_WritesDescriptionParamIntoDeviceMessage()
		{
			var root = new GameObject("joint-control-root");
			var plugin = root.AddComponent<JointControlPlugin>();
			RobotDescriptionField.SetValue(plugin, "<sdf><model name='robot'/></sdf>");
			var response = new DeviceMessage();

			try
			{
				SetRobotDescriptionMethod.Invoke(plugin, new object[] { response });
				var param = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(param, Is.Not.Null);
				Assert.That(param.Params.ContainsKey("description"), Is.True);
				Assert.That(param.Params["description"].Type, Is.EqualTo(cloisim.msgs.Any.ValueType.String));
				Assert.That(param.Params["description"].StringValue, Is.EqualTo("<sdf><model name='robot'/></sdf>"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_PopulatesResponseForRobotDescriptionRequest()
		{
			var root = new GameObject("joint-control-root");
			var plugin = root.AddComponent<JointControlPlugin>();
			RobotDescriptionField.SetValue(plugin, "robot-description-body");
			var response = new DeviceMessage();

			try
			{
				HandleCustomRequestMessageMethod.Invoke(
					plugin,
					new object[] { "robot_description", new cloisim.msgs.Any(), response });
				var param = response.GetMessage<cloisim.msgs.Param>();

				Assert.That(response.IsValid(), Is.True);
				Assert.That(param.Params["description"].StringValue, Is.EqualTo("robot-description-body"));
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}

		[Test]
		public void HandleCustomRequestMessage_IgnoresUnknownRequestType()
		{
			var root = new GameObject("joint-control-root");
			var plugin = root.AddComponent<JointControlPlugin>();
			var response = new DeviceMessage();

			try
			{
				HandleCustomRequestMessageMethod.Invoke(
					plugin,
					new object[] { "unknown_request", new cloisim.msgs.Any(), response });

				Assert.That(response.IsValid(), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(root);
			}
		}
	}

	public class MicomPluginTests
	{
		private class TestMicomPlugin : MicomPlugin
		{
			protected override void OnAwake() { }

			protected override System.Collections.IEnumerator OnStart()
			{
				yield break;
			}
		}

		private static readonly MethodInfo LoadStaticTfMethod = typeof(MicomPlugin).GetMethod(
			"LoadStaticTF",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo LinkHelperInChildrenField = typeof(MicomPlugin).GetField(
			"_linkHelperInChildren",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo StaticTfListField = typeof(CLOiSimPlugin).GetField(
			"_staticTfList",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo ParentModelHelperField = typeof(SDFormat.Helper.Link).GetField(
			"_parentModelHelper",
			BindingFlags.NonPublic | BindingFlags.Instance);
		private static readonly FieldInfo RootModelInScopeField = typeof(SDFormat.Helper.Base).GetField(
			"_rootModelInScope",
			BindingFlags.NonPublic | BindingFlags.Instance);

		[Test]
		public void LoadStaticTF_PrefersExplicitStaticTransformOverAutoDuplicateByDefault()
		{
			var pluginRoot = new GameObject("micom-root");
			var plugin = pluginRoot.AddComponent<TestMicomPlugin>();
			var (articulationRoot, linkHelper) = CreateFixedJointLink("fixed_link", "joint_parent");

			plugin.SetPluginParameters(CreatePluginParameters(explicitStaticLink: "fixed_link", explicitStaticParentFrameId: "explicit_parent"));
			LinkHelperInChildrenField.SetValue(plugin, new[] { linkHelper });

			try
			{
				LoadStaticTfMethod.Invoke(plugin, null);
				var staticTfList = GetStaticTfList(plugin);

				Assert.That(staticTfList.Count, Is.EqualTo(1));
				Assert.That(staticTfList[0].ChildFrameID, Is.EqualTo("fixed_link"));
				Assert.That(staticTfList[0].ParentFrameID, Is.EqualTo("explicit_parent"));
			}
			finally
			{
				Object.DestroyImmediate(pluginRoot);
				Object.DestroyImmediate(articulationRoot);
			}
		}

		[Test]
		public void LoadStaticTF_SkipsAutoStaticTransformWhenDynamicTfIsExplicitlyConfigured()
		{
			var pluginRoot = new GameObject("micom-root");
			var plugin = pluginRoot.AddComponent<TestMicomPlugin>();
			var (articulationRoot, linkHelper) = CreateFixedJointLink("fixed_link", "joint_parent");

			plugin.SetPluginParameters(CreatePluginParameters(explicitDynamicLink: "fixed_link"));
			LinkHelperInChildrenField.SetValue(plugin, new[] { linkHelper });

			try
			{
				LoadStaticTfMethod.Invoke(plugin, null);
				var staticTfList = GetStaticTfList(plugin);

				Assert.That(staticTfList, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(pluginRoot);
				Object.DestroyImmediate(articulationRoot);
			}
		}

		[Test]
		public void LoadStaticTF_SkipsAutoStaticTransformWhenChildAndParentFramesMatch()
		{
			var pluginRoot = new GameObject("micom-root");
			var plugin = pluginRoot.AddComponent<TestMicomPlugin>();
			var (articulationRoot, linkHelper) = CreateFixedJointLink("base_link", "base_link");

			plugin.SetPluginParameters(CreatePluginParameters());
			LinkHelperInChildrenField.SetValue(plugin, new[] { linkHelper });

			try
			{
				LoadStaticTfMethod.Invoke(plugin, null);
				var staticTfList = GetStaticTfList(plugin);

				Assert.That(staticTfList, Is.Empty);
			}
			finally
			{
				Object.DestroyImmediate(pluginRoot);
				Object.DestroyImmediate(articulationRoot);
			}
		}

		private static System.Collections.Generic.List<TF> GetStaticTfList(CLOiSimPlugin plugin)
		{
			return (System.Collections.Generic.List<TF>)StaticTfListField.GetValue(plugin);
		}

		private static SDFormat.Plugin CreatePluginParameters(
			string explicitStaticLink = null,
			string explicitStaticParentFrameId = "base_link",
			string explicitDynamicLink = null)
		{
			var pluginParameters = new SDFormat.Plugin("libMicomPlugin.so", "micom_plugin");
			var pluginElement = pluginParameters.ToElement();
			var ros2Element = pluginElement.AddElement("ros2");
			var staticTransformsElement = ros2Element.AddElement("static_transforms");

			if (!string.IsNullOrEmpty(explicitStaticLink))
			{
				var staticLinkElement = staticTransformsElement.AddElement("link");
				staticLinkElement.AddAttribute("parent_frame_id", "string", "base_link", false);
				staticLinkElement.GetAttribute("parent_frame_id")?.SetFromString(explicitStaticParentFrameId);
				staticLinkElement.AddValue("string", string.Empty, false);
				staticLinkElement.GetValue()?.SetFromString(explicitStaticLink);
			}

			if (!string.IsNullOrEmpty(explicitDynamicLink))
			{
				var transformsElement = ros2Element.AddElement("transforms");
				var dynamicLinkElement = transformsElement.AddElement("link");
				dynamicLinkElement.AddValue("string", string.Empty, false);
				dynamicLinkElement.GetValue()?.SetFromString(explicitDynamicLink);
			}

			pluginParameters.Element = pluginElement;
			return pluginParameters;
		}

		private static (GameObject articulationRoot, SDFormat.Helper.Link linkHelper) CreateFixedJointLink(string childFrameId, string parentFrameId)
		{
			var articulationRoot = new GameObject("articulation-root");
			articulationRoot.AddComponent<ArticulationBody>();

			var articulationChild = new GameObject("articulation-child");
			articulationChild.transform.SetParent(articulationRoot.transform);
			var articulationBody = articulationChild.AddComponent<ArticulationBody>();
			articulationBody.jointType = ArticulationJointType.FixedJoint;
			articulationChild.AddComponent<SDFormat.Helper.Model>();

			var linkObject = new GameObject(childFrameId);
			linkObject.transform.SetParent(articulationChild.transform);
			var linkHelper = linkObject.AddComponent<SDFormat.Helper.Link>();
			linkHelper.JointChildLinkName = childFrameId;
			linkHelper.JointParentLinkName = parentFrameId;

			return (articulationRoot, linkHelper);
		}

		private static (GameObject rootObject, SDFormat.Helper.Link linkHelper) CreateNestedFixedJointLink(
			string nestedModelName,
			string childFrameId,
			string parentFrameId)
		{
			var rootObject = new GameObject($"{nestedModelName}-root");
			var rootModel = rootObject.AddComponent<SDFormat.Helper.Model>();
			rootObject.AddComponent<ArticulationBody>();

			var articulationChild = new GameObject($"{nestedModelName}-joint");
			articulationChild.transform.SetParent(rootObject.transform);
			var articulationBody = articulationChild.AddComponent<ArticulationBody>();
			articulationBody.jointType = ArticulationJointType.FixedJoint;

			var nestedModelObject = new GameObject(nestedModelName);
			nestedModelObject.transform.SetParent(articulationChild.transform);
			var nestedModel = nestedModelObject.AddComponent<SDFormat.Helper.Model>();

			var linkObject = new GameObject(childFrameId);
			linkObject.transform.SetParent(nestedModelObject.transform);
			var linkHelper = linkObject.AddComponent<SDFormat.Helper.Link>();
			linkHelper.JointChildLinkName = childFrameId;
			linkHelper.JointParentLinkName = parentFrameId;

			ParentModelHelperField.SetValue(linkHelper, nestedModel);
			RootModelInScopeField.SetValue(linkHelper, rootModel);

			return (rootObject, linkHelper);
		}
	}

	public class TFTests
	{
		private static readonly FieldInfo RootModelInScopeField = typeof(SDFormat.Helper.Base).GetField(
			"_rootModelInScope",
			BindingFlags.NonPublic | BindingFlags.Instance);

		[Test]
		public void GetPose_UsesConfiguredParentFrameTransform()
		{
			var rootObject = new GameObject("robot_model");
			var rootModel = rootObject.AddComponent<SDFormat.Helper.Model>();

			var baseLinkObject = new GameObject("base_link");
			baseLinkObject.transform.SetParent(rootObject.transform, false);
			baseLinkObject.transform.localPosition = new Vector3(1.2f, -0.4f, 0.8f);
			baseLinkObject.transform.localRotation = Quaternion.Euler(0f, 35f, 0f);
			var baseLink = baseLinkObject.AddComponent<SDFormat.Helper.Link>();

			var bodyBaseLinkObject = new GameObject("body_base_link");
			bodyBaseLinkObject.transform.SetParent(baseLinkObject.transform, false);
			bodyBaseLinkObject.transform.localPosition = new Vector3(0.3f, 0.1f, -0.25f);
			bodyBaseLinkObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
			var bodyBaseLink = bodyBaseLinkObject.AddComponent<SDFormat.Helper.Link>();

			var wheelLinkObject = new GameObject("left_wheel_link");
			wheelLinkObject.transform.SetParent(bodyBaseLinkObject.transform, false);
			wheelLinkObject.transform.localPosition = new Vector3(0.15f, -0.22f, 0.41f);
			wheelLinkObject.transform.localRotation = Quaternion.Euler(22f, -17f, 61f);
			var wheelLink = wheelLinkObject.AddComponent<SDFormat.Helper.Link>();

			RootModelInScopeField.SetValue(baseLink, rootModel);
			RootModelInScopeField.SetValue(bodyBaseLink, rootModel);
			RootModelInScopeField.SetValue(wheelLink, rootModel);

			rootModel.StoreWorldPoseSnapshot(rootObject.transform.position, rootObject.transform.rotation);
			baseLink.StoreWorldPoseSnapshot(baseLinkObject.transform.position, baseLinkObject.transform.rotation);
			bodyBaseLink.StoreWorldPoseSnapshot(bodyBaseLinkObject.transform.position, bodyBaseLinkObject.transform.rotation);
			wheelLink.StoreWorldPoseSnapshot(wheelLinkObject.transform.position, wheelLinkObject.transform.rotation);

			try
			{
				var tf = new TF(wheelLink, "left_wheel_link", "base_link");

				var pose = tf.GetPose();
				var expectedPosition = baseLinkObject.transform.InverseTransformPoint(wheelLinkObject.transform.position);
				var expectedRotation = Quaternion.Inverse(baseLinkObject.transform.rotation) * wheelLinkObject.transform.rotation;

				Assert.That(pose.position.x, Is.EqualTo(expectedPosition.x).Within(1e-5f));
				Assert.That(pose.position.y, Is.EqualTo(expectedPosition.y).Within(1e-5f));
				Assert.That(pose.position.z, Is.EqualTo(expectedPosition.z).Within(1e-5f));
				Assert.That(Quaternion.Angle(pose.rotation, expectedRotation), Is.LessThan(1e-4f));
			}
			finally
			{
				Object.DestroyImmediate(rootObject);
			}
		}
	}
}