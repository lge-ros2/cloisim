/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walk to a random position and repeat or specify the destination
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class ActorAgent : MonoBehaviour
{
	private NavMeshAgent m_Agent;

	public float goalTolerance = 0.1f;

	public float m_MaxTargetRange = 10f;

	private bool isRandomWalking = false;

	private Vector3 m_TargetDestination = Vector3.zero;

	public bool RandomWalking
	{
		get => isRandomWalking;
		set => isRandomWalking = value;
	}

	void Start()
	{
		m_Agent = GetComponent<NavMeshAgent>();
	}

	void LateUpdate()
	{
		if (m_Agent.pathPending || m_Agent.remainingDistance > goalTolerance)
		{
			return;
		}
		else
		{
			if (isRandomWalking)
			{
				m_Agent.destination = Random.Range(0, m_MaxTargetRange) * Random.insideUnitCircle;
			}
			else
			{
				m_Agent.destination = m_TargetDestination;
			}
		}
	}
}