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
	private const float MAX_HEIGHT = 1000f;
	private const float MIN_HEIGHT = -1000f;

	private Actuator lift = new Actuator();

	private HashSet<GameObject> hashsetAllTopModels = new HashSet<GameObject>();
	private HashSet<GameObject> hashsetLiftingObjects = new HashSet<GameObject>();

	private UnityEvent finishedLiftingEvent = new UnityEvent();
	private GameObject rootModel = null;
	private Transform rootModelTransform = null;

	public string floorColliderName = string.Empty;
	private MeshCollider floorCollider = null;

	public float speed = 1;
	public bool IsMoving => lift.IsMoving;

	void Awake()
	{
		rootModel = Main.WorldRoot;
	}

	void Start()
	{
		rootModelTransform = rootModel.transform;

		lift.SetTarget(transform);
		lift.SetInitialPose(transform.localPosition);
		lift.SetMovingType(Actuator.MovingType.SmoothDamp);
		lift.SetMaxSpeed(speed);
		lift.SetDirection(Vector3.up);
		lift.SetMaxOffset(MAX_HEIGHT);
		lift.SetMinOffset(MIN_HEIGHT);
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
		var colliders = transform.GetComponentsInChildren<MeshCollider>();
		foreach (var collider in colliders)
		{
			if (collider.name.Equals(floorColliderName))
			{
				floorCollider = collider;
				floorCollider.convex = false;
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

		var allModelHelpers = rootModel.GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var modelHelper in allModelHelpers)
		{
			if (modelHelper.IsFirstChild)
			{
				hashsetAllTopModels.Add(modelHelper.gameObject);
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

			lift.SetTargetPosition(targetHeight);
			StartCoroutine(DoLifting());
		}
	}

	private IEnumerator DoLifting()
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