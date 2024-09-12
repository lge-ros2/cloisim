/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using System;

namespace SDF
{
	namespace Import
	{
		public partial class Base
		{
			protected virtual Object ImportWorld(in World world)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, world.Name);
				return null;
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

			protected virtual void AfterImportVisual(in SDF.Visual visual, in System.Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
			}

			protected virtual Object ImportCollision(in Collision collision, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
				return null;
			}

			protected virtual void AfterImportCollision(in Collision collision, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
			}

			protected virtual Object ImportLink(in Link link, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
				return null;
			}

			protected virtual void AfterImportLink(in Link link, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
			}

			protected virtual Object ImportModel(in Model model, in Object parentObject = null)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
				return null;
			}

			protected virtual void AfterImportModel(in Model model, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
			}

			protected virtual System.Object ImportActor(in Actor actor)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, actor.Name);
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

			protected virtual void ImportLight(in Light light)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, light.Name);
			}

			private void PrintNotImported(in string methodName, in string name)
			{
				Console.Error.WriteLine("[{0}][{1}] Not Imported yet", methodName, name);
			}

			private void PrintNotImported(in string methodName)
			{
				Console.Error.WriteLine("[{0}] Not Imported yet", methodName);
			}
		}
	}
}
