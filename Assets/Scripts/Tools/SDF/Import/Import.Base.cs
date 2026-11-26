/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Import
	{
		public partial class Base
		{
			private Dictionary<Joint, object> _jointObjectList = new();
			private Dictionary<Plugin, object> _pluginObjectList = new();
			private Dictionary<Gripper, object> _gripperObjectList = new();
			private List<(UE.Transform parentLink, UE.Transform childRoot, string rootModelName)> _pendingIslandSplits = new();

			private static void UpdateEnvironmentIfNeeded(in object targetObject)
			{
				if (targetObject is not UE.GameObject gameObject)
				{
					return;
				}

				if (gameObject.GetComponentsInChildren<UE.Light>(true).Length <= 0)
				{
					return;
				}

				UE.DynamicGI.UpdateEnvironment();
			}

			private IEnumerator ImportVisuals(IReadOnlyList<Visual> items, object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportVisual(item, parentObject);

					var geomScope = LoadProfiler.Begin("visual.geometry");
					yield return ImportGeometry(item.Geom, createdObject);
					geomScope.End();

					AfterImportVisual(item, createdObject);

					ImportMaterial(item.MaterialInfo, createdObject);

					StorePlugins(item.Plugins, createdObject);
				}
			}

			private IEnumerator ImportCollisions(IReadOnlyList<Collision> items, object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportCollision(item, parentObject);

					var geomScope = LoadProfiler.Begin("collision.geometry");
					yield return ImportGeometry(item.Geom, createdObject);
					geomScope.End();

					var afterScope = LoadProfiler.Begin("collision.after (VHACD+collider)");
					yield return AfterImportCollision(item, createdObject);
					afterScope.End();
				}
			}

			private void ImportSensors(IReadOnlyList<Sensor> items, in object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportSensor(item, parentObject);
					StorePlugins(item.Plugins, createdObject);
				}
			}

			protected IEnumerator ImportLinks(IReadOnlyList<Link> items, object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportLink(item, parentObject);

					yield return ImportVisuals(item.Visuals, createdObject);

					yield return ImportCollisions(item.Collisions, createdObject);

					ImportSensors(item.Sensors, createdObject);

					ImportLights(item.Lights, createdObject);

					var afterLinkScope = LoadProfiler.Begin("link.after (articulation)");
					AfterImportLink(item, createdObject);
					afterLinkScope.End();
				}
			}

			protected void StorePlugins(IReadOnlyList<Plugin> items, object parentObject)
			{
				// Plugin should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_pluginObjectList.Add(item, parentObject);
				}
			}

			protected void StoreGrippers(IReadOnlyList<Gripper> items, object parentObject)
			{
				// Grippers should be handled after all links of model are loaded.
				foreach (var item in items)
				{
					_gripperObjectList.Add(item, parentObject);
				}
			}

			protected void StoreJoints(IReadOnlyList<Joint> items, in object parentObject)
			{
				// Joints should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_jointObjectList.Add(item, parentObject);
				}
			}

			// Registered by ImportJoint() for fixed-joint subtrees with too many
			// internal ArticulationBody nodes to safely join the parent's island.
			// Processed by SpecifyPose() after pose computation but before the
			// ArticulationBody enable loop, so the split takes effect before Unity's
			// native 64-node island limit is evaluated.
			//
			// The owning root model's name is resolved here (at registration time,
			// while parentLink is still attached under its original model hierarchy)
			// rather than later in SpecifyPose(): earlier splits in the same batch may
			// have already reparented an ancestor of parentLink out from under the
			// root model by the time later splits are processed, which would make a
			// deferred lookup resolve to nothing (e.g. a whole arm subtree split off
			// before a hand subtree mounted on that same arm is processed).
			protected void RegisterPendingIslandSplit(UE.Transform parentLink, UE.Transform childRoot)
			{
				var rootModelName = parentLink.GetComponentsInParent<Helper.Model>().LastOrDefault()?.name;
				_pendingIslandSplits.Add((parentLink, childRoot, rootModelName));
			}

			protected IEnumerator ImportModels(IReadOnlyList<Model> items, object parentObject = null)
			{
				foreach (var item in items)
				{
					yield return ImportModel(item, parentObject);
				}
			}

			protected void ImportActors(IReadOnlyList<Actor> items)
			{
				foreach (var item in items)
				{
					try
					{
						var createdObject = ImportActor(item);
						StorePlugins(item.Plugins, createdObject);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import actor '{item.Name}', skipping it: {e}");
					}
				}
			}

			protected void ImportLights(IReadOnlyList<Light> items, in object parentObject)
			{
				foreach (var item in items)
				{
					ImportLight(item, parentObject);
				}
			}

			// Lists every mesh file the import will load, in import order
			// (per link: visuals then collisions; nested models after the links), for parallel pre-parsing.
			private static void CollectMeshReferences(Geometry geometry, List<(string uri, string subMesh)> result)
			{
				if (geometry != null && geometry.Type == GeometryType.Mesh && geometry.MeshShape != null)
				{
					result.Add((geometry.MeshShape.Uri, geometry.MeshShape.Submesh));
				}
			}

			private static void CollectMeshReferences(Model model, List<(string uri, string subMesh)> result)
			{
				if (model == null)
				{
					return;
				}

				foreach (var link in model.Links)
				{
					foreach (var visual in link.Visuals)
					{
						CollectMeshReferences(visual.Geom, result);
					}

					foreach (var collision in link.Collisions)
					{
						CollectMeshReferences(collision.Geom, result);
					}
				}

				foreach (var nestedModel in model.Models)
				{
					CollectMeshReferences(nestedModel, result);
				}
			}

			public IEnumerator Start(World world)
			{
				var meshes = new List<(string uri, string subMesh)>();
				foreach (var model in world.Models)
				{
					CollectMeshReferences(model, meshes);
				}

				MeshLoader.BeginPrefetch(meshes);
				try
				{
					yield return StartWorld(world);
				}
				finally
				{
					MeshLoader.EndPrefetch();
				}
			}

			public IEnumerator Start(Model model, Action<object> onCreatedRoot = null)
			{
				var meshes = new List<(string uri, string subMesh)>();
				CollectMeshReferences(model, meshes);

				MeshLoader.BeginPrefetch(meshes);
				try
				{
					yield return StartModel(model, onCreatedRoot);
				}
				finally
				{
					MeshLoader.EndPrefetch();
				}
			}

			private IEnumerator StartWorld(World world)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();
				_gripperObjectList.Clear();
				_pendingIslandSplits.Clear();
				Helper.DetachedIslandUtil.ClearDetachedSubtrees();

				var scope = LoadProfiler.Begin("import.world");
				var worldObject = ImportWorld(world);
				scope.End();

				scope = LoadProfiler.Begin("import.models");
				yield return ImportModels(world.Models);
				scope.End();

				scope = LoadProfiler.Begin("import.joints+grippers");
				foreach (var jointObject in _jointObjectList)
				{
					try
					{
						ImportJoint(jointObject.Key, jointObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import joint '{jointObject.Key.Name}', skipping it: {e}");
					}
				}

				foreach (var gripperObject in _gripperObjectList)
				{
					try
					{
						ImportGripper(gripperObject.Key, gripperObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import gripper '{gripperObject.Key.Name}', skipping it: {e}");
					}
				}

				scope.End();

				scope = LoadProfiler.Begin("import.actors");
				StorePlugins(world.Plugins, worldObject);

				ImportActors(world.Actors);
				yield return null;
				scope.End();

				scope = LoadProfiler.Begin("import.pose+environment");
				worldObject?.SpecifyPose(_pendingIslandSplits);
				UpdateEnvironmentIfNeeded(worldObject);
				scope.End();

				scope = LoadProfiler.Begin("import.plugins");

				foreach (var pluginObject in _pluginObjectList)
				{
					try
					{
						ImportPlugin(pluginObject.Key, pluginObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import plugin '{pluginObject.Key.Name}', skipping it: {e}");
					}
				}

				scope.End();

				LogVhacdReport(worldObject);
			}

			private IEnumerator StartModel(Model model, Action<object> onCreatedRoot)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();
				_gripperObjectList.Clear();
				_pendingIslandSplits.Clear();
				Helper.DetachedIslandUtil.ClearDetachedSubtrees();

				object modelObject = null;
				var scope = LoadProfiler.Begin("import.models");
				yield return ImportModel(model, onCreatedRoot: obj => modelObject = obj);
				scope.End();

				scope = LoadProfiler.Begin("import.joints+grippers");

				foreach (var jointObject in _jointObjectList)
				{
					try
					{
						ImportJoint(jointObject.Key, jointObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import joint '{jointObject.Key.Name}', skipping it: {e}");
					}
				}

				foreach (var gripperObject in _gripperObjectList)
				{
					try
					{
						ImportGripper(gripperObject.Key, gripperObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import gripper '{gripperObject.Key.Name}', skipping it: {e}");
					}
				}

				scope.End();

				scope = LoadProfiler.Begin("import.pose+environment");
				modelObject?.SpecifyPose(_pendingIslandSplits);
				UpdateEnvironmentIfNeeded(modelObject);
				scope.End();

				scope = LoadProfiler.Begin("import.plugins");

				foreach (var pluginObject in _pluginObjectList)
				{
					try
					{
						ImportPlugin(pluginObject.Key, pluginObject.Value);
					}
					catch (Exception e)
					{
						UE.Debug.LogError($"Failed to import plugin '{pluginObject.Key.Name}', skipping it: {e}");
					}
				}

				scope.End();

				LogVhacdReport(modelObject);
				onCreatedRoot?.Invoke(modelObject);
			}

			private const int VhacdReportMaxPaths = 20;

			private static void LogVhacdReport(in object rootObject)
			{
				var summary = VHACD.ConsumeStatsSummary();
				if (!string.IsNullOrEmpty(summary))
				{
					UE.Debug.Log($"[VHACD] {summary}");
				}

				// TODO(vhacd-convex): measurement for convex=true decision; remove after
				if (rootObject is not UE.GameObject rootGameObject)
				{
					return;
				}

				var count = 0;
				var paths = new List<string>();
				foreach (var meshCollider in rootGameObject.GetComponentsInChildren<UE.MeshCollider>(true))
				{
					if (meshCollider.convex || meshCollider.attachedArticulationBody == null)
					{
						continue;
					}

					count++;
					if (paths.Count < VhacdReportMaxPaths)
					{
						var path = meshCollider.name;
						for (var t = meshCollider.transform.parent; t != null && t != rootGameObject.transform.parent; t = t.parent)
						{
							path = t.name + "/" + path;
						}
						paths.Add(path);
					}
				}

				if (count > 0)
				{
					UE.Debug.Log($"[VHACD] concave MeshColliders under ArticulationBody: {count}\n  " + string.Join("\n  ", paths) +
						(count > paths.Count ? $"\n  ... (+{count - paths.Count} more)" : string.Empty));
				}
				else
				{
					UE.Debug.Log("[VHACD] concave MeshColliders under ArticulationBody: 0");
				}
			}
		}
	}
}
