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
			private Dictionary<SDFormat.Joint, Object> _jointObjectList = new();
			private Dictionary<SDFormat.Plugin, Object> _pluginObjectList = new();

			private void ImportVisuals(IReadOnlyList<SDFormat.Visual> items, in Object parentObject)
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

			private void ImportCollisions(IReadOnlyList<SDFormat.Collision> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportCollision(item, parentObject);

					ImportGeometry(item.Geom, createdObject);

					AfterImportCollision(item, createdObject);
				}
			}

			private void ImportSensors(IReadOnlyList<SDFormat.Sensor> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportSensor(item, parentObject);
					StorePlugins(item.Plugins, createdObject);
				}
			}

			protected void ImportLinks(IReadOnlyList<SDFormat.Link> items, in Object parentObject)
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

			protected void StorePlugins(IReadOnlyList<SDFormat.Plugin> items, Object parentObject)
			{
				// Plugin should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_pluginObjectList.Add(item, parentObject);
				}
			}

			protected void StoreJoints(IReadOnlyList<SDFormat.Joint> items, in Object parentObject)
			{
				// Joints should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					_jointObjectList.Add(item, parentObject);
				}
			}

			protected IEnumerator ImportModels(IReadOnlyList<SDFormat.Model> items, Object parentObject = null)
			{
				foreach (var item in items)
				{
					yield return ImportModel(item, parentObject);
				}
			}

			protected void ImportActors(IReadOnlyList<SDFormat.Actor> items)
			{
				foreach (var item in items)
				{
					var createdObject = ImportActor(item);
					StorePlugins(item.Plugins, createdObject);
				}
			}

			protected void ImportLights(IReadOnlyList<SDFormat.Light> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					ImportLight(item, parentObject);
				}
			}

			public IEnumerator Start(SDFormat.World world)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();

				var worldObject = ImportWorld(world);

				yield return ImportModels(world.Models);

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
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

			public IEnumerator Start(SDFormat.Model model, Action<Object> onCreatedRoot = null)
			{
				_jointObjectList.Clear();
				_pluginObjectList.Clear();

				Object modelObject = null;
				yield return ImportModel(model, onCreatedRoot: obj => modelObject = obj);

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
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
