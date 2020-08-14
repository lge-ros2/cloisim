/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Reflection;
using System;

namespace SDF
{
	public class Importer
	{
		public Importer()
		{
		}

		protected virtual void ImportWorld(in World world)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, world.Name);
		}

		protected virtual void ImportPlugin(in Plugin plugin, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, plugin.Name);
		}

		protected virtual void ImportJoint(in Joint joint, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, joint.Name);
		}

		protected virtual Object ImportSensor(in Sensor sensor, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, sensor.Name);
			return null;
		}

		protected virtual Object ImportVisual(in Visual visual, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
			return null;
		}

		protected virtual void PostImportVisual(in SDF.Visual visual, in System.Object targetObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
		}

		protected virtual Object ImportCollision(in Collision collision, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
			return null;
		}

		protected virtual void PostImportCollision(in Collision collision, in Object targetObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
		}

		protected virtual Object ImportLink(in Link link, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
			return null;
		}

		protected virtual void PostImportLink(in Link link, in Object targetObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
		}

		protected virtual Object ImportModel(in Model model, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
			return null;
		}

		protected virtual void ImportGeometry(in Geometry geometry, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, geometry.Name);
		}

		protected virtual void ImportMaterial(in Material sdfMaterial, in Object parentObject)
		{
			PrintNotImported(MethodBase.GetCurrentMethod().Name, sdfMaterial.Name);
		}

		private void ImportVisuals(IReadOnlyList<Visual> items, in Object parentObject)
		{
			foreach (var item in items)
			{
				// Console.WriteLine("[Visual] {0}", item.Name);
				var createdObject = ImportVisual(item, parentObject);

				ImportGeometry(item.GetGeometry(), createdObject);

				ImportMaterial(item.GetMaterial(), createdObject);

				ImportPlugins(item.GetPlugins(), createdObject);

				PostImportVisual(item, createdObject);
			}
		}

		private void ImportCollisions(IReadOnlyList<Collision> items, in Object parentObject)
		{
			foreach (var item in items)
			{
				// Console.WriteLine("[Collision] {0}", item.Name);
				var createdObject = ImportCollision(item, parentObject);

				ImportGeometry(item.GetGeometry(), createdObject);

				PostImportCollision(item, createdObject);
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

		private void ImportLinks(IReadOnlyList<Link> items, in Object parentObject)
		{
			foreach (var item in items)
			{
				// Console.WriteLine("[Link] {0}", item.Name);
				var createdObject = ImportLink(item, parentObject);

				ImportVisuals(item.GetVisuals(), createdObject);

				ImportCollisions(item.GetCollisions(), createdObject);

				ImportSensors(item.GetSensors(), createdObject);

				PostImportLink(item, createdObject);
			}
		}

		private void ImportPlugins(IReadOnlyList<Plugin> items, Object parentObject)
		{
			foreach (var item in items)
			{
				ImportPlugin(item, parentObject);
			}
		}

		private void ImportJoints(IReadOnlyList<Joint> items, Object parentObject)
		{
			foreach (var item in items)
			{
				ImportJoint(item, parentObject);
			}
		}

		private void ImportModels(IReadOnlyList<Model> items, in Object parentObject = null)
		{
			foreach (var item in items)
			{
				// Console.WriteLine("[Model][{0}][{1}]", item.Name, parentObject);
				Object child = ImportModel(item, parentObject);
				ImportLinks(item.GetLinks(), child);

				// Add nested models
				ImportModels(item.GetModels(), child);

				// Joint should be added after all links of model loaded!!!
				ImportJoints(item.GetJoints(), child);

				// Plugin should be added after all links of model loaded!!!
				ImportPlugins(item.GetPlugins(), child);
			}
		}

		public void Start(in World _world)
		{
			ImportWorld(_world);

			// Console.WriteLine("Import Models({0})/Links/Joints", _world.GetModels().Count);
			ImportModels(_world.GetModels());
		}

		private void PrintNotImported(in string methodName, in string name)
		{
			(Console.Out as DebugLogWriter).SetWarning(true);
			Console.WriteLine("[{0}][{1}] Not Imported yet", methodName, name);
			(Console.Out as DebugLogWriter).SetWarning(false);
		}

		private void PrintNotImported(in string methodName)
		{
			(Console.Out as DebugLogWriter).SetWarning(true);
			Console.WriteLine("[{0}] Not Imported yet", methodName);
			(Console.Out as DebugLogWriter).SetWarning(false);
		}
	}
}