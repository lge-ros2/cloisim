/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class FollowingCamera : MonoBehaviour
{
	private bool _isFollowing = false;
	private Transform _targetObjectTransform = null;
	private CameraControl _cameraControl = null;

	[Header("Following Camera Parameters")]
	public bool blockControl = false;

	[Range(-20, 20)]
	public float horizontalOffset = 0;

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

	private bool _alignSameDirection = false;

	public void SetInitialRelativePosition(Vector3 position)
	{
		followingAngle = 0;
		horizontalOffset = position.x;
		distance = position.magnitude;
		height = position.y;
	}

	public void AlignSameDirection(in bool value)
	{
		_alignSameDirection = value;
	}

	// Start is called before the first frame update
	void Start()
	{
		_cameraControl = Main.CameraControl;
	}

	void LateUpdate()
	{
		if (!blockControl)
		{
			ChangeParameterByBaseInput();
		}

		if (_isFollowing && _targetObjectTransform != null)
		{
			var targetAngle = (_alignSameDirection) ?
				_targetObjectTransform.rotation.eulerAngles.y : followingAngle;
			var rotation = Quaternion.Euler(0, targetAngle, 0);

			transform.position
				= _targetObjectTransform.position
					- (rotation * Vector3.forward * distance)
					+ (Vector3.right * horizontalOffset)
					+ (Vector3.up * height);

			transform.LookAt(_targetObjectTransform);
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

			if (Input.GetKey(KeyCode.G))
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
				Main.UIController?.SetWarningMessage("'" + targetObjectName + "' model seems removed from the world.");
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
		if (_targetObjectTransform != null && !ReferenceEquals(_targetObjectTransform.gameObject, null))
		{
			Main.UIController?.SetInfoMessage("Camera view for '" + _targetObjectTransform.name + "' model is released.");
		}
		_targetObjectTransform = null;
		_isFollowing = false;
		Main.CameraControl?.UnBlockControl();
		this.blockControl = true;
	}

	private void LockTargetObject(in Transform targetTransform)
	{
		Main.UIController?.SetInfoMessage("Camera view for '" + targetTransform.name + "' model is locked.");
		_targetObjectTransform = targetTransform;
		_isFollowing = true;
		Main.CameraControl?.BlockControl();
		this.blockControl = false;
	}
}