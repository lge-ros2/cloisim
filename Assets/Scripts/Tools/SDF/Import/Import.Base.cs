/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System;

namespace SDFormat
{
	namespace Import
	{
		public partial class Base
		{
			private Dictionary<Joint, Object> _jointObjectList = new();
			private Dictionary<Plugin, Object> _pluginObjectList = new();
			private Dictionary<Gripper, Object> _gripperObjectList = new();

			private void ImportVisuals(IReadOnlyList<Visual> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportVisual(item, parentObject);

					ImportGeometry(item.Geom, createdObject);

					AfterImportVisual(item, createdObject);

					ImportMaterial(item.MaterialInfo, createdObject);

					StorePlugins(item.Plugins, createdObject);
				}
			}

			private void ImportCollisions(IReadOnlyList<Collision> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportCollision(item, parentObject);

					ImportGeometry(item.Geom, createdObject);

					AfterImportCollision(item, createdObject);
				}
			}

			private void ImportSensors(IReadOnlyList<Sensor> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportSensor(item, parentObject);
					StorePlugins(item.Plugins, createdObject);
				}
			}

			protected void ImportLinks(IReadOnlyList<Link> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportLink(item, parentObject);

					ImportVisuals(item.Visuals, createdObject);

					ImportCollisions(item.Collisions, createdObject);

					ImportSensors(item.Sensors, createdObject);

					ImportLights(item.Lights, createdObject);

					AfterImportLink(item, createdObject);
				}
			}

			protected void StorePlugins(IReadOnlyList<Plugin> items, Object parentObject)
			{
				// Plugin should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_pluginObjectList.Add(item, parentObject);
				}
			}

			protected void StoreGrippers(IReadOnlyList<Gripper> items, Object parentObject)
			{
				// Grippers should be handled after all links of model are loaded.
				foreach (var item in items)
				{
					_gripperObjectList.Add(item, parentObject);
				}
			}

			protected void StoreJoints(IReadOnlyList<Joint> items, in Object parentObject)
			{
				// Joints should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_jointObjectList.Add(item, parentObject);
				}
			}

			protected IEnumerator ImportModels(IReadOnlyList<Model> items, Object parentObject = null)
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
					var createdObject = ImportActor(item);
					StorePlugins(item.Plugins, createdObject);
				}
			}

			protected void ImportLights(IReadOnlyList<Light> items, in Object parentObject)
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
					ImportJoint(jointObject.Key, jointObject.Value);
				}

				foreach (var gripperObject in _gripperObjectList)
				{
					ImportGripper(gripperObject.Key, gripperObject.Value);
				}

				StorePlugins(world.Plugins, worldObject);

				ImportActors(world.Actors);
				yield return null;

				worldObject?.SpecifyPose();

				foreach (var pluginObject in _pluginObjectList)
				{
					ImportPlugin(pluginObject.Key, pluginObject.Value);
				}
			}

			public IEnumerator Start(Model model, Action<Object> onCreatedRoot = null)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();
				_gripperObjectList.Clear();

				Object modelObject = null;
				yield return ImportModel(model, onCreatedRoot: obj => modelObject = obj);

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
				}

				foreach (var gripperObject in _gripperObjectList)
				{
					ImportGripper(gripperObject.Key, gripperObject.Value);
				}

				modelObject?.SpecifyPose();

				foreach (var pluginObject in _pluginObjectList)
				{
					ImportPlugin(pluginObject.Key, pluginObject.Value);
				}

				onCreatedRoot?.Invoke(modelObject);
			}
		}
	}
}
