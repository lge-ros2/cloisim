/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UE = UnityEngine;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private GameObject _rootObject = null;
			private UE.Camera mainCamera = null;

			public Loader(GameObject rootObject)
			{
				_rootObject = rootObject;
			}

			public void SetMainCamera(in UE.Camera camera)
			{
				if (mainCamera == null)
				{
					mainCamera = camera;
				}
			}

			private void SetParentObject(GameObject childObject, GameObject parentObject)
			{
				childObject.transform.position = Vector3.zero;
				childObject.transform.rotation = Quaternion.identity;

				if (parentObject == null)
				{
					childObject.transform.SetParent(_rootObject.transform, false);
				}
				else
				{
					childObject.transform.SetParent(parentObject.transform, false);
				}

				childObject.transform.localScale = Vector3.one;
				childObject.transform.localPosition = Vector3.zero;
				childObject.transform.localRotation = Quaternion.identity;
			}

			private void SetParentObject(GameObject childObject, string parentObjectName)
			{
				var parentObject = GameObject.Find(parentObjectName);

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
				var targetObject = (parentObject as GameObject);

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

			protected override System.Object ImportModel(in SDF.Model model, in System.Object parentObject)
			{
				if (model == null)
				{
					return null;
				}

				var targetObject = (parentObject as GameObject);
				var newModelObject = new GameObject(model.Name);
				newModelObject.tag = "Model";

				SetParentObject(newModelObject, targetObject);

				// Apply attributes
				var localPosition = SDF2Unity.GetPosition(model.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(model.Pose.Rot);
				// Debug.Log(newModelObject.name + "::" + localPosition + ", " + localRotation);

				var modelHelper = newModelObject.AddComponent<Helper.Model>();
				modelHelper.isStatic = model.IsStatic;
				modelHelper.SetPose(localPosition, localRotation);

				return newModelObject as System.Object;
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
					mainCamera.transform.localPosition = Vector3.zero;
					mainCamera.transform.localRotation = Quaternion.identity;
					mainCamera.transform.position = Vector3.zero;

					mainCamera.transform.Translate(SDF2Unity.GetPosition(world.GuiCameraPose.Pos));
					var rotate = SDF2Unity.GetRotation(world.GuiCameraPose.Rot);
					mainCamera.transform.rotation = rotate;
				}
			}
		}
	}
}