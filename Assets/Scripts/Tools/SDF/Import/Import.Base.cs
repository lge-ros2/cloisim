/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
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

					yield return ImportGeometry(item.Geom, createdObject);

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

					yield return ImportGeometry(item.Geom, createdObject);

					AfterImportCollision(item, createdObject);
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

					AfterImportLink(item, createdObject);
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

			public IEnumerator Start(World world)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();
				_gripperObjectList.Clear();

				var worldObject = ImportWorld(world);

				yield return ImportModels(world.Models);

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

				StorePlugins(world.Plugins, worldObject);

				ImportActors(world.Actors);
				yield return null;

				worldObject?.SpecifyPose();
				UpdateEnvironmentIfNeeded(worldObject);

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
			}

			public IEnumerator Start(Model model, Action<object> onCreatedRoot = null)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();
				_gripperObjectList.Clear();

				object modelObject = null;
				yield return ImportModel(model, onCreatedRoot: obj => modelObject = obj);

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

				modelObject?.SpecifyPose();
				UpdateEnvironmentIfNeeded(modelObject);

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

				onCreatedRoot?.Invoke(modelObject);
			}
		}
	}
}
