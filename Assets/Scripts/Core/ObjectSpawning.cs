/*
 * Copyright (c) 2021 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(40)]
public class ObjectSpawning : MonoBehaviour
{
	public enum PropsType { NONE = 0, BOX = 1, CYLINDER = 2, SPHERE = 3 };
	private const float UnitMass = 3.5f;
	private const float CylinderRotationAngle = 90;

	private static PhysicsMaterial _propsPhysicalMaterial = null;
	private static Material _propMaterial = null;

	private GameObject _propsRoot = null;
	private Camera _mainCam = null;
	private UIController _uiController = null;
	private RuntimeGizmos.TransformGizmo transformGizmo = null;
	private FollowingTargetList _followingList = null;

	private Dictionary<PropsType, GameObject> props = new();
	public float maxRayDistance = 100;
	private Dictionary<PropsType, uint> propsCount = new();

	private float _scaleFactor = 0.5f;
	private PropsType _propType = PropsType.NONE;

	public void SetScaleFactor(in float value)
	{
		_scaleFactor = value;
	}

	public void SetPropType(in PropsType value)
	{
		_propType = value;
	}

	public PropsType GetPropType()
	{
		return _propType;
	}

	void Awake()
	{
		_propMaterial = SDF2Unity.CreateMaterial();
		_propsPhysicalMaterial = Resources.Load<PhysicsMaterial>("PhysicsMaterials/Props");
		_propsRoot = GameObject.Find("Props");
		_mainCam = Camera.main;
		_uiController = Main.UIObject?.GetComponent<UIController>();
		transformGizmo = Main.Gizmos;

		if (Main.UIMainCanvas != null)
		{
			_followingList = Main.UIMainCanvas.GetComponentInChildren<FollowingTargetList>();
		}
		else
		{
			Debug.LogError("Main.UIMainCanvas is not ready!!");
		}
	}

	void OnDestroy()
	{
		Resources.UnloadAsset(_propsPhysicalMaterial);
	}

	// Update is called once per frame
	void LateUpdate()
	{
		if (Keyboard.current[Key.LeftCtrl].isPressed)
		{
			if (Mouse.current.leftButton.wasReleasedThisFrame)
			{
				// Add On left click spawn
				// selected prefab and align its rotation to a surface normal
				if (GetPositionAndNormalOnClick(out var hitPoint, out var hitNormal))
				{
					var propsScale = Vector3.one * _scaleFactor;
					StartCoroutine(SpawnTargetObject(hitPoint, hitNormal, propsScale));
				}
			}
			else if (Mouse.current.rightButton.wasReleasedThisFrame)
			{
				// Remove spawned prefab when holding left control and right clicking
				var selectedPropsTransform = GetTransformOnClick();
				if (selectedPropsTransform)
				{
					StartCoroutine(DeleteTargetObject(selectedPropsTransform));
				}
			}
		}
		else if (Keyboard.current[Key.Delete].wasReleasedThisFrame)
		{
			transformGizmo.GetSelectedTargets(out var list);
			StartCoroutine(DeleteTargetObject(list));
			transformGizmo.ClearTargets();
		}
	}

	private IEnumerator SpawnTargetObject(Vector3 position, Vector3 normal, Vector3 scale)
	{
		if (_propType == PropsType.NONE)
		{
			Main.UIController.SetWarningMessage($"Select props type first!!!!");
			yield break;
		}
		Main.UIController.ClearMessage();

		if (!propsCount.ContainsKey(_propType))
		{
			propsCount.Add(_propType, 0);
		}

		Mesh mesh = null;
		if (!props.ContainsKey(_propType))
		{
			switch (_propType)
			{
				case PropsType.BOX:
					mesh = ProceduralMesh.CreateBox();
					break;

				case PropsType.CYLINDER:
					mesh = ProceduralMesh.CreateCylinder(0.5f, 1, 20, CylinderRotationAngle);
					break;

				case PropsType.SPHERE:
					mesh = ProceduralMesh.CreateSphere(0.5f, 13, 13);
					break;
			}

			if (mesh != null)
			{
				var newTempPropsObject = CreateUnitProps(_propType, mesh);
				props.Add(_propType, newTempPropsObject);
				newTempPropsObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSave;
				newTempPropsObject.SetActive(false);
			}
		}

		var propsName = _propType.ToString() + "-" + propsCount[_propType]++;

		GameObject spawnedObject = null;
		if (props.TryGetValue(_propType, out var targetObject))
		{
			spawnedObject = Instantiate(targetObject);
			spawnedObject.name = propsName;
			spawnedObject.hideFlags = HideFlags.None; // Ensure spawned props are visible to FindObjectsByType
			spawnedObject.SetActive(false);

			// Do NOT activate yet — set position/scale first to avoid physics jitter
			var meshFilter = spawnedObject.GetComponentInChildren<MeshFilter>(true);
			mesh = meshFilter.sharedMesh;

			const float SpawningMargin = 0.01f;
			position.y += mesh.bounds.max.y + SpawningMargin;

			// Set transform BEFORE activation so physics starts at the correct pose
			var spawanedObjectTransform = spawnedObject.transform;
			spawanedObjectTransform.SetParent(_propsRoot.transform);
			spawanedObjectTransform.position = position;
			spawanedObjectTransform.rotation = Quaternion.FromToRotation(spawanedObjectTransform.up, normal);
			spawanedObjectTransform.localScale = scale;

			// Now activate — physics will start from the correct position
			spawnedObject.SetActive(true);

			var renderer = spawnedObject.GetComponentInChildren<Renderer>();
			var newColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.3f, 1f);
			renderer.material.SetColor("_BaseColor", newColor);

			var rigidBody = spawnedObject.GetComponentInChildren<Rigidbody>();
			rigidBody.mass = CalculateMass(scale);
			rigidBody.ResetCenterOfMass();
			rigidBody.ResetInertiaTensor();

			Main.SegmentationManager.AttachTag(_propType.ToString(), spawnedObject);
			Main.SegmentationManager.UpdateTags();
		}

		yield return null;
	}

	private float CalculateMass(in Vector3 scale)
	{
		return (scale.x + scale.y + scale.z) / 3 * UnitMass;
	}

	private GameObject CreateUnitProps(in PropsType type, in Mesh targetMesh)
	{
		var newObject = new GameObject(type.ToString());
		newObject.tag = "Props";
		newObject.isStatic = false;

		var meshFilter = newObject.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = targetMesh;

		var meshRenderer = newObject.AddComponent<MeshRenderer>();
		meshRenderer.shadowCastingMode = ShadowCastingMode.On;
		meshRenderer.receiveShadows = true;
		meshRenderer.sharedMaterial = _propMaterial;

		meshRenderer.material.name = targetMesh.name;
		meshRenderer.material.color = Color.white;

		switch (type)
		{
			case PropsType.BOX:
				var boxCollider = newObject.AddComponent<BoxCollider>();
				boxCollider.center = Vector3.zero;
				boxCollider.size = Vector3.one;
				break;

			case PropsType.SPHERE:
				var sphereCollider = newObject.AddComponent<SphereCollider>();
				sphereCollider.center = Vector3.zero;
				sphereCollider.radius = 0.5f;
				break;

			case PropsType.CYLINDER:
			default:
				var meshCollider = newObject.AddComponent<MeshCollider>();
				meshCollider.sharedMesh = targetMesh;
				meshCollider.convex = true;
				meshCollider.isTrigger = false;
				break;
		}

		var collider = newObject.GetComponent<Collider>();
		collider.sharedMaterial = _propsPhysicalMaterial;

		var rigidBody = newObject.AddComponent<Rigidbody>();
		rigidBody.mass = 1;
		rigidBody.linearDamping = 2f;
		rigidBody.angularDamping = 2f;
		rigidBody.sleepThreshold = 0.05f;
		rigidBody.interpolation = RigidbodyInterpolation.Interpolate;

		var navMeshObstacle = newObject.AddComponent<NavMeshObstacle>();
		navMeshObstacle.carving = true;
		navMeshObstacle.size = Vector3.one;
		navMeshObstacle.carvingMoveThreshold = 0.1f;
		navMeshObstacle.carvingTimeToStationary = 0.2f;
		navMeshObstacle.carveOnlyStationary = true;

		newObject.AddComponent<Segmentation.Tag>();

		GameObject.DontDestroyOnLoad(newObject);

		return newObject;
	}

	private IEnumerator DeleteTargetObject(Transform targetObjectTransform)
	{
		var newList = new List<Transform>() { targetObjectTransform };
		yield return DeleteTargetObject(newList);
	}

	private IEnumerator DeleteTargetObject(List<Transform> targetObjectsTransform)
	{
		var targets = new List<Transform>(targetObjectsTransform);
		for (var i = 0; i < targets.Count; i++)
		{
			var targetObjectTransform = targets[i];
			if (targetObjectTransform == null)
				continue;

			if (targetObjectTransform.CompareTag("Props") ||
				targetObjectTransform.CompareTag("Road") ||
				targetObjectTransform.CompareTag("Model"))
			{
				Destroy(targetObjectTransform.gameObject);
				yield return null;
			}
		}		
		_followingList?.UpdateList();

		yield return null;
	}

	private bool GetPositionAndNormalOnClick(out Vector3 hitPoint, out Vector3 hitNormal)
	{
		var ray = _mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());
		if (Physics.Raycast(ray, out var hit, maxRayDistance))
		{
			hitPoint = hit.point; // 0 = spawn poisiton
			hitNormal = hit.normal; // 1 = surface normal
			return true;
		}
		else
		{
			hitPoint = Vector3.positiveInfinity;
			hitNormal = Vector3.positiveInfinity;
		}

		return false;
	}

	private Transform GetTransformOnClick()
	{
		var screenPoint2Ray = _mainCam.ScreenPointToRay(Mouse.current.position.ReadValue());

		if (Physics.Raycast(screenPoint2Ray, out var hit, maxRayDistance))
		{
			var parent = hit.transform.parent;
			if (parent.name.Equals("Props") && hit.transform.CompareTag("Props"))
			{
				return hit.transform;
			}
		}

		return null;
	}
}