/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class LiftControl : MonoBehaviour
{
	private Actuator lift;

	private HashSet<GameObject> hashsetAllTopModels;
	private HashSet<GameObject> hashsetLiftingObjects;

	private UnityEvent finishedLiftingEvent;
	private GameObject rootModel = null;
	private Transform rootModelTransform = null;

	public string floorColliderName = string.Empty;
	private MeshCollider floorCollider = null;
	private Vector3 targetPosition = Vector3.zero;

	public float speed = 1;
	public bool IsMoving => lift.IsMoving;

	LiftControl()
	{
		lift = new Actuator();
		hashsetAllTopModels = new HashSet<GameObject>();
		hashsetLiftingObjects = new HashSet<GameObject>();
	}

	void Awake()
	{
		lift.SetTarget(transform);
		rootModel = GameObject.Find("Models");
		rootModelTransform = rootModel.transform;
		finishedLiftingEvent = new UnityEvent();
	}

	void Start()
	{
		lift.SetMovingType(Actuator.MovingType.SmoothDamp);
		lift.SetMaxSpeed(speed);
		// Debug.Log(name + "::" + speed);

		FindFloorRegionInLift();
		UpdateTopModels();
	}

	public void SetFinishedEventListener(UnityAction call)
	{
		finishedLiftingEvent.AddListener(call);
	}

	private void FindFloorRegionInLift()
	{
		foreach (Transform child in transform)
		{
			if (floorCollider != null)
			{
				break;
			}

			foreach (Transform grandChild in child.transform)
			{
				if (grandChild.name.Equals(floorColliderName))
				{
					floorCollider = grandChild.GetComponent<MeshCollider>();
					floorCollider.convex = false;
					break;
				}
			}
		}
	}

	public void UpdateTopModels()
	{
		if (rootModel == null)
		{
			Debug.LogError("root model is not assigned yet.");
			return;
		}

		var allModelPlugins = rootModel.GetComponentsInChildren<ModelPlugin>();
		foreach (var modelPlugin in allModelPlugins)
		{
			if (modelPlugin.IsTopModel)
			{
				hashsetAllTopModels.Add(modelPlugin.gameObject);
			}
		}
	}

	private void DetectObjectsToLiftAndLiftIt()
	{
		hashsetLiftingObjects.Clear();
		foreach (var topModel in hashsetAllTopModels)
		{
			if (topModel != null)
			{
				var topModelPosition = topModel.transform.position;
				if (floorCollider != null && floorCollider.bounds.Contains(topModelPosition))
				{
					hashsetLiftingObjects.Add(topModel);

					topModel.transform.SetParent(transform, true);
				}
			}
		}
	}

	private void DropLiftedObjects()
	{
		// Unlink parenting between lifted objects if arrived at the target floor.
		foreach (var obj in hashsetLiftingObjects)
		{
			if (rootModelTransform != null)
			{
				obj.transform.SetParent(rootModelTransform, true);
			}
		}
	}

	public void MoveTo(in float targetHeight)
	{
		if (!lift.IsMoving)
		{
			DetectObjectsToLiftAndLiftIt();

			lift.SetTargetPosition(Vector3.up, targetHeight);
			StartCoroutine(RunLifting());
		}
	}

	private IEnumerator RunLifting()
	{
		var waitForFixedUpdate = new WaitForFixedUpdate();
		yield return waitForFixedUpdate;

		do
		{
			yield return waitForFixedUpdate;
			lift.Drive();

		} while(lift.IsMoving);

		DropLiftedObjects();

		finishedLiftingEvent.Invoke();
	}

// #if UNITY_EDITOR
// 	// just for test
// 	void Update()
// 	{
// 		if (!lift.IsMoving)
// 		{
// 			if (Input.GetKeyUp(KeyCode.U))
// 			{
// 				MoveTo(600);
// 			}
// 			else if (Input.GetKeyUp(KeyCode.J))
// 			{
// 				MoveTo(-600);
// 			}
// 		}
// 	}
// #endif
}