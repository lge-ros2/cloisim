/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

public partial class SDFImporter : SDF.Importer
{
	private GameObject rootObject = null;
	private UnityEngine.Camera mainCamera = null;

	public SDFImporter()
	{
		// Debug.Log(MethodBase.GetCurrentMethod().Name);
	}

	public SDFImporter(GameObject target)
	{
		SetRootObject(target);
	}

	public void SetRootObject(GameObject target)
	{
		rootObject = target;
	}

	public void SetMainCamera(in UnityEngine.Camera camera)
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
			childObject.transform.SetParent(rootObject.transform, false);
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

		if (targetObject == null)
		{
			Debug.LogError("[Plugin] targetObject is empty");
			return;
		}

		// filtering plugin name
		var pluginName = plugin.ClassName();
		// Debug.Log("plugin name = " + pluginName);

		var pluginType = Type.GetType(pluginName);
		if (pluginType != null)
		{
			var pluginObject = targetObject.AddComponent(pluginType);
			var devicePluginObject = pluginObject as DevicePlugin;
			var devicesPluginObject = pluginObject as DevicesPlugin;

			if (devicePluginObject != null)
			{
				var node = plugin.GetNode();
				devicePluginObject.SetPluginParameters(node);
				// Debug.Log("[Plugin] device added : " + plugin.Name);
			}
			else if (devicesPluginObject != null)
			{
				var node = plugin.GetNode();
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
		SetParentObject(newModelObject, targetObject);

		// Apply attributes
		var modelPlugin = newModelObject.AddComponent<ModelPlugin>();
		modelPlugin.isStatic = model.IsStatic;

		var localPosition = SDF2Unity.GetPosition(model.Pose.Pos);
		var localRotation = SDF2Unity.GetRotation(model.Pose.Rot);
		modelPlugin.SetPose(localPosition, localRotation);
		// Debug.Log(newModelObject.name + "::" + newModelObject.transform.position + ", " + newModelObject.transform.rotation);

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
			mainCamera.transform.rotation = Quaternion.identity;
			// Debug.Log("Camera position/rotation");

			mainCamera.transform.Translate(SDF2Unity.GetPosition(world.GuiCameraPose.Pos));
			var camRot = world.GuiCameraPose.Rot;
			var camRotRoll = Mathf.Rad2Deg * (float)(camRot.Roll);
			var camRotPitch = Mathf.Rad2Deg * (float)(camRot.Pitch);
			var camRotYaw = Mathf.Rad2Deg * (float)(camRot.Yaw);
			mainCamera.transform.Rotate(camRotPitch, camRotYaw, camRotRoll);
		}
	}
}
