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

	private HashSet<GameObject> hashsetLiftingObjects = new HashSet<GameObject>();
	private HashSet<GameObject> hashsetLiftingProps = new HashSet<GameObject>();

	private UnityEvent finishedLiftingEvent = new UnityEvent();
	private Transform rootModelTransform = null;
	private Transform rootPropsTransform = null;

	public string floorColliderName = string.Empty;
	private MeshCollider floorCollider = null;

	public float speed = 1;
	public bool IsMoving => lift.IsMoving;

	void Awake()
	{
		rootModelTransform = Main.WorldRoot.transform;
		rootPropsTransform = Main.PropsRoot.transform;
	}

	void Start()
	{
		lift.SetTarget(transform);
		lift.SetInitialPose(transform.localPosition);
		lift.SetMovingType(Actuator.MovingType.SmoothDamp);
		lift.SetMaxSpeed(speed);
		lift.SetDirection(Vector3.up);
		lift.SetMaxOffset(MAX_HEIGHT);
		lift.SetMinOffset(MIN_HEIGHT);
		// Debug.Log(name + "::" + speed);

		FindFloorRegionInLift();
	}

	public void SetFinishedEventListener(UnityAction call)
	{
		finishedLiftingEvent.AddListener(call);
	}

	private void FindFloorRegionInLift()
	{
		var collisions = transform.GetComponentsInChildren<SDF.Helper.Collision>();
		foreach (var collision in collisions)
		{
			if (collision.name.Equals(floorColliderName))
			{
				floorCollider = collision.GetComponentInChildren<MeshCollider>();
				floorCollider.convex = false;
			}
		}
	}

	private void DetectObjectsToLiftAndLiftIt()
	{
		hashsetLiftingObjects.Clear();
		hashsetLiftingProps.Clear();

		var allModelHelpers = rootModelTransform.GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var modelHelper in allModelHelpers)
		{
			if (modelHelper.IsFirstChild)
			{
				var topModel = modelHelper.gameObject;
				var topModelPosition = topModel.transform.position;
				if (floorCollider != null && floorCollider.bounds.Contains(topModelPosition))
				{
					hashsetLiftingObjects.Add(topModel);
					topModel.transform.SetParent(transform, true);
				}
			}
		}

		var allProps = rootPropsTransform.GetComponentsInChildren<Transform>();
		foreach (var prop in allProps)
		{
			if (prop.CompareTag("Props"))
			{
				var propPosition = prop.transform.position;
				if (floorCollider != null && floorCollider.bounds.Contains(propPosition))
				{
					var propObject = prop.gameObject;
					hashsetLiftingProps.Add(propObject);
					prop.transform.SetParent(transform, true);
				}
			}
		}
	}

	private void DropLiftedObjects()
	{
		// Unlink parenting between lifted objects if arrived at the target floor.
		if (rootModelTransform != null)
		{
			foreach (var obj in hashsetLiftingObjects)
			{
				obj.transform.SetParent(rootModelTransform, true);
			}
		}

		if (rootPropsTransform != null)
		{
			foreach (var obj in hashsetLiftingProps)
			{
				obj.transform.SetParent(rootPropsTransform, true);
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
		var waitForEOF = new WaitForEndOfFrame();

		var bodyOriginalTransform = new Dictionary<ArticulationBody, Transform>();

		// handling the gameobhect which has articulation body
		foreach (var obj in hashsetLiftingObjects)
		{
			var articulationBodies = obj.GetComponentsInChildren<ArticulationBody>();
			foreach (var articulationBody in articulationBodies)
			{
				if (articulationBody.isRoot)
				{
					bodyOriginalTransform.Add(articulationBody, articulationBody.transform);
					break;
				}
			}
		}

		do
		{
			lift.Drive();

			// find root articulation body and teleport the body following as
			foreach (var item in bodyOriginalTransform)
			{
				var articulationBody = item.Key;
				var originalTransform = item.Value;
				var newWorldPose = originalTransform.position;
				newWorldPose.y = transform.localPosition.y;
				articulationBody.Sleep();
				articulationBody.TeleportRoot(newWorldPose, originalTransform.localRotation);
			}

			yield return waitForEOF;
		} while (lift.IsMoving);

		bodyOriginalTransform.Clear();

		DropLiftedObjects();

		finishedLiftingEvent.Invoke();

		yield return null;
	}

#if UNITY_EDITOR
	// just for test
	// void Update()
	// {
	// 	if (!lift.IsMoving)
	// 	{
	// 		if (Input.GetKeyUp(KeyCode.U))
	// 		{
	// 			MoveTo(600);
	// 		}
	// 		else if (Input.GetKeyUp(KeyCode.J))
	// 		{
	// 			MoveTo(-600);
	// 		}
	// 	}
	// }
#endif
}