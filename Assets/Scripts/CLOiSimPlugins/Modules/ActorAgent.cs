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
[RequireComponent(typeof(NavMeshAgent))]
public class ActorAgent : MonoBehaviour
{
	public enum Type {STANDBY, MOVING};

	private Type currentType = Type.STANDBY;

	private NavMeshAgent m_Agent;
	private Animation m_Animation;

	public float goalTolerance = 0.1f;

	public float m_MaxTargetRange = 10f;

	private bool isRandomWalking = true;

	private Dictionary<Type, string> motionTypeAnimations = new Dictionary<Type, string>()
	{
		{Type.STANDBY, ""},
		{Type.MOVING, ""}
	};

	public bool RandomWalking
	{
		get => isRandomWalking;
		set => isRandomWalking = value;
	}

	void Awake()
	{
		m_Agent = GetComponent<NavMeshAgent>();
		m_Animation = GetComponent<Animation>();
		m_Agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
	}

	void Start()
	{
		var capsuleCollider = gameObject.GetComponent<CapsuleCollider>();

		if (capsuleCollider)
		{
			SetObstacleSize(capsuleCollider.radius, capsuleCollider.height);
		}

		Stop();
	}

	void LateUpdate()
	{
		if (m_Agent == null || m_Agent.pathStatus.Equals(NavMeshPathStatus.PathInvalid))
		{
			// Debug.LogWarning("agent is null or path status is invalid");
			return;
		}

		if (!m_Agent.pathPending)
		{
			if (m_Agent.remainingDistance < goalTolerance)
			{
				// Debug.LogWarning("remainingDistance:" + m_Agent.remainingDistance);
				Stop();

				if (isRandomWalking)
				{
					var nextTarget = m_MaxTargetRange * Random.insideUnitCircle;
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
		if (m_Agent && m_Agent.isOnNavMesh)
		{
			// Debug.Log("stop");
			m_Agent.isStopped = true;
			m_Agent.SetDestination(transform.position);
			SetAnimationMotion(Type.STANDBY);
		}
	}

	public void AssignTargetDestination(in Vector3 point)
	{
	 	Stop();

		if (m_Agent && m_Agent.isOnNavMesh)
		{
			SetAnimationMotion(Type.MOVING);
			m_Agent.isStopped = false;
			m_Agent.SetDestination(point);
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
		if (m_Animation)
		{
			var animationName = motionTypeAnimations[motionType];

			if (currentType != motionType)
			{
				var targetClip = m_Animation.GetClip(animationName);
				if (targetClip != null)
				{
					m_Animation.clip = targetClip;
					m_Animation.Stop();
					m_Animation.Play();
				}

				currentType = motionType;
			}
		}
	}

	public void SetObstacleSize(in float radius, in float height)
	{
		if (m_Agent)
		{
			m_Agent.radius = radius;
			m_Agent.height = height;
		}
	}

	public void SetSteering(in float speed, in float angularSpeed, in float acceleration)
	{
		if (m_Agent)
		{
			m_Agent.speed = speed;
			m_Agent.angularSpeed = angularSpeed;
			m_Agent.acceleration = acceleration;
		}
	}
}