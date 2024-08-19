/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ModelImporter : MonoBehaviour
{
	private GameObject modelList = null;
	private Transform _targetObject = null;

	#region variables for the object with articulation body
	private ArticulationBody _rootArticulationBody = null;
	private Vector3 modelDeployOffset = Vector3.zero;
	#endregion

	private Transform _targetObjectForCopy = null;

	[SerializeField]
	private float maxRayDistance = 60.0f;

	void Awake()
	{
		modelList = transform.parent.Find("ModelList").gameObject;
	}

	public void OnButtonClicked()
	{
		modelList.SetActive(!modelList.activeSelf);
		Main.CameraControl.BlockMouseWheelControl(modelList.activeSelf);
	}

	private static void ChangeColliderObjectLayer(Transform target, in string layerName)
	{
		foreach (var collider in target.GetComponentsInChildren<Collider>())
		{
			collider.gameObject.layer = LayerMask.NameToLayer(layerName);
		}
	}

	private void DiscardSelectedModel()
	{
		if (_targetObject != null)
		{
			GameObject.Destroy(_targetObject.gameObject);
			_targetObject = null;
		}
		_rootArticulationBody = null;
	}

	private void BlockSelfRaycast()
	{
		if(_targetObject.CompareTag("Road"))
		{
			var meshCollider = _targetObject.GetComponentInChildren<Collider>();
			meshCollider.enabled = false;
		}
		else
		{
			ChangeColliderObjectLayer(_targetObject, "Ignore Raycast");
		}
	}

	private void UnblockSelfRaycast()
	{
		if(_targetObject.CompareTag("Road"))
		{
			var meshCollider = _targetObject.GetComponentInChildren<Collider>();
			meshCollider.enabled = true;
		}
		else
		{
			ChangeColliderObjectLayer(_targetObject, "Default");
		}
	}

	public void SetModelForDeploy(in Transform targetTransform)
	{
		const float DeployOffsetMargin = 0.1f;

		DiscardSelectedModel();

		_targetObject = targetTransform;

		BlockSelfRaycast();

		_rootArticulationBody = _targetObject.GetComponentInChildren<ArticulationBody>();
		if (_rootArticulationBody != null)
		{
			if (_rootArticulationBody.isRoot)
			{
				_rootArticulationBody.immovable = true;
			}
			else
			{
				_rootArticulationBody = null;
			}
		}

		var totalBound = new Bounds();
		foreach (var collider in _targetObject.GetComponentsInChildren<Collider>())
		{
			// Debug.Log(collider.bounds.min + ", " + collider.bounds.max);
			totalBound.Encapsulate(collider.bounds);
		}

		modelDeployOffset.y = DeployOffsetMargin + ((totalBound.min.y < 0) ? -totalBound.min.y : 0);
		// Debug.Log("Deploy == " + modelDeployOffset.y + " " + totalBound.min + ", " + totalBound.center + "," + totalBound.extents);

		#region Workaround code for Wrong TerrainHeight issue
		var terrain = targetTransform.GetComponentInChildren<Terrain>();
		if (terrain != null)
		{
			var terrainSize = terrain.terrainData.size;
			terrain.terrainData.size = terrainSize;
		}
		#endregion
	}

	private bool GetPointAndNormalOnClick(out Vector3 point, out Vector3 normal)
	{
		var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		var layerMask = ~(LayerMask.GetMask("Ignore Raycast")
						| LayerMask.GetMask("TransparentFX")
						| LayerMask.GetMask("UI")
						| LayerMask.GetMask("Water"));
		if (Physics.Raycast(ray, out var hitInfo, maxRayDistance, layerMask))
		{
			point = hitInfo.point;
			normal = hitInfo.normal;
			// Debug.Log(point + ", " + normal);
			return true;
		}
		else
		{
			point = Vector3.negativeInfinity;
			normal = Vector3.negativeInfinity;
		}
		return false;
	}

	private void UpdateInitPose()
	{
		var modelHelper = _targetObject.GetComponent<SDF.Helper.Model>();
		if (modelHelper != null)
		{
			modelHelper.SetPose(_targetObject.localPosition + modelDeployOffset, _targetObject.localRotation);
		}
	}

	private void CompleteDeployment()
	{
		if (_rootArticulationBody != null)
		{
			_rootArticulationBody.immovable = false;
		}

		UpdateInitPose();

		UnblockSelfRaycast();

		_targetObject = null;
		_rootArticulationBody = null;
	}

	private void MoveImportedObject()
	{
		if (GetPointAndNormalOnClick(out var point, out var normal))
		{
			if (_targetObject.position != point)
			{
				if (_rootArticulationBody != null)
				{
					_rootArticulationBody.Sleep();
					var bodyRotation = Quaternion.FromToRotation(transform.up, normal);
					_rootArticulationBody.TeleportRoot(point + modelDeployOffset, bodyRotation);
				}
				else
				{
					_targetObject.position = point + modelDeployOffset;
				}
			}
		}
	}

	private void HandlingImportedObject()
	{
		if (Input.GetMouseButtonUp(0))
		{
			CompleteDeployment();
		}
		else if (Input.GetKeyUp(KeyCode.Escape))
		{
			DiscardSelectedModel();
		}
		else
		{
			MoveImportedObject();
		}
	}

	void LateUpdate()
	{
		if (_targetObject != null)
		{
			HandlingImportedObject();
		}
		else
		{
			// close 'Add model' list Panel
			if (Input.GetKeyUp(KeyCode.Escape))
			{
				modelList.SetActive(false);
				Main.CameraControl.BlockMouseWheelControl(false);
			}
			else if (Input.GetKeyUp(KeyCode.F3))
			{
				OnButtonClicked();
			}
		}

		if (Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKeyUp(KeyCode.C))
			{
				Main.Gizmos.GetSelectedTargets(out var objectListForCopy);

				if (objectListForCopy.Count > 0)
				{
					if (objectListForCopy.Count > 1)
					{
						Main.UIController?.SetWarningMessage("Multiple Object is selected. Only single object can be copied.");
					}

					_targetObjectForCopy = objectListForCopy[objectListForCopy.Count - 1];

					Main.Gizmos.ClearTargets();
				}
			}
			else if (Input.GetKeyUp(KeyCode.V))
			{
				if (_targetObjectForCopy != null)
				{
					var instantiatedObject = GameObject.Instantiate(_targetObjectForCopy, _targetObjectForCopy.root, true);
					instantiatedObject.name = $"{_targetObjectForCopy.name}_clone_{instantiatedObject.GetInstanceID()}";

					if (_targetObjectForCopy.CompareTag("Road"))
					{
						var loftRoadOriginal = _targetObjectForCopy.GetComponent<Unity.Splines.LoftRoadGenerator>();
						var loftRoadNew = instantiatedObject.GetComponent<Unity.Splines.LoftRoadGenerator>();
						loftRoadNew.SdfMaterial = loftRoadOriginal.SdfMaterial;
					}

					SetModelForDeploy(instantiatedObject);
					// Debug.Log("Paste " + instantiatedObject.name);

					var segmentationTag = instantiatedObject.GetComponentInChildren<Segmentation.Tag>();
					segmentationTag?.Refresh();
					Main.SegmentationManager.UpdateTags();
				}
			}
		}
	}
}