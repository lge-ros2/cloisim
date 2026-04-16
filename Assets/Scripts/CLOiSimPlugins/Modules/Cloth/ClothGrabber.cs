/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using Unity.Mathematics;

namespace CLOiSim.Cloth
{
	/// <summary>
	/// Attach to a robot fingertip link. Set GrabCollider to the fingertip's MeshCollider.
	/// When activated, grabs the nearest cloth vertex that overlaps or is within the collider bounds.
	/// Driven by ClothGrabberPlugin from SDF config.
	/// </summary>
	public class ClothGrabber : MonoBehaviour
	{
		[Header("Grab Collider")]
		[SerializeField] private MeshCollider _grabCollider;
		[SerializeField] private float _grabRadius = 0.01f; // fallback search radius around collider bounds
		[SerializeField] private bool _isActive = false;

		private BurstCloth _cloth;
		private int _grabbedIndex = -1;

		/// <summary>Whether this grabber is currently holding a cloth vertex.</summary>
		public bool IsGrabbing => _grabbedIndex >= 0;

		/// <summary>Index of the currently grabbed vertex, or -1 if not grabbing.</summary>
		public int GrabbedVertexIndex => _grabbedVertexIndex;
		private int _grabbedVertexIndex = -1;

		/// <summary>The BurstCloth instance being grabbed, or null.</summary>
		public BurstCloth TargetCloth => _cloth;

		/// <summary>The MeshCollider used to detect cloth contact.</summary>
		public MeshCollider GrabCollider
		{
			get => _grabCollider;
			set => _grabCollider = value;
		}

		/// <summary>Fallback search radius around collider center when no vertex is strictly inside.
		/// Set via SDF grab_radius.</summary>
		public float GrabRadius
		{
			get => _grabRadius;
			set => _grabRadius = value;
		}

		public bool IsActive
		{
			get => _isActive;
			set
			{
				_isActive = value;
				if (!value) Release();
			}
		}

		/// <summary>Activates the grabber. Call when gripper closes.</summary>
		public void Activate() => IsActive = true;

		/// <summary>Deactivates the grabber and releases any grabbed vertex. Call when gripper opens.</summary>
		public void Deactivate() => IsActive = false;

		/// <summary>Releases the currently grabbed vertex without deactivating.</summary>
		public void Release()
		{
			if (_grabbedVertexIndex >= 0 && _cloth != null)
				_cloth.ReleaseVertex(_grabbedVertexIndex);
			_grabbedVertexIndex = -1;
			_cloth = null;
		}

		private void Update()
		{
			if (!_isActive) return;
			if (_grabCollider == null) return;

			var grabCenter = (float3)_grabCollider.bounds.center;

			if (_grabbedVertexIndex >= 0)
			{
				// Already grabbing — move grabbed vertex to collider center
				if (_cloth != null)
					_cloth.UpdateGrabPosition(_grabbedVertexIndex, grabCenter);
				return;
			}

			TryGrabNearestVertex(grabCenter);
		}

		private void TryGrabNearestVertex(float3 grabCenter)
		{
			// Compute search radius from collider bounds diagonal + fallback margin
			var bounds = _grabCollider.bounds;
			var boundsRadius = math.length((float3)bounds.extents) + _grabRadius;

			var bestIndex = -1;
			var bestDistSq = boundsRadius * boundsRadius;
			BurstCloth bestCloth = null;

			foreach (var cloth in BurstCloth.Instances)
			{
				var positions = cloth.GetPositions();
				if (!positions.IsCreated) continue;

				// First pass: look for vertices strictly inside collider bounds
				var strictIndex = -1;
				var strictDistSq = float.MaxValue;
				for (var i = 0; i < positions.Length; i++)
				{
					if (cloth.IsVertexGrabbed(i)) continue;
					var pos = (Vector3)positions[i];
					if (!bounds.Contains(pos)) continue;
					var dSq = math.lengthsq(positions[i] - grabCenter);
					if (dSq < strictDistSq)
					{
						strictDistSq = dSq;
						strictIndex = i;
					}
				}

				if (strictIndex >= 0 && strictDistSq < bestDistSq)
				{
					bestDistSq = strictDistSq;
					bestIndex = strictIndex;
					bestCloth = cloth;
				}
				else
				{
					// Fallback: nearest within boundsRadius
					var fallbackIndex = cloth.FindNearestVertex(grabCenter, boundsRadius);
					if (fallbackIndex < 0) continue;
					var dSq = math.lengthsq(positions[fallbackIndex] - grabCenter);
					if (dSq < bestDistSq)
					{
						bestDistSq = dSq;
						bestIndex = fallbackIndex;
						bestCloth = cloth;
					}
				}
			}

			if (bestCloth != null && bestCloth.GrabVertex(bestIndex, grabCenter))
			{
				_cloth = bestCloth;
				_grabbedVertexIndex = bestIndex;
			}
		}

		private void OnDisable() => Release();
		private void OnDestroy() => Release();
		void Reset() => Release();

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (_grabCollider != null)
			{
				Gizmos.color = _isActive ? Color.yellow : Color.gray;
				Gizmos.DrawWireCube(_grabCollider.bounds.center, _grabCollider.bounds.size);
				Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
				Gizmos.DrawCube(_grabCollider.bounds.center, _grabCollider.bounds.size);
			}
			else
			{
				Gizmos.color = _isActive ? Color.yellow : Color.gray;
				Gizmos.DrawWireSphere(transform.position, _grabRadius);
			}
		}
#endif
	}
}
