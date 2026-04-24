/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UE = UnityEngine;

namespace SDFormat
{
	namespace Helper
	{
		/// <summary>
		/// Attaches to a single gripper finger link and forwards contact events
		/// (as link-index / Rigidbody pairs) to the parent <see cref="Gripper"/>.
		/// Add one instance per gripper_link defined in SDF.
		/// </summary>
		public class GripperLinkContact : UE.MonoBehaviour
		{
			private Gripper _gripper;
			private int _linkIndex;

			public void Initialize(Gripper gripper, int linkIndex)
			{
				_gripper = gripper;
				_linkIndex = linkIndex;
			}

			private void OnCollisionEnter(UE.Collision collision)
			{
				var rb = collision.rigidbody;
				if (rb != null)
					_gripper?.RegisterContact(_linkIndex, rb);
			}

			private void OnCollisionStay(UE.Collision collision)
			{
				var rb = collision.rigidbody;
				if (rb != null)
					_gripper?.RegisterContact(_linkIndex, rb);
			}
		}

		/// <summary>
		/// Implements the Gazebo-compatible gripper grasp-check logic described in
		/// the SDF <c>&lt;gripper&gt;</c> element. Monitors contacts on the finger
		/// links and creates a <see cref="UE.FixedJoint"/> between the grabbed
		/// object's Rigidbody and the palm link's ArticulationBody when the
		/// debounce thresholds are met.
		///
		/// Algorithm per FixedUpdate step:
		/// <code>
		///   if distinct (link, rigidbody) contacts >= MinContactCount:
		///       attachCounter++; detachCounter = 0
		///   else:
		///       detachCounter++; attachCounter = 0
		///
		///   if !attached and attachCounter >= AttachSteps  → Attach (create FixedJoint)
		///   if  attached and detachCounter >= DetachSteps  → Detach (destroy FixedJoint)
		/// </code>
		/// </summary>
		public class Gripper : UE.MonoBehaviour
		{
			[UE.Header("GraspCheck Parameters")]
			[UE.SerializeField] private int _minContactCount = 2;
			[UE.SerializeField] private int _attachSteps = 20;
			[UE.SerializeField] private int _detachSteps = 40;

			[UE.Header("Links")]
			[UE.SerializeField] private UE.Transform _palmLink;
			[UE.SerializeField] private List<UE.Transform> _gripperLinks = new();

			private UE.ArticulationBody _palmArticulationBody;

			// Key: (gripper-link index, contacted Rigidbody) — unique contact pairs
			private readonly HashSet<(int, UE.Rigidbody)> _contactsThisStep = new();

			private int _attachCounter;
			private int _detachCounter;

			private UE.Rigidbody _attachedRigidbody;
			private UE.FixedJoint _fixedJoint;

			// ── Public configuration ──────────────────────────────────────────────

			public int MinContactCount
			{
				get => _minContactCount;
				set => _minContactCount = value;
			}

			public int AttachSteps
			{
				get => _attachSteps;
				set => _attachSteps = value;
			}

			public int DetachSteps
			{
				get => _detachSteps;
				set => _detachSteps = value;
			}

			public UE.Transform PalmLink
			{
				get => _palmLink;
				set => _palmLink = value;
			}

			public void AddGripperLink(UE.Transform link) => _gripperLinks.Add(link);

			// ── Called by GripperLinkContact ──────────────────────────────────────

			/// <summary>Records a live contact from the given gripper link index.</summary>
			internal void RegisterContact(int linkIndex, UE.Rigidbody other)
			{
				if (other == null || IsPartOfThisModel(other.transform))
					return;

				_contactsThisStep.Add((linkIndex, other));
			}

			// ── Unity callbacks ───────────────────────────────────────────────────

			private void Start()
			{
				if (_palmLink != null)
				{
					_palmArticulationBody = _palmLink.GetComponent<UE.ArticulationBody>();
					if (_palmArticulationBody == null)
					{
						UE.Debug.LogWarning($"[Gripper:{name}] PalmLink '{_palmLink.name}' has no ArticulationBody.");
					}
				}
			}

			private void FixedUpdate()
			{
				int contactCount = _contactsThisStep.Count;

				if (contactCount >= _minContactCount)
				{
					_attachCounter++;
					_detachCounter = 0;
				}
				else
				{
					_detachCounter++;
					_attachCounter = 0;
				}

				if (_attachedRigidbody == null && _attachCounter >= _attachSteps)
					TryAttach();
				else if (_attachedRigidbody != null && _detachCounter >= _detachSteps)
					Detach();

				// Clear per-step contacts — OnCollisionStay will repopulate next step
				_contactsThisStep.Clear();
			}

			private void OnDestroy()
			{
				if (_attachedRigidbody != null)
					Detach();
			}

			// ── Private helpers ───────────────────────────────────────────────────

			private void TryAttach()
			{
				if (_palmArticulationBody == null)
					return;

				// Pick the first valid contacted Rigidbody
				foreach (var (_, rb) in _contactsThisStep)
				{
					if (rb == null)
						continue;

					_attachedRigidbody = rb;

					// Create a FixedJoint on the grabbed object, connected to palm's ArticulationBody
					_fixedJoint = rb.gameObject.AddComponent<UE.FixedJoint>();
					_fixedJoint.connectedArticulationBody = _palmArticulationBody;
					_fixedJoint.autoConfigureConnectedAnchor = true;

					UE.Debug.Log($"[Gripper:{name}] Attached '{rb.gameObject.name}' to palm '{_palmLink.name}' via FixedJoint");
					_attachCounter = 0;
					return;
				}
			}

			private void Detach()
			{
				if (_attachedRigidbody == null)
					return;

				UE.Debug.Log($"[Gripper:{name}] Detached '{_attachedRigidbody.gameObject.name}' from palm '{_palmLink.name}'");

				if (_fixedJoint != null)
				{
					Destroy(_fixedJoint);
					_fixedJoint = null;
				}

				_attachedRigidbody = null;
				_detachCounter = 0;
			}

			private bool IsPartOfThisModel(UE.Transform t)
			{
				var root = transform;
				while (t != null)
				{
					if (t == root)
						return true;
					t = t.parent;
				}
				return false;
			}
		}
	}
}
