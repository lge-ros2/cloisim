/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using UE = UnityEngine;

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
				private sealed class ModelMeshStats
				{
					public string ObjectPath { get; set; }
					public string MeshName { get; set; }
					public int TriangleCount { get; set; }
					public int VertexCount { get; set; }
				}

				private static readonly int HighPolyModelTriangleThreshold = 100000;
				private static readonly int VeryHighPolyModelTriangleThreshold = 300000;
				private static readonly int HighPolyMeshTriangleThreshold = 50000;
				private static readonly int TopMeshLogCount = 5;

				private static void ReportModelMeshStatistics(UE.GameObject modelObject)
				{
					if (modelObject == null)
					{
						return;
					}

					var meshStats = CollectModelMeshStats(modelObject);
					if (meshStats.Count <= 0)
					{
						return;
					}

					var totalTriangles = meshStats.Sum(item => item.TriangleCount);
					var totalVertices = meshStats.Sum(item => item.VertexCount);
					var renderers = modelObject.GetComponentsInChildren<UE.Renderer>(true).Length;
					var summary = $"[PolyStats] model={modelObject.name} totalTris={totalTriangles} totalVerts={totalVertices} renderers={renderers} meshes={meshStats.Count}";

					if (totalTriangles >= VeryHighPolyModelTriangleThreshold)
					{
						UE.Debug.LogWarning(summary + " severity=very-high");
					}
					else if (totalTriangles >= HighPolyModelTriangleThreshold)
					{
						UE.Debug.LogWarning(summary + " severity=high");
					}
					else
					{
						UE.Debug.Log(summary);
					}

					var topMeshes = meshStats
						.OrderByDescending(item => item.TriangleCount)
						.ThenByDescending(item => item.VertexCount)
						.Take(TopMeshLogCount)
						.ToArray();

					foreach (var meshStat in topMeshes)
					{
						var meshSummary = $"[PolyStats] top model={modelObject.name} path={meshStat.ObjectPath} mesh={meshStat.MeshName} tris={meshStat.TriangleCount} verts={meshStat.VertexCount}";
						if (meshStat.TriangleCount >= HighPolyMeshTriangleThreshold)
						{
							UE.Debug.LogWarning(meshSummary + " severity=high-mesh");
						}
						else
						{
							UE.Debug.Log(meshSummary);
						}
					}
				}

				private static List<ModelMeshStats> CollectModelMeshStats(UE.GameObject modelObject)
				{
					var result = new List<ModelMeshStats>();
					var meshFilters = modelObject.GetComponentsInChildren<UE.MeshFilter>(true);

					foreach (var meshFilter in meshFilters)
					{
						AppendMeshStats(result, meshFilter.sharedMesh, meshFilter.transform, modelObject.transform);
					}

					var skinnedMeshes = modelObject.GetComponentsInChildren<UE.SkinnedMeshRenderer>(true);
					foreach (var skinnedMesh in skinnedMeshes)
					{
						AppendMeshStats(result, skinnedMesh.sharedMesh, skinnedMesh.transform, modelObject.transform);
					}

					return result;
				}

				private static void AppendMeshStats(List<ModelMeshStats> result, UE.Mesh mesh, UE.Transform meshTransform, UE.Transform rootTransform)
				{
					if (mesh == null)
					{
						return;
					}

					var triangleCount = mesh.triangles.Length / 3;
					if (triangleCount <= 0)
					{
						return;
					}

					result.Add(new ModelMeshStats
					{
						ObjectPath = BuildRelativePath(meshTransform, rootTransform),
						MeshName = mesh.name,
						TriangleCount = triangleCount,
						VertexCount = mesh.vertexCount,
					});
				}

				private static string BuildRelativePath(UE.Transform current, UE.Transform root)
				{
					if (current == null)
					{
						return string.Empty;
					}

					if (current == root)
					{
						return current.name;
					}

					var pathSegments = new Stack<string>();
					var cursor = current;
					while (cursor != null)
					{
						pathSegments.Push(cursor.name);
						if (cursor == root)
						{
							break;
						}

						cursor = cursor.parent;
					}

					return string.Join("/", pathSegments);
				}

			/// <summary>make root articulation body for handling robots</summary>
			/// <remarks>should add root body first</remarks>
			private static UE.ArticulationBody CreateRootArticulationBody(UE.GameObject targetObject)
			{
				var articulationBody = targetObject.GetComponent<UE.ArticulationBody>();

				// Configure articulation body for root object
				if (articulationBody == null)
				{
					articulationBody = targetObject.AddComponent<UE.ArticulationBody>();
				}

				articulationBody.useGravity = false;
				articulationBody.immovable = false;
				articulationBody.linearDamping = 3;
				articulationBody.angularDamping = 1;
				articulationBody.jointFriction = 0;

				articulationBody.mass = 0.1f;
				articulationBody.automaticCenterOfMass = true;
				articulationBody.automaticInertiaTensor = true;
				articulationBody.ResetCenterOfMass();
				articulationBody.ResetInertiaTensor();

				articulationBody.solverIterations = 0;
				articulationBody.solverVelocityIterations = 0;
				articulationBody.linearVelocity = UE.Vector3.zero;
				articulationBody.angularVelocity = UE.Vector3.zero;
				articulationBody.sleepThreshold = 0.1f;
				articulationBody.Sleep();

				// Keep disabled to prevent physics from shifting transforms during import.
				// SpecifyPose() will re-enable all ArticulationBodies after poses are applied.
				articulationBody.enabled = false;

				// UE.Debug.Log(targetObject.name + " Create root articulation body");
				return articulationBody;
			}

			private static UE.Rigidbody CreateRootRigidBody(UE.GameObject targetObject)
			{
				var rigidBody = targetObject.GetComponent<UE.Rigidbody>();

				// Configure articulation body for root object
				if (rigidBody == null)
				{
					rigidBody = targetObject.AddComponent<UE.Rigidbody>();
					// Set kinematic immediately so PhysX never sees dynamic + concave-mesh-collider
					// on any pre-existing children (the warning fires at AddComponent time).
					rigidBody.isKinematic = true;
				}

				rigidBody.useGravity = false;
				rigidBody.isKinematic = true;
				rigidBody.mass = 0;
				rigidBody.ResetCenterOfMass();
				rigidBody.ResetInertiaTensor();
				rigidBody.Sleep();

				return rigidBody;
			}

			private static UE.GameObject ResolveModelHierarchyParent(in Model model, UE.GameObject parentObject)
			{
				if (parentObject == null || !model.IsNested() || string.IsNullOrEmpty(model.PoseRelativeTo))
				{
					return parentObject;
				}

				var relativeParent = Util.FindPoseRelativeObject(parentObject.transform, model.PoseRelativeTo);
				return relativeParent != null ? relativeParent.gameObject : parentObject;
			}

			private UE.GameObject CreateModel(in Model model, in UE.GameObject parentObject)
			{
				var newModelObject = new UE.GameObject(model.Name)
				{
					tag = "Model"
				};

				ResolveModelHierarchyParent(model, parentObject).SetChild(newModelObject);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.modelNameInPath = model.OriginalName();
				modelHelper.isStatic = model.Static;
				modelHelper.Pose = model.RawPose;
				modelHelper.PoseRelativeTo = model.PoseRelativeTo;
				modelHelper.isNested = model.IsNested();

				return newModelObject;
			}

			protected override IEnumerator ImportModel(Model model, object parentObject, Action<object> onCreatedRoot)
			{
				if (model == null)
				{
					yield return null;
				}

				var targetObject = parentObject as UE.GameObject;
				var newModelObject = CreateModel(model, targetObject);

				// For static root models, pre-create kinematic Rigidbody before importing links/colliders
				// so PhysX sees a kinematic body when concave mesh colliders are built, preventing
				// the "Concave Mesh Collider + dynamic Rigidbody" warning (ordering: kinematic body first,
				// then colliders, rather than colliders first and kinematic flag set after AddComponent).
				if (model.Static && newModelObject.IsRootModel())
				{
					CreateRootRigidBody(newModelObject);
				}

				ImportLinks(model.Links, newModelObject);

				// Add nested models
				yield return ImportModels(model.Models, newModelObject);

				AfterImportModel(model, newModelObject);

				StoreJoints(model.Joints, newModelObject);

				StoreGrippers(model.Grippers, newModelObject);

				StorePlugins(model.Plugins, newModelObject);

				ReportModelMeshStatistics(newModelObject);

				if (parentObject == null)
				{
					onCreatedRoot?.Invoke(newModelObject);
				}

				yield return null;
			}

			protected override void AfterImportModel(in Model model, in object targetObject)
			{
				var modelObject = targetObject as UE.GameObject;

				var modelHelper = modelObject.GetComponent<Helper.Model>();
				if (modelHelper.IsFirstChild)
				{
					Main.SegmentationManager.AttachTag(model.OriginalName(), modelObject);

					// Also attach per-link tags with model::link naming for link-level segmentation
					var linkHelpers = modelObject.GetComponentsInChildren<Helper.Link>();
					foreach (var linkHelper in linkHelpers)
					{
						var linkTagName = model.OriginalName() + "::" + linkHelper.name;
						Main.SegmentationManager.AttachTag(linkTagName, linkHelper.gameObject);
					}

					Main.SegmentationManager.UpdateTags();

					if (modelHelper.isStatic)
					{
						var rb = CreateRootRigidBody(modelObject);
						rb.isKinematic = true;
						rb.useGravity = false;
					}
					else
					{
						var bodies = modelHelper.GetComponentsInChildren<UE.ArticulationBody>(true);
						if (bodies.Length > 0)
							CreateRootArticulationBody(modelObject);
						else
							UE.Debug.LogWarning($"'{modelHelper.name}' has no articulation bodies in children");
					}
				}
			}
		}
	}
}