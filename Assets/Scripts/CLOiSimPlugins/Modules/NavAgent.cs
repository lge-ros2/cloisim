/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Walk to a random position and repeat
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class NavAgent : MonoBehaviour
{
	private NavMeshAgent m_Agent;

	private bool isRandomWalking = false;
	public float m_MaxTargetRange = 10f;

	private Vector3 m_TargetDestination = Vector3.zero;


	public bool RandomWalking
	{
		get => isRandomWalking;
		set => isRandomWalking = value;
	}

	public

	void Start()
	{
		m_Agent = GetComponent<NavMeshAgent>();
	}

	void Update()
	{
		if (m_Agent.pathPending || m_Agent.remainingDistance > 0.1f)
		{
			return;
		}

		var targetDestination = Vector3.zero;
		if (isRandomWalking)
		{
			targetDestination = Random.Range(0, m_MaxTargetRange) * Random.insideUnitCircle;
		}
		else
		{
			targetDestination = m_TargetDestination;
		}

		m_Agent.destination = targetDestination;
	}
}