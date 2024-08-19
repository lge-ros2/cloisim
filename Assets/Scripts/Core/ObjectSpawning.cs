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

[DefaultExecutionOrder(40)]
public class ObjectSpawning : MonoBehaviour
{
	public enum PropsType { BOX = 0, CYLINDER = 1, SPHERE = 2 };

	private static PhysicMaterial _propsPhysicalMaterial = null;
	private static Material _propMaterial = null;

	private GameObject _propsRoot = null;
	private Camera _mainCam = null;
	private UIController _uiController = null;
	private RuntimeGizmos.TransformGizmo transformGizmo = null;
	private FollowingTargetList _followingList = null;

	private const float CylinderRotationAngle = 90;

	private Dictionary<PropsType, GameObject> props = new Dictionary<PropsType, GameObject>();
	public float maxRayDistance = 100;
	private Dictionary<PropsType, uint> propsCount = new Dictionary<PropsType, uint>();

	private float _scaleFactor = 0.5f;
	private PropsType _propType = 0;

	private const float UnitMass = 3f;

	public void SetScaleFactor(in float value)
	{
		_scaleFactor = value;
	}

	public void SetPropType(in PropsType value)
	{
		_propType = value;
	}

	void Awake()
	{
		_propMaterial = SDF2Unity.Material.Create();
		_propsPhysicalMaterial = Resources.Load<PhysicMaterial>("PhysicsMaterials/Props");
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
		if (Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetMouseButtonUp(0))
			{
				// Add On left click spawn
				// selected prefab and align its rotation to a surface normal
				if (GetPositionAndNormalOnClick(out var hitPoint, out var hitNormal))
				{
					var propsScale = Vector3.one * _scaleFactor;
					StartCoroutine(SpawnTargetObject((PropsType)_propType, hitPoint, hitNormal, propsScale));
				}
			}
			else if (Input.GetMouseButtonUp(1))
			{
				// Remove spawned prefab when holding left control and right clicking
				var selectedPropsTransform = GetTransformOnClick();
				if (selectedPropsTransform)
				{
					StartCoroutine(DeleteTargetObject(selectedPropsTransform));
				}
			}
		}
		else if (Input.GetKeyUp(KeyCode.Alpha1))
		{
			ChnagePropType(PropsType.BOX);
		}
		else if (Input.GetKeyUp(KeyCode.Alpha2))
		{
			ChnagePropType(PropsType.CYLINDER);
		}
		else if (Input.GetKeyUp(KeyCode.Alpha3))
		{
			ChnagePropType(PropsType.SPHERE);
		}
		else if (Input.GetKeyUp(KeyCode.Delete))
		{
			transformGizmo.GetSelectedTargets(out var list);
			StartCoroutine(DeleteTargetObject(list));
			transformGizmo.ClearTargets();
		}
	}

	private void ChnagePropType(in PropsType type)
	{
		if (!_uiController.IsScaleFieldFocused())
		{
			SetPropType(type);
		}
	}

	private IEnumerator SpawnTargetObject(PropsType type, Vector3 position, Vector3 normal, Vector3 scale)
	{
		GameObject spawnedObject = null;
		Mesh mesh = null;

		if (!propsCount.ContainsKey(type))
		{
			propsCount.Add(type, 0);
		}

		if (!props.ContainsKey(type))
		{
			switch (type)
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
				var newTempPropsObject = CreateUnitProps(type, mesh);
				props.Add(type, newTempPropsObject);
				newTempPropsObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSave;
				newTempPropsObject.SetActive(false);
			}
		}

		var propsName = type.ToString() + "-" + propsCount[type]++;

		if (props.TryGetValue(type, out var targetObject))
		{
			spawnedObject = Instantiate(targetObject);
			spawnedObject.name = propsName;
			spawnedObject.SetActive(true);
			var meshFilter = spawnedObject.GetComponentInChildren<MeshFilter>();
			mesh = meshFilter.sharedMesh;

			const float SpawningMargin = 0.001f;
			position.y += mesh.bounds.max.y + SpawningMargin;

			var renderer = spawnedObject.GetComponentInChildren<Renderer>();
			var newColor = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.3f, 1f);
			renderer.material.SetColor("_BaseColor", newColor);

			var rigidBody = spawnedObject.GetComponentInChildren<Rigidbody>();
			rigidBody.mass = CalculateMass(scale);
			rigidBody.ResetCenterOfMass();
			rigidBody.ResetInertiaTensor();

			// var propTypeName = (type.ToString() + scale.ToString()).Trim();
			// Debug.Log(propTypeName);
			Main.SegmentationManager.AttachTag(type.ToString(), spawnedObject);
			Main.SegmentationManager.UpdateTags();
		}


		var spawanedObjectTransform = spawnedObject.transform;
		spawanedObjectTransform.position = position;
		spawanedObjectTransform.rotation = Quaternion.FromToRotation(spawanedObjectTransform.up, normal);

		spawnedObject.transform.localScale = scale;
		spawnedObject.transform.SetParent(_propsRoot.transform);

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
		newObject.isStatic = true;

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
		rigidBody.drag = 0.25f;
		rigidBody.angularDrag = 1f;

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
		for (var i = 0; i < targetObjectsTransform.Count; i++)
		{
			var targetObjectTransform = targetObjectsTransform[i];
			if (targetObjectTransform.CompareTag("Props") ||
				targetObjectTransform.CompareTag("Road") ||
				targetObjectTransform.CompareTag("Model"))
			{
				Destroy(targetObjectTransform.gameObject);
			}
		}

		yield return new WaitForEndOfFrame();

		_followingList?.UpdateList();

		yield return null;
	}

	private bool GetPositionAndNormalOnClick(out Vector3 hitPoint, out Vector3 hitNormal)
	{
		var ray = _mainCam.ScreenPointToRay(Input.mousePosition);
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
		var screenPoint2Ray = _mainCam.ScreenPointToRay(Input.mousePosition);

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