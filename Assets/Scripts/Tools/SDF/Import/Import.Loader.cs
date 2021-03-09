/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private UE.GameObject _rootObject = null;
			private UE.Camera _mainCamera = null;

			public Loader(UE.GameObject rootObject)
			{
				_rootObject = rootObject;
			}

			public void SetMainCamera(in UE.Camera camera)
			{
				if (_mainCamera == null)
				{
					_mainCamera = camera;
				}
			}

			private void SetParentObject(UE.GameObject childObject, UE.GameObject parentObject)
			{
				childObject.transform.position = UE.Vector3.zero;
				childObject.transform.rotation = UE.Quaternion.identity;

				if (parentObject == null)
				{
					childObject.transform.SetParent(_rootObject.transform, false);
				}
				else
				{
					childObject.transform.SetParent(parentObject.transform, false);
				}

				childObject.transform.localScale = UE.Vector3.one;
				childObject.transform.localPosition = UE.Vector3.zero;
				childObject.transform.localRotation = UE.Quaternion.identity;
			}

			private void SetParentObject(UE.GameObject childObject, in string parentObjectName)
			{
				var parentObject = UE.GameObject.Find(parentObjectName);

				if (parentObject != null)
				{
					SetParentObject(childObject, parentObject);
				}
				else
				{
					Debug.Log("There is no parent Object: " + parentObjectName);
				}
			}

			protected override void ImportPlugin(in SDF.Plugin plugin, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);

				// filtering plugin name
				var pluginName = plugin.ClassName();
				// Debug.Log("plugin name = " + pluginName);

				var pluginType = Type.GetType(pluginName);
				if (pluginType != null)
				{
					if (targetObject == null)
					{
						Debug.LogError("[Plugin] targetObject is empty");
						return;
					}

					var pluginObject = targetObject.AddComponent(pluginType);

					var devicePluginObject = pluginObject as DevicePlugin;
					var devicesPluginObject = pluginObject as DevicesPlugin;

					if (devicePluginObject != null)
					{
						var node = plugin.GetNode();
						devicePluginObject.SetPluginName(plugin.Name);
						devicePluginObject.SetPluginParameters(node);
						// Debug.Log("[Plugin] device added : " + plugin.Name);
					}
					else if (devicesPluginObject != null)
					{
						var node = plugin.GetNode();
						devicesPluginObject.SetPluginName(plugin.Name);
						devicesPluginObject.SetPluginParameters(node);
						// Debug.Log("[Plugin] devices added : " + plugin.Name);
					}
					else
					{
						Debug.LogError("[Plugin] failed to add : " + plugin.Name);
					}
				}
				else
				{
					Debug.LogWarningFormat("[Plugin] No plugin({0}) exist", plugin.Name);
				}
			}

			protected override void ImportWorld(in SDF.World world)
			{
				if (world == null)
				{
					return;
				}

				// Debug.Log("Import World");
				if (world.GuiCameraPose != null)
				{
					_mainCamera.transform.localPosition = UE.Vector3.zero;
					_mainCamera.transform.localRotation = UE.Quaternion.identity;
					_mainCamera.transform.position = UE.Vector3.zero;

					var rotate = SDF2Unity.GetRotation(world.GuiCameraPose.Rot);
					var translate = SDF2Unity.GetPosition(world.GuiCameraPose.Pos);
					_mainCamera.transform.Translate(translate);
					_mainCamera.transform.rotation = rotate;
				}
			}
		}
	}
}