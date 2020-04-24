/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class FollowingCamera : MonoBehaviour
{
	private bool isFollowing = false;
	private Transform targetObjectTransform = null;
	private CameraControl cameraControl = null;

	[Header("Following Camera Parameters")]

	public bool blockControl = false;

	[Range(0.1f, 20)]
	public float distance = 8f;

	[Range(-20, 20)]
	public float height = 3f;

	[Range(-180, 180)]
	public float followingAngle = 40f;

	public float moveAmount = 0.1f;
	public float angleStep = 1.5f;

	// Start is called before the first frame update
	void Start()
	{
		cameraControl = GetComponent<CameraControl>();
	}

	void LateUpdate()
	{
		if (!blockControl)
		{
			ChangeParameterByBaseInput();
		}

		if (isFollowing && targetObjectTransform != null)
		{
			var rotation = Quaternion.Euler(0, followingAngle, 0);

			transform.position
				= targetObjectTransform.position
					- (rotation * Vector3.forward * distance) + (Vector3.up * height);

			transform.LookAt(targetObjectTransform);
		}
	}

	private void ChangeParameterByBaseInput()
	{

		if (Input.GetKey(KeyCode.W))
		{
			const float blockZeroDistance = 0.001f;

			if (distance > moveAmount + blockZeroDistance)
			{
				distance -= moveAmount;
			}
		}
		else if (Input.GetKey(KeyCode.S))
		{
			distance += moveAmount;
		}

		if (Input.GetKey(KeyCode.A))
		{
			followingAngle += (angleStep);
		}
		else if (Input.GetKey(KeyCode.D))
		{
			followingAngle -= (angleStep);
		}

		if (Input.GetKey(KeyCode.Q))
		{
			height += moveAmount;
		}
		else if (Input.GetKey(KeyCode.Z))
		{
			height -= moveAmount;
		}
	}

	public void SetTargetObject(in string targetObjectName)
	{
		if (!string.IsNullOrEmpty(targetObjectName))
		{
			var targetObject = GameObject.Find(targetObjectName);
			targetObjectTransform = targetObject.transform;
			isFollowing = true;
			cameraControl.blockControl = true;
			this.blockControl = false;
		}
		else
		{
			targetObjectTransform = null;
			isFollowing = false;
			cameraControl.blockControl = false;
			this.blockControl = true;
		}
	}
}