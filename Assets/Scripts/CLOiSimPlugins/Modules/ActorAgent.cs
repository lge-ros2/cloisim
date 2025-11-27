/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walk to a random position and repeat or specify the destination
/// </summary>
[DefaultExecutionOrder(601)]
[RequireComponent(typeof(NavMeshAgent))]
public class ActorAgent : MonoBehaviour
{
	public enum Type {STANDBY, MOVING};

	private Type currentType = Type.STANDBY;

	private NavMeshAgent _navMeshAgent;
	private Animation _animation;

	[SerializeField]
	private float _goalTolerance = 0.1f;

	[SerializeField]
	private float _maxTargetRange = 5f;

	[SerializeField]
	private bool _isRandomWalking = true;

	private Dictionary<Type, string> motionTypeAnimations = new Dictionary<Type, string>()
	{
		{Type.STANDBY, ""},
		{Type.MOVING, ""}
	};

	public bool RandomWalking
	{
		get => _isRandomWalking;
		set => _isRandomWalking = value;
	}

	void Awake()
	{
		_navMeshAgent = GetComponent<NavMeshAgent>();
		_animation = GetComponentInChildren<Animation>();
		_navMeshAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
		_navMeshAgent.agentTypeID = WorldNavMeshBuilder.AgentTypeId;
		_navMeshAgent.autoTraverseOffMeshLink = false;
	}

	void Start()
	{
		Stop();
	}

	void LateUpdate()
	{
		if (_navMeshAgent == null || _navMeshAgent.pathStatus.Equals(NavMeshPathStatus.PathInvalid))
		{
			// Debug.LogWarning("agent is null or path status is invalid");
			return;
		}

		if (!_navMeshAgent.pathPending)
		{
			if (_navMeshAgent.remainingDistance < _goalTolerance)
			{
				// Debug.LogWarning("remainingDistance:" + _navMeshAgent.remainingDistance);
				Stop();

				if (_isRandomWalking)
				{
					var nextTarget = _maxTargetRange * Random.insideUnitCircle;
					// Debug.Log("next random moving: " + nextTarget.ToString("F7"));
					AssignTargetDestination(nextTarget);
				}
			}
		}
		// else
		// {
		// 	Debug.LogWarning("pathPending");
		// }
	}

	public void Stop()
	{
		if (_navMeshAgent && _navMeshAgent.isOnNavMesh)
		{
			// Debug.Log("stop");
			_navMeshAgent.isStopped = true;
			_navMeshAgent.SetDestination(transform.position);
			SetAnimationMotion(Type.STANDBY);
		}
	}

	public void AssignTargetDestination(in Vector3 point)
	{
	 	Stop();

		if (_navMeshAgent && _navMeshAgent.isOnNavMesh)
		{
			SetAnimationMotion(Type.MOVING);
			_navMeshAgent.isStopped = false;
			_navMeshAgent.SetDestination(point);
		}
	}

	public void SetMotionType(in Type type, in string animationName)
	{
		if (motionTypeAnimations.ContainsKey(type))
		{
			motionTypeAnimations[type] = animationName;
		}
	}

	private void SetAnimationMotion(in Type motionType)
	{
		if (_animation)
		{
			var animationName = motionTypeAnimations[motionType];

			if (currentType != motionType)
			{
				_animation.Stop();
				_animation.Play(animationName);

				currentType = motionType;
			}
		}
	}

	public void SetSteering(in float speed, in float angularSpeed, in float acceleration)
	{
		if (_navMeshAgent)
		{
			_navMeshAgent.speed = speed;
			_navMeshAgent.angularSpeed = angularSpeed;
			_navMeshAgent.acceleration = acceleration;
		}
	}
}