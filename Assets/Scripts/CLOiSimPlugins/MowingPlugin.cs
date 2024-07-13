/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Linq;
using UnityEngine;

public class MowingPlugin : CLOiSimPlugin
{
	private GameObject _mowingList = null;
	private Transform _targetPlaneTranform = null;
	private Material _grassMaterial = null;

	private Rect _planeSize = new Rect();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;

		modelName = "Mowing";
		partsName = "_";

		_mowingList = new GameObject("MowingList");
		_mowingList.transform.SetParent(transform);

		_grassMaterial = Instantiate(Resources.Load<Material>("Materials/GeometryGrass"));
	}

	protected override void OnStart()
	{
		var targetPlaneLinkName = GetPluginParameters().GetValue<string>("target/link");

		if (FindTargetPlane(targetPlaneLinkName) == false)
		{
			Debug.LogWarning("Target is not Plane");
		}
		else
		{
			SetGrassMaterial();
			PlantGrass();
		}
	}

	private void SetGrassMaterial()
	{
		var bladeWidthMin = GetPluginParameters().GetValue<float>("grass/blade/width/min");
		var bladeWidthMax = GetPluginParameters().GetValue<float>("grass/blade/width/max");
		var bladeHeightMin = GetPluginParameters().GetValue<float>("grass/blade/height/min");
		var bladeHeightMax = GetPluginParameters().GetValue<float>("grass/blade/height/max");

		var bendBladeForwardAmount = GetPluginParameters().GetValue<float>("grass/bend/blade_amount/forward");
		var bendBladeCurvatureAmount = GetPluginParameters().GetValue<float>("grass/bend/blade_amount/curvature");
		var bendVariation = GetPluginParameters().GetValue<float>("grass/bend/variation");

		var tessAmount = GetPluginParameters().GetValue<float>("grass/tessellation/amount");
		var tessDistanceMin = GetPluginParameters().GetValue<float>("grass/tessellation/distance/min");
		var tessDistanceMax = GetPluginParameters().GetValue<float>("grass/tessellation/distance/max");

		if (_grassMaterial != null)
		{
			_grassMaterial.SetFloat("_BladeWidthMin", bladeWidthMin);
			_grassMaterial.SetFloat("_BladeWidthMax", bladeWidthMax);
			_grassMaterial.SetFloat("_BladeHeightMin", bladeHeightMin);
			_grassMaterial.SetFloat("_BladeHeightMax", bladeHeightMax);
			_grassMaterial.SetFloat("_BladeBendDistance", bendBladeForwardAmount);
			_grassMaterial.SetFloat("_BladeBendCurve", bendBladeCurvatureAmount);
			_grassMaterial.SetFloat("_BladeBendDelta", bendVariation);
			_grassMaterial.SetFloat("_TessAmount", tessAmount);
			_grassMaterial.SetFloat("_TessMinDistance", tessDistanceMin);
			_grassMaterial.SetFloat("_TessMaxDistance", tessDistanceMax);
		}
	}

	private bool FindTargetPlane(string targetPlaneLinkName)
	{
		_targetPlaneTranform = GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == targetPlaneLinkName);
		var targetPlaneCollision
				= _targetPlaneTranform?.GetComponentsInChildren<Transform>()
					.FirstOrDefault(x => x.gameObject.layer == LayerMask.NameToLayer("Plane"));

		return targetPlaneCollision != null;
	}

	private void PlantGrass()
	{
		if (_targetPlaneTranform == null)
			return;

		var targetPlaneMesh = _targetPlaneTranform?.GetComponentInChildren<MeshFilter>();
		var targetPlaneMeshRenderer = _targetPlaneTranform?.GetComponentInChildren<MeshRenderer>();

		// Debug.Log(targetPlaneMesh.sharedMesh.vertexCount);
		// Debug.Log("targetPlaneMesh.sharedMesh.bounds.size=" + targetPlaneMesh.sharedMesh.bounds.size);
		_planeSize.width = targetPlaneMesh.sharedMesh.bounds.size.x;
		_planeSize.height = targetPlaneMesh.sharedMesh.bounds.size.z;

		var materials = targetPlaneMeshRenderer.materials;
		var newMaterials = new Material[materials.Length + 1];
		materials.CopyTo(newMaterials, 0);
		newMaterials[newMaterials.Length - 1] = _grassMaterial;
		targetPlaneMeshRenderer.materials = newMaterials;
	}

	protected override void OnReset()
	{
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
	}
#endif

	void LateUpdate()
	{
	}
	// private void PublishThread(System.Object threadObject)
	// {
	// }
}