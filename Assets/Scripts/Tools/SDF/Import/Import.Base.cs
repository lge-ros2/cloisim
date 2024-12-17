/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System;

namespace SDF
{
	namespace Import
	{
		public partial class Base
		{
			private Dictionary<Joint, Object> _jointObjectList = new Dictionary<Joint, Object>();

			private void ImportVisuals(IReadOnlyList<Visual> items, in Object parentObject)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Visual] {0}", item.Name);
					var createdObject = ImportVisual(item, parentObject);

					ImportGeometry(item.GetGeometry(), createdObject);

					ImportPlugins(item.GetPlugins(), createdObject);

					AfterImportVisual(item, createdObject);

					ImportMaterial(item.GetMaterial(), createdObject);
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

					ImportPlugins(item.GetPlugins(), createdObject);
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

					ImportLights(item.GetLights());

					AfterImportLink(item, createdObject);
				}
			}

			protected void ImportPlugins(IReadOnlyList<Plugin> items, Object parentObject)
			{
				foreach (var item in items)
				{
					ImportPlugin(item, parentObject);
				}
			}

			protected void ImportJoints(IReadOnlyList<Joint> items, Object parentObject)
			{
				// Joints should be handled after all links of model loaded due to articulation body.
				foreach (var item in items)
				{
					_jointObjectList.Add(item, parentObject);
				}
			}

			protected void ImportModels(IReadOnlyList<Model> items, in Object parentObject = null)
			{
				foreach (var item in items)
				{
					// Console.WriteLine("[Model][{0}][{1}]", item.Name, parentObject);
					ImportModel(item, parentObject);
				}
			}

			protected void ImportActors(IReadOnlyList<Actor> items)
			{
				foreach (var item in items)
				{
					var createdObject = ImportActor(item);

					ImportPlugins(item.GetPlugins(), createdObject);
				}
			}

			protected void ImportLights(IReadOnlyList<Light> items)
			{
				foreach (var item in items)
				{
					ImportLight(item);
				}
			}

			public IEnumerator<World> Start(World world)
			{
				// Console.WriteLine("Import Models({0})/Links/Joints", world.GetModels().Count);
				_jointObjectList.Clear();

				var worldObject = ImportWorld(world);

				ImportModels(world.GetModels());

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
				}

				ImportPlugins(world.GetPlugins(), worldObject);

				worldObject.SpecifyPose();
				yield return null;

				ImportActors(world.GetActors());
				yield return null;
			}

			public IEnumerator<Model> Start(Model model)
			{
				yield return null;

				_jointObjectList.Clear();

				var modelObject = ImportModel(model);

				foreach (var jointObject in _jointObjectList)
				{
					ImportJoint(jointObject.Key, jointObject.Value);
				}

				modelObject.SpecifyPose();
			}
		}
	}
}
