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
			protected virtual Object ImportWorld(in SDFormat.World world)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, world.Name);
				return null;
			}

			protected virtual void ImportPlugin(in SDFormat.Plugin plugin, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, plugin.Name);
			}

			protected virtual void ImportJoint(in SDFormat.Joint joint, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, joint.Name);
			}

			protected virtual Object ImportSensor(in SDFormat.Sensor sensor, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, sensor.Name);
				return null;
			}

			protected virtual Object ImportVisual(in SDFormat.Visual visual, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
				return null;
			}

			protected virtual void AfterImportVisual(in SDFormat.Visual visual, in System.Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, visual.Name);
			}

			protected virtual Object ImportCollision(in SDFormat.Collision collision, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
				return null;
			}

			protected virtual void AfterImportCollision(in SDFormat.Collision collision, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, collision.Name);
			}

			protected virtual Object ImportLink(in SDFormat.Link link, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
				return null;
			}

			protected virtual void AfterImportLink(in SDFormat.Link link, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, link.Name);
			}

			protected virtual IEnumerator ImportModel(SDFormat.Model model, Object parentObject = null, Action<Object> onCreatedRoot = null)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
				yield return null;
			}

			protected virtual void AfterImportModel(in SDFormat.Model model, in Object targetObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, model.Name);
			}

			protected virtual System.Object ImportActor(in SDFormat.Actor actor)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, actor.Name);
				return null;
			}

			protected virtual void ImportGeometry(in SDFormat.Geometry geometry, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, geometry.Type.ToString());
			}

			protected virtual void ImportMaterial(in SDFormat.Material sdfMaterial, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, "material");
			}

			protected virtual void ImportLight(in SDFormat.Light light, in Object parentObject)
			{
				PrintNotImported(MethodBase.GetCurrentMethod().Name, light.Name);
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
