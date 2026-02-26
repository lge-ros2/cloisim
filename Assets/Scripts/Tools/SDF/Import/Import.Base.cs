/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;
using System.Collections;
using System;

namespace SDF
{
	namespace Import
	{
		public partial class Base
		{
			private Dictionary<Joint, Object> _jointObjectList = new();
			private Dictionary<Plugin, Object> _pluginObjectList = new();

			private void ImportVisuals(IReadOnlyList<Visual> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Visual] {0}", item.Name);
					var createdObject = ImportVisual(item, parentObject);

					ImportGeometry(item.GetGeometry(), createdObject);

					AfterImportVisual(item, createdObject);

					ImportMaterial(item.GetMaterial(), createdObject);

					StorePlugins(item.GetPlugins(), createdObject);
				}
			}

			private void ImportCollisions(IReadOnlyList<Collision> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Collision] {0}", item.Name);
					var createdObject = ImportCollision(item, parentObject);

					ImportGeometry(item.GetGeometry(), createdObject);

					AfterImportCollision(item, createdObject);
				}
			}

			private void ImportSensors(IReadOnlyList<Sensor> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					var createdObject = ImportSensor(item, parentObject);
					StorePlugins(item.GetPlugins(), createdObject);
				}
			}

			protected void ImportLinks(IReadOnlyList<Link> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Link] {0}", item.Name);
					var createdObject = ImportLink(item, parentObject);

					ImportVisuals(item.GetVisuals(), createdObject);

					ImportCollisions(item.GetCollisions(), createdObject);

					ImportSensors(item.GetSensors(), createdObject);

					ImportLights(item.GetLights(), createdObject);

					AfterImportLink(item, createdObject);
				}
			}

			protected void StorePlugins(IReadOnlyList<Plugin> items, Object parentObject)
			{
				// Plugin should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					// Console.WriteLine($"PluginName: {item.Name}");
					_pluginObjectList.Add(item, parentObject);
				}
			}

			protected void StoreJoints(IReadOnlyList<Joint> items, in Object parentObject)
			{
				// Joints should be handled after all links of model are loaded due to articulation body.
				foreach (var item in items)
				{
					// Console.WriteLine($"JointName: {item.Name} Child: {item.ChildLinkName} Parent: {item.ParentLinkName}");
					_jointObjectList.Add(item, parentObject);
				}
			}

			protected IEnumerator ImportModels(IReadOnlyList<Model> items, Object parentObject = null)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Model][{0}][{1}]", item.Name, parentObject);
					yield return ImportModel(item, parentObject);
				}
			}

			protected void ImportActors(IReadOnlyList<Actor> items)
			{
				foreach (var item in items)
				{
					var createdObject = ImportActor(item);
					StorePlugins(item.GetPlugins(), createdObject);
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
				// Console.WriteLine("Import Models({0})/Links/Joints", world.GetModels().Count);
				_jointObjectList.Clear();
				_pluginObjectList.Clear();

				var worldObject = ImportWorld(world);

				yield return ImportModels(world.GetModels());

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
				}

				StorePlugins(world.GetPlugins(), worldObject);

				ImportActors(world.GetActors());
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
