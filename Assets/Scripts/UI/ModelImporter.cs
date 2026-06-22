/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine.UI;
using UnityEngine;
using UnityEngine.InputSystem;

public class ModelImporter : MonoBehaviour
{
	private GameObject _modelList = null;
	private Transform _targetObject = null;

	#region variables for the object with articulation body
	private ArticulationBody _rootArticulationBody = null;
	private Vector3 _modelDeployOffset = Vector3.zero;
	#endregion

	private Rigidbody _rootRigidbody = null;

	private Transform _targetObjectForCopy = null;

	[SerializeField]
	private float _maxRayDistance = 60.0f;

	void Awake()
	{
		_modelList = Main.UIMainCanvas.transform.Find("ModelList").gameObject;
	}

	public void ToggleModelList()
	{
		ShowModelList(!_modelList.activeSelf);
	}

	public void ShowModelList(in bool open)
	{
		_modelList.SetActive(open);
		Main.CameraControl.BlockMouseWheelControl(open);
	}

	private void ClearUIModelList()
	{
		if (_modelList != null)
		{
			// Update UI Model list
			var viewport = _modelList.transform.GetChild(0);
			var contentList = viewport.GetChild(0).gameObject;
			foreach (var child in contentList.GetComponentsInChildren<Button>())
			{
				Destroy(child.gameObject);
			}
		}
	}

	public void UpdateUIModelList(in SDFormat.ResourceModelTable resourceModelTable)
	{
		if (_modelList == null)
		{
			Debug.LogWarning("_modelList is null");
			return;
		}

		ClearUIModelList();

		// Update UI Model list
		var viewport = _modelList.transform.GetChild(0);
		var contentList = viewport.GetChild(0).gameObject;
		var buttonTemplate = viewport.Find("ButtonTemplate").gameObject;

		foreach (var item in resourceModelTable)
		{
			// var itemKey = item.Key;
			var itemValue = item.Value;
			var duplicatedButton = Instantiate(buttonTemplate);
			duplicatedButton.SetActive(true);
			duplicatedButton.transform.SetParent(contentList.transform, false);

			var textComponent = duplicatedButton.GetComponentInChildren<Text>();
			textComponent.text = itemValue.configName;

			var buttonComponent = duplicatedButton.GetComponentInChildren<Button>();
			buttonComponent.onClick.AddListener(delegate ()
			{
				// Debug.Log(itemValue.configName + ", " + itemValue.FullName + ", " + itemValue.sdfFileName);
				StartCoroutine(Main.Instance.LoadModel(itemValue.path, itemValue.filename));
			});
		}
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
			Main.SuppressPhysicsDebugContacts("discarding a staged model");
			if (_targetObject.CompareTag("Model"))
			{
				Main.SafeDestroyModelRoot(_targetObject);
			}
			else
			{
				Destroy(_targetObject.gameObject);
			}
			_targetObject = null;
		}
		_rootArticulationBody = null;
	}

	private void BlockSelfRaycast()
	{
		if (_targetObject.CompareTag("Road") || _targetObject.CompareTag("Props"))
		{
			foreach (var col in _targetObject.GetComponentsInChildren<Collider>())
				col.enabled = false;
		}
		else
		{
			ChangeColliderObjectLayer(_targetObject, "Ignore Raycast");
		}
	}

	private void UnblockSelfRaycast()
	{
		if (_targetObject.CompareTag("Road") || _targetObject.CompareTag("Props"))
		{
			foreach (var col in _targetObject.GetComponentsInChildren<Collider>())
				col.enabled = true;
		}
		else
		{
			ChangeColliderObjectLayer(_targetObject, "Default");
		}
	}

	public void SetModelForDeploy(in Transform targetTransform)
	{
		const float DeployOffsetMargin = 0.08f;

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

		_rootRigidbody = (_rootArticulationBody == null) ? _targetObject.GetComponentInChildren<Rigidbody>() : null;
		if (_rootRigidbody != null)
		{
			_rootRigidbody.isKinematic = true;
		}

		var totalBound = new Bounds();
		foreach (var renderer in _targetObject.GetComponentsInChildren<Renderer>())
		{
			totalBound.Encapsulate(renderer.bounds);
		}

		_modelDeployOffset.y = DeployOffsetMargin + Mathf.Max(0f, _targetObject.position.y - totalBound.min.y);
		// Debug.Log("Deploy == " + _modelDeployOffset.y + " " + totalBound.min + ", " + totalBound.center + "," + totalBound.extents);

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
		var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
		var layerMask = ~(LayerMask.GetMask("Ignore Raycast")
						| LayerMask.GetMask("TransparentFX")
						| LayerMask.GetMask("UI")
						| LayerMask.GetMask("Water"));
		var hits = Physics.RaycastAll(ray, _maxRayDistance, layerMask);
		System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
		foreach (var hit in hits)
		{
			if (_targetObject != null && hit.transform.IsChildOf(_targetObject))
				continue;

			point = hit.point;
			normal = hit.normal;
			return true;
		}

		point = Vector3.negativeInfinity;
		normal = Vector3.negativeInfinity;
		return false;
	}

	private void SetInitPose()
	{
		var modelHelper = _targetObject.GetComponent<SDFormat.Helper.Model>();
		if (modelHelper != null)
		{
			modelHelper.SetPose(_targetObject.localPosition + _modelDeployOffset, _targetObject.localRotation);
		}
	}

	private void CompleteDeployment()
	{
		if (_rootArticulationBody != null)
		{
			_rootArticulationBody.immovable = false;
		}

		if (_rootRigidbody != null)
		{
			var modelHelper = _targetObject?.GetComponent<SDFormat.Helper.Model>();
			if (modelHelper == null || !modelHelper.isStatic)
			{
				_rootRigidbody.isKinematic = false;
			}
			_rootRigidbody = null;
		}

		SetInitPose();

		UnblockSelfRaycast();

		foreach (var helper in _targetObject.GetComponentsInChildren<SDFormat.Helper.Base>())
		{
			helper.Reset();
		}

		foreach (var plugin in _targetObject.GetComponentsInChildren<CLOiSimPlugin>())
		{
			plugin.Reset();
		}

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
					var bodyRotation = Quaternion.FromToRotation(transform.up, normal) * _rootArticulationBody.transform.rotation;
					_rootArticulationBody.TeleportRoot(point + _modelDeployOffset, bodyRotation);
				}
				else
				{
					_targetObject.position = point + _modelDeployOffset;
				}
			}
		}
	}

	private void HandlingImportedObject()
	{
		if (Mouse.current.leftButton.wasReleasedThisFrame)
		{
			CompleteDeployment();
		}
		else if (Keyboard.current[Key.Escape].wasReleasedThisFrame)
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
			if (Keyboard.current[Key.Escape].wasReleasedThisFrame)
			{
				_modelList.SetActive(false);
				Main.CameraControl.BlockMouseWheelControl(false);
			}
		}

		var ctrlPressed = Keyboard.current[Key.LeftCtrl].isPressed || Keyboard.current[Key.RightCtrl].isPressed;
		if (ctrlPressed)
		{
			if (Keyboard.current[Key.C].wasPressedThisFrame)
			{
				Main.Gizmos.GetSelectedTargets(out var objectListForCopy);

				if (objectListForCopy.Count > 1)
				{
					Main.UIController?.SetWarningMessage("Multiple Object is selected. Only single object can be copied.");
				}
				else if (objectListForCopy.Count == 1)
				{
					_targetObjectForCopy = objectListForCopy[0];
					Main.Gizmos.ClearTargets();
					Main.UIController?.SetInfoMessage($"Copied: {_targetObjectForCopy.name}");
				}
				else
				{
					Main.UIController?.SetWarningMessage("No object selected to copy.");
				}
			}
			else if (Keyboard.current[Key.V].wasPressedThisFrame)
			{
				if (_targetObjectForCopy != null)
				{
					var instantiatedObject = Instantiate(_targetObjectForCopy, _targetObjectForCopy.root, true);
					instantiatedObject.name = $"{_targetObjectForCopy.name}_clone_{instantiatedObject.GetEntityId()}";

					if (_targetObjectForCopy.CompareTag("Road"))
					{
						var loftRoadOriginal = _targetObjectForCopy.GetComponent<Unity.Splines.LoftRoadGenerator>();
						var loftRoadNew = instantiatedObject.GetComponent<Unity.Splines.LoftRoadGenerator>();
						loftRoadNew.SdfMaterial = loftRoadOriginal.SdfMaterial;
					}

					SetModelForDeploy(instantiatedObject);

					var segmentationTag = instantiatedObject.GetComponentInChildren<Segmentation.Tag>();
					segmentationTag?.Refresh();
					Main.SegmentationManager.UpdateTags();
				}
				else
				{
					Main.UIController?.SetWarningMessage("No object to paste. Select an object and use Ctrl+C first.");
				}
			}
		}
	}
}