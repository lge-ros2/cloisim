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

public class ObjectSpawning : MonoBehaviour
{
	public enum PropsType { BOX = 0, CYLINDER = 1, SPHERE = 2 };

	private static PhysicMaterial PropsPhysicalMaterial = null;

	private GameObject propsRoot = null;
	private Camera mainCam = null;
	private RuntimeGizmos.TransformGizmo transformGizmo = null;
	private FollowingTargetList followingList = null;

	private const float CylinderRotationAngle = 90;

	private Dictionary<PropsType, GameObject> props = new Dictionary<PropsType, GameObject>();
	public float maxRayDistance = 100;
	private Dictionary<PropsType, uint> propsCount = new Dictionary<PropsType, uint>();

	private string scaleFactorString = "0.5";
	private int propType = 0;

	private const float UnitMass = 3f;

	public void SetScaleFactor(in string value)
	{
		scaleFactorString = value;
	}

	public void SetPropType(in int value)
	{
		propType = value;
	}

	void Awake()
	{
		PropsPhysicalMaterial = Resources.Load<PhysicMaterial>("Materials/Props");
		propsRoot = GameObject.Find("Props");
		mainCam = Camera.main;
		transformGizmo = Main.Gizmos;

		if (Main.UIMainCanvas != null)
		{
			followingList = Main.UIMainCanvas.GetComponentInChildren<FollowingTargetList>();
		}
		else
		{
			Debug.LogError("Main.UIMainCanvas is not ready!!");
		}
	}

	void OnDestroy()
	{
		Resources.UnloadAsset(PropsPhysicalMaterial);
	}

	// Update is called once per frame
	void LateUpdate()
	{
		var leftControlPressed = Input.GetKey(KeyCode.LeftControl);

		if (leftControlPressed && Input.GetMouseButtonDown(0))
		{
			// Add On left click spawn
			// selected prefab and align its rotation to a surface normal
			var spawnData = GetPositionAndNormalOnClick();
			var scaleFactor = float.Parse(scaleFactorString);
			var propsScale = Vector3.one * scaleFactor;
			StartCoroutine(SpawnTargetObject((PropsType)propType, spawnData[0], spawnData[1], propsScale));
		}
		else if (leftControlPressed && Input.GetMouseButtonDown(1))
		{
			// Remove spawned prefab when holding left control and right clicking
			var selectedPropsTransform = GetTransformOnClick();
			if (selectedPropsTransform)
			{
				StartCoroutine(DeleteTargetObject(selectedPropsTransform));
			}
		}
		else if (Input.GetKey(KeyCode.Delete))
		{
			transformGizmo.GetSelectedTargets(out var list);
			StartCoroutine(DeleteTargetObject(list));
			transformGizmo.ClearTargets();
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
					mesh = ProceduralMesh.CreateSphere(0.5f, 14, 10);
					break;
			}

			if (mesh != null)
			{
				var newTempPropsObject = CreateProps(type.ToString(), mesh, scale);
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

			var meshRender = spawnedObject.GetComponentInChildren<MeshRenderer>();
			meshRender.material.color = Random.ColorHSV(0f, 1f, 0.4f, 1f, 0.3f, 1f);

			var rigidBody = spawnedObject.GetComponentInChildren<Rigidbody>();
			rigidBody.mass = CalculateMass(scale);
			rigidBody.ResetCenterOfMass();
			rigidBody.ResetInertiaTensor();

			// var propTypeName = (type.ToString() + scale.ToString()).Trim();
			// Debug.Log(propTypeName);
			SegmentationManager.AttachTag(type.ToString(), spawnedObject);

			Main.SegmentationManager.UpdateTags();
		}

		if (mesh != null)
		{
			const float SpawningMargin = 0.001f;
			position.y += mesh.bounds.max.y + SpawningMargin;
		}

		var spawanedObjectTransform = spawnedObject.transform;
		spawanedObjectTransform.position = position;
		spawanedObjectTransform.rotation = Quaternion.FromToRotation(spawanedObjectTransform.up, normal);

		spawnedObject.transform.localScale = scale;
		spawnedObject.transform.SetParent(propsRoot.transform);

		yield return null;
	}

	private float CalculateMass(in Vector3 scale)
	{
		return (scale.x + scale.y + scale.z) / 3 * UnitMass;
	}

	private GameObject CreateProps(in string name, in Mesh targetMesh, in Vector3 scale)
	{
		var newObject = new GameObject(name);
		newObject.tag = "Props";
		newObject.isStatic = true;

		var meshFilter = newObject.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = targetMesh;

		var newMaterial = new Material(SDF2Unity.CommonShader);
		newMaterial.name = targetMesh.name;
		newMaterial.color = Color.white;

		var meshRenderer = newObject.AddComponent<MeshRenderer>();
		meshRenderer.material = newMaterial;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;

		var meshCollider = newObject.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = targetMesh;
		meshCollider.sharedMaterial = PropsPhysicalMaterial;
		meshCollider.convex = true;
		meshCollider.isTrigger = false;

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

		newObject.AddComponent<SegmentationTag>();

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
			if (targetObjectTransform.CompareTag("Props") || targetObjectTransform.CompareTag("Model"))
			{
				Destroy(targetObjectTransform.gameObject);
				yield return new WaitForEndOfFrame();
			}
		}

		yield return new WaitForEndOfFrame();

		followingList?.UpdateList();

		yield return null;
	}

	private Vector3[] GetPositionAndNormalOnClick()
	{
		var returnData = new Vector3[] { Vector3.zero, Vector3.zero }; //0 = spawn poisiton, 1 = surface normal
		var ray = mainCam.ScreenPointToRay(Input.mousePosition);

		if (Physics.Raycast(ray, out var hit, maxRayDistance))
		{
			returnData[0] = hit.point;
			returnData[1] = hit.normal;
		}

		return returnData;
	}

	private Transform GetTransformOnClick()
	{
		var screenPoint2Ray = mainCam.ScreenPointToRay(Input.mousePosition);

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