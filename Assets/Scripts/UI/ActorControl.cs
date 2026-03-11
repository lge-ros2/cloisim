/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
		if (Mouse.current.rightButton.isPressed)
		{
			if (Main.Gizmos != null)
			{
				Main.Gizmos.GetSelectedTargets(out var list);

				if (list.Count > 0)
				{
					var ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
					if (Physics.Raycast(ray.origin, ray.direction, out m_HitInfo))
					{
						ClickToMove(ref list);
					}
				}
			}
		}
	}
}