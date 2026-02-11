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

	[Header("Following Camera Parameters")]
	public bool blockControl = false;

	[SerializeField]
	[Range(-20, 20)]
	private float _horizontalOffset = 0;

	[SerializeField]
	[Range(0.1f, 20)]
	private float _distance = 8f;

	[SerializeField]
	[Range(-20, 20)]
	private float _height = 3f;

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
		_horizontalOffset = position.x;
		_height = position.y;
		_distance = -position.z;
	}

	public void AlignSameDirection(in bool value)
	{
		_alignSameDirection = value;
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
			var yawRot = Quaternion.Euler(0, targetAngle, 0);

			var localOffset = new Vector3(_horizontalOffset, _height, -_distance);
			transform.position = _targetObjectTransform.position + (yawRot * localOffset);

			var toTarget = (_targetObjectTransform.position - transform.position);
			var dir = toTarget.normalized;

			var stableUp = yawRot * Vector3.forward;
			var verticalness = Mathf.Abs(Vector3.Dot(dir, Vector3.up));
			if (verticalness > 0.99999f)
			{
				dir = Vector3.down;
			}

			transform.rotation = Quaternion.LookRotation(dir, stableUp);
		}
	}

	private void ChangeParameterByBaseInput()
	{
		if (!Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetKey(KeyCode.W))
			{
				const float blockZeroDistance = 0.001f;

				if (_distance > moveAmount + blockZeroDistance)
				{
					_distance -= moveAmount;
				}
			}
			else if (Input.GetKey(KeyCode.S))
			{
				_distance += moveAmount;
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
				_height += moveAmount;
			}
			else if (Input.GetKey(KeyCode.F))
			{
				_height -= moveAmount;
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