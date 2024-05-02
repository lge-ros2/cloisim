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
			protected override void ImportPlugin(in SDF.Plugin plugin, in System.Object parentObject)
			{
				// filtering plugin name
				var pluginLibraryName = plugin.LibraryName();
				// Debug.Log($"[Plugin] name={plugin.Name}");

				var pluginType = Type.GetType(pluginLibraryName);
				if (pluginType == null)
				{
					Debug.LogWarning($"[Plugin] No plugin({plugin.Name}) exist");
					return;
				}

				var targetObject = (parentObject as UE.GameObject);
				if (targetObject == null)
				{
					Debug.LogError("[Plugin] targetObject is empty");
					return;
				}

				// temporary deactivate for passing plugin parameters
				targetObject.SetActive(false);

				var pluginComponent = targetObject.AddComponent(pluginType);

				var pluginObject = pluginComponent as CLOiSimPlugin;
				var multiPluginObject = pluginComponent as CLOiSimMultiPlugin;

				if (multiPluginObject != null)
				{
					multiPluginObject.SetPluginParameters(plugin);
					// Debug.Log("[Plugin] devices added : " + plugin.Name);
				}
				else if (pluginObject != null)
				{
					pluginObject.SetPluginParameters(plugin);
					// Debug.Log("[Plugin] added : " + plugin.Name);
				}
				else
				{
					Debug.LogError($"[Plugin] failed to add : {plugin.Name}");
				}

				targetObject.SetActive(true);
			}
		}
	}
}