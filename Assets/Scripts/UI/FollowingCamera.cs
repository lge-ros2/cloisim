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

	[SerializeField]
	public float moveAmount = 0.1f;

	[SerializeField]
	public float angleStep = 1.5f;

	// Start is called before the first frame update
	void Start()
	{
		cameraControl = Main.CameraControl;
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
		if (!Input.GetKey(KeyCode.LeftControl))
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

			if (Input.GetKey(KeyCode.R))
			{
				height += moveAmount;
			}
			else if (Input.GetKey(KeyCode.F))
			{
				height -= moveAmount;
			}
		}
	}

	public void SetTargetObject(in string targetObjectName)
	{
		if (!string.IsNullOrEmpty(targetObjectName))
		{
			var targetObject = GameObject.Find(targetObjectName);
			if (targetObject != null)
			{
				LockTargetObject(targetObject.transform);
			}
			else
			{
				Main.Display?.SetWarningMessage("'" + targetObjectName + "' model seems removed from the world.");
				ReleaseTargetObject();
			}
		}
		else
		{
			ReleaseTargetObject();
		}
	}

	private void ReleaseTargetObject()
	{
		if (targetObjectTransform != null && !ReferenceEquals(targetObjectTransform.gameObject, null))
		{
			Main.Display?.SetInfoMessage("Camera view for '" + targetObjectTransform.name + "' model is released.");
		}
		targetObjectTransform = null;
		isFollowing = false;
		Main.CameraControl?.UnBlockControl();
		this.blockControl = true;
	}

	private void LockTargetObject(in Transform targetTransform)
	{
		Main.Display?.SetInfoMessage("Camera view for '" + targetTransform.name + "' model is locked.");
		targetObjectTransform = targetTransform;
		isFollowing = true;
		Main.CameraControl?.BlockControl();
		this.blockControl = false;
	}
}