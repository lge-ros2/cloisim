/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(700)]
public class ActorControl : MonoBehaviour
{
	private RaycastHit m_HitInfo = new RaycastHit();

	private void ClickToMove(ref List<Transform> list)
	{
		foreach (var transform in list)
		{
			if (transform == null)
				continue;

			var actorAgent = transform.GetComponent<ActorAgent>();
			if (actorAgent != null)
			{
				if (actorAgent.RandomWalking)
				{
					Debug.LogWarning("Cannot move for random walking actor");
				}
				else
				{
					actorAgent.AssignTargetDestination(m_HitInfo.point);
				}
			}
		}
	}

	void LateUpdate()
	{
		if (Input.GetMouseButton(1))
		{
			Main.Gizmos.GetSelectedTargets(out var list);

			if (list.Count > 0)
			{
				var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
				if (Physics.Raycast(ray.origin, ray.direction, out m_HitInfo))
				{
					ClickToMove(ref list);
				}
			}
		}
	}
}