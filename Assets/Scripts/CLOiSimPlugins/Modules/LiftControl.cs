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

	private Actuator _lift = new();

	private Dictionary<ArticulationBody, Transform> _bodyOriginalTransform = new();
	private HashSet<GameObject> _hashsetLiftingObjects = new();
	private HashSet<GameObject> _hashsetLiftingProps = new();

	private UnityEvent _finishedLiftingEvent = new();
	private Transform _rootModelTransform = null;
	private Transform _rootPropsTransform = null;

	public string floorColliderName = string.Empty;
	private MeshCollider _floorCollider = null;

	public float speed = 1;
	public bool IsMoving => _lift.IsMoving;

	void Awake()
	{
		_rootModelTransform = Main.WorldRoot.transform;
		_rootPropsTransform = Main.PropsRoot.transform;
	}

	void Start()
	{
		_lift.SetTarget(transform);
		_lift.SetInitialPose(transform.localPosition);
		_lift.SetMovingType(Actuator.MovingType.SmoothDamp);
		_lift.SetMaxSpeed(speed);
		_lift.SetDirection(Vector3.up);
		_lift.SetMaxOffset(MAX_HEIGHT);
		_lift.SetMinOffset(MIN_HEIGHT);
		// Debug.Log(name + "::" + speed);

		FindFloorRegionInLift();
	}

	public void SetFinishedEventListener(UnityAction call)
	{
		_finishedLiftingEvent.AddListener(call);
	}

	private void FindFloorRegionInLift()
	{
		var collisions = transform.GetComponentsInChildren<SDF.Helper.Collision>();
		foreach (var collision in collisions)
		{
			if (collision.name.Equals(floorColliderName))
			{
				_floorCollider = collision.GetComponentInChildren<MeshCollider>();
				_floorCollider.convex = false;
			}
		}
	}

	private void DetectObjectsToLiftAndLiftIt()
	{
		_hashsetLiftingObjects.Clear();
		_hashsetLiftingProps.Clear();

		var allModelHelpers = _rootModelTransform.GetComponentsInChildren<SDF.Helper.Model>();
		foreach (var modelHelper in allModelHelpers)
		{
			if (modelHelper.IsFirstChild)
			{
				var topModel = modelHelper.gameObject;
				var topModelPosition = topModel.transform.position;
				if (_floorCollider != null && _floorCollider.bounds.Contains(topModelPosition))
				{
					_hashsetLiftingObjects.Add(topModel);
					topModel.transform.SetParent(transform, true);
				}
			}
		}

		var allProps = _rootPropsTransform.GetComponentsInChildren<Transform>();
		foreach (var prop in allProps)
		{
			if (prop.CompareTag("Props"))
			{
				var propPosition = prop.transform.position;
				if (_floorCollider != null && _floorCollider.bounds.Contains(propPosition))
				{
					var propObject = prop.gameObject;
					_hashsetLiftingProps.Add(propObject);
					prop.transform.SetParent(transform, true);
				}
			}
		}
	}

	private void DropLiftedObjects()
	{
		// Unlink parenting between lifted objects if arrived at the target floor.
		if (_rootModelTransform != null)
		{
			foreach (var obj in _hashsetLiftingObjects)
			{
				obj.transform.SetParent(_rootModelTransform, true);
			}
		}

		if (_rootPropsTransform != null)
		{
			foreach (var obj in _hashsetLiftingProps)
			{
				obj.transform.SetParent(_rootPropsTransform, true);
			}
		}
	}

	public void MoveTo(in float targetHeight)
	{
		if (!_lift.IsMoving)
		{
			DetectObjectsToLiftAndLiftIt();

			_lift.SetTargetPosition(targetHeight);
			StartCoroutine(DoLifting());
		}
	}

	private IEnumerator DoLifting()
	{
		// handling the gameobhect which has articulation body
		foreach (var obj in _hashsetLiftingObjects)
		{
			var articulationBodies = obj.GetComponentsInChildren<ArticulationBody>();
			foreach (var articulationBody in articulationBodies)
			{
				if (articulationBody.isRoot)
				{
					_bodyOriginalTransform.Add(articulationBody, articulationBody.transform);
					break;
				}
			}
		}

		var waitForEOF = new WaitForEndOfFrame();
		var waitForFU = new WaitForFixedUpdate();

		do
		{
			_lift.Drive();
			yield return waitForEOF;
			yield return waitForFU;
		} while (_lift.IsMoving);

		_bodyOriginalTransform.Clear();

		DropLiftedObjects();
		_finishedLiftingEvent.Invoke();

		yield return null;
	}

	void FixedUpdate()
	{
		const float GAP_BETWEEN_ELEVATOR_FLOOR = 0.02f;

		if (_bodyOriginalTransform.Count > 0)
		{
			// find root articulation body and teleport the body following as elevator's height
			foreach (var item in _bodyOriginalTransform)
			{
				var articulationBody = item.Key;
				var originalTransform = item.Value;
				var newWorldPose = originalTransform.position;
				newWorldPose.y = transform.localPosition.y + GAP_BETWEEN_ELEVATOR_FLOOR;
				articulationBody.Sleep();
				articulationBody.TeleportRoot(newWorldPose, originalTransform.localRotation);
			}
		}
	}

#if UNITY_EDITOR
	// just for test
	// void Update()
	// {
	// 	if (!_lift.IsMoving)
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