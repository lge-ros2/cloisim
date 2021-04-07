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
			private UE.GameObject _rootObjectModels = null;
			private UE.GameObject _rootObjectLights = null;

			public void SetRootModels(in UE.GameObject root)
			{
				_rootObjectModels = root;
			}

			public void SetRootLights(in UE.GameObject root)
			{
				_rootObjectLights = root;
			}

			private void SetParentObject(UE.GameObject childObject, UE.GameObject parentObject)
			{
				childObject.transform.position = UE.Vector3.zero;
				childObject.transform.rotation = UE.Quaternion.identity;

				var targetParentTransform = (parentObject == null) ? _rootObjectModels.transform : parentObject.transform;
				childObject.transform.SetParent(targetParentTransform, false);

				childObject.transform.localScale = UE.Vector3.one;
				childObject.transform.localPosition = UE.Vector3.zero;
				childObject.transform.localRotation = UE.Quaternion.identity;
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
		}
	}
}