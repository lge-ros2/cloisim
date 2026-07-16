/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Reflection;
using System.Collections;
using System;

namespace SDFormat
{
	namespace Import
	{
		public partial class Base
		{
			protected virtual object ImportWorld(in World world)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, world.Name);
				return null;
			}

			protected virtual void ImportPlugin(in Plugin plugin, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, plugin.Name);
			}

			protected virtual void ImportJoint(in Joint joint, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, joint.Name);
			}

			protected virtual object ImportSensor(in Sensor sensor, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, sensor.Name);
				return null;
			}

			protected virtual object ImportVisual(in Visual visual, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
				return null;
			}

			protected virtual void AfterImportVisual(in Visual visual, in object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
			}

			protected virtual object ImportCollision(in Collision collision, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
				return null;
			}

			protected virtual void AfterImportCollision(in Collision collision, in object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
			}

			protected virtual object ImportLink(in Link link, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
				return null;
			}

			protected virtual void AfterImportLink(in Link link, in object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
			}

			protected virtual IEnumerator ImportModel(Model model, object parentObject = null, Action<object> onCreatedRoot = null)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
				yield return null;
			}

			protected virtual void AfterImportModel(in Model model, in object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
			}

			protected virtual object ImportActor(in Actor actor)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, actor.Name);
				return null;
			}

			protected virtual IEnumerator ImportGeometry(Geometry geometry, object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, geometry.Type.ToString());
				yield return null;
			}

			protected virtual void ImportMaterial(in Material sdfMaterial, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, "material");
			}

			protected virtual void ImportLight(in Light light, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, light.Name);
			}

			protected virtual void ImportGripper(in Gripper gripper, in object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, gripper.Name);
			}

			private void PrintNotImported(in string methodName, in string name)
			{
				Console.Error.WriteLine($"[{methodName}][{name}] Not Imported yet");
			}

			private void PrintNotImported(in string methodName)
			{
				Console.Error.WriteLine($"[{methodName}] Not Imported yet");
			}
		}
	}
}
