/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System.Linq;
// using UE = UnityEngine;
using UnityEngine;
using messages = cloisim.msgs;

public class MowingPlugin : CLOiSimPlugin
{
	private GameObject _mowingList = null;
	private Transform _targetPlaneTranform = null;

	private ProceduralGrass _proceduralGrass = null;

	private Rect _planeSize = new Rect();

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;

		modelName = "Mowing";
		partsName = "_";

		_mowingList = new GameObject("MowingList");
		_mowingList.transform.SetParent(transform);

		_proceduralGrass = transform.gameObject.AddComponent<ProceduralGrass>();
	}

	protected override void OnStart()
	{
		var deployObjectPath = GetPluginParameters().GetValue<string>("deploy/uri");
		var deployObjectPoseString = GetPluginParameters().GetValue<string>("deploy/pose");
		var targetPlaneLinkName = GetPluginParameters().GetValue<string>("target/link");
		// var blade = GetPluginParameters().GetValue<string>("blade/link");

		var deployObjectPose = new SDF.Pose<float>(deployObjectPoseString);
		var deployObjectPosition = SDF2Unity.Position(deployObjectPose.Pos);
		var deployObjectRotation = SDF2Unity.Rotation(deployObjectPose.Rot);

		// Debug.Log(deployObjectPath + " " + deployObjectPose + " " + targetPlaneLinkName);

		if (FindTargetPlane(targetPlaneLinkName) == false)
		{
			Debug.LogWarning("Target is not Plane");
		}
		else
		{
			InspectPlane();

			DeployOnPlane(deployObjectPath, deployObjectPosition, deployObjectRotation);
		}

		_proceduralGrass.Prepare();
		_proceduralGrass.Execute();
	}

	private bool FindTargetPlane(string targetPlaneLinkName)
	{
		_targetPlaneTranform = GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == targetPlaneLinkName);
		var targetPlaneCollision
				= _targetPlaneTranform?.GetComponentsInChildren<Transform>()
					.FirstOrDefault(x => x.gameObject.layer == LayerMask.NameToLayer("Plane"));

		return targetPlaneCollision != null;
	}

	private void InspectPlane()
	{
		if (_targetPlaneTranform == null)
			return;

		var targetPlaneMesh = _targetPlaneTranform?.GetComponentInChildren<MeshFilter>();

		// Debug.Log(targetPlaneMesh.sharedMesh.vertexCount);
		// Debug.Log("targetPlaneMesh.sharedMesh.bounds.size=" + targetPlaneMesh.sharedMesh.bounds.size);
		_planeSize.width = targetPlaneMesh.sharedMesh.bounds.size.x;
		_planeSize.height = targetPlaneMesh.sharedMesh.bounds.size.z;

		_proceduralGrass.SetTerrain(_planeSize);
	}

	private void DeployOnPlane(in string deployObjectPath, in Vector3 deployObjectPose, in Quaternion deployObjectRotation)
	{
		// var deployObject = Main.Instance.GetModel(deployObjectPath);
		// var deployObjectRigidBody = deployObject.GetComponent<Rigidbody>();
		// GameObject.Destroy(deployObjectRigidBody);

		if (_mowingList != null)
		{
			// deployObject.transform.SetParent(_mowingList.transform);

			// StartCoroutine(DeployObjectsOnPlane(deployObject));

			// deployObject.hideFlags = HideFlags.HideAndDontSave;
		}
	}

	private IEnumerator DeployObjectsOnPlane(GameObject targetModel)
	{
		yield return null;

		var bounds = new Bounds();
		var meshFilters = targetModel.GetComponentsInChildren<MeshFilter>();
		foreach (var meshFilter in meshFilters)
		{
			Debug.Log(meshFilter.name + "," + meshFilter.sharedMesh.bounds.center + ", " + meshFilter.sharedMesh.bounds.size);
			bounds.Encapsulate(meshFilter.sharedMesh.bounds);
		}

		yield return null;

		// Debug.Log("bounds.size=" + bounds.size);
		var deployObjectSize = new Rect(0, 0, bounds.size.x, bounds.size.z);

		if (deployObjectSize.width > _planeSize.width ||
			deployObjectSize.height > _planeSize.height)
		{
			Debug.LogWarning("Not enough space on the plane");
		}
		else
		{
			var rowCount = Mathf.CeilToInt(_planeSize.width / deployObjectSize.width);
			var colCount = Mathf.CeilToInt(_planeSize.height / deployObjectSize.height);
			Debug.Log("rowCount=" + rowCount + ", colCount=" + colCount);

			var initialOffset = new Rect(_planeSize.width/2, _planeSize.height/2, 0, 0);

			for (int i = 0; i < rowCount; i++)
			{
				for (int j = 0; j < colCount; j++)
				{
					// var cloned = GameObject.Instantiate(targetModel);
					// cloned.transform.SetParent(_mowingList.transform);

					// var x = i * deployObjectSize.width - initialOffset.x;
					// var y = j * deployObjectSize.height - initialOffset.y;
					// var offset = new Vector3(x, 0, y);
					// cloned.transform.localPosition = offset;
				}
				yield return null;
			}
		}

		yield return null;
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