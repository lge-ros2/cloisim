/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;
using Unity.Mathematics;
using Unity.Profiling;

namespace CLOiSim.Cloth
{
	/// <summary>
	/// Attach to a robot fingertip link. Set GrabCollider to the fingertip's MeshCollider.
	/// When activated, grabs the nearest cloth vertex that overlaps or is within the collider bounds.
	/// Driven by ClothGrabberPlugin from SDF config.
	/// </summary>
	public class ClothGrabber : MonoBehaviour
	{
		private static readonly ProfilerMarker HasClothWithinDistanceMarker = new("CLOiSim.ClothGrabber.HasClothWithinDistance");
		private static readonly ProfilerMarker GrabberUpdateMarker = new("CLOiSim.ClothGrabber.Update");
		private static readonly ProfilerMarker TryGrabNearestVertexMarker = new("CLOiSim.ClothGrabber.TryGrabNearestVertex");

		[Header("Grab Collider")]
		[SerializeField] private MeshCollider _grabCollider;
		[SerializeField] private float _grabRadius = 0.01f; // fallback search radius around collider bounds
		[SerializeField] private bool _isActive = false;

		private BurstCloth _cloth;

		/// <summary>Whether this grabber is currently holding a cloth vertex.</summary>
		public bool IsGrabbing => _grabbedVertexIndex >= 0;

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

		/// <summary>
		/// Returns true if any cloth has a free vertex within <paramref name="distance"/> of the grab collider surface.
		/// Does NOT grab — use for proximity queries only.
		/// </summary>
		public bool HasClothWithinDistance(float distance)
		{
			using (HasClothWithinDistanceMarker.Auto())
			{
				if (_grabCollider == null) return false;
				var colliderBounds = GetColliderBounds();
				var maxSurfaceDistanceSq = distance * distance;
				foreach (var cloth in BurstCloth.Instances)
				{
					var positions = cloth.GetPositions();
					if (!positions.IsCreated) continue;

					for (var i = 0; i < positions.Length; i++)
					{
						if (cloth.IsVertexGrabbed(i) || cloth.IsVertexPinned(i)) continue;

						if (GetSurfaceDistanceSq(positions[i], colliderBounds) <= maxSurfaceDistanceSq)
							return true;
					}
				}
				return false;
			}
		}

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
			using (GrabberUpdateMarker.Auto())
			{
				if (!_isActive) return;
				if (_grabCollider == null) return;

				var colliderBounds = GetColliderBounds();
				var grabReference = (float3)colliderBounds.center;

				if (_grabbedVertexIndex >= 0)
				{
					if (_cloth != null)
					{
						var positions = _cloth.GetPositions();
						if (positions.IsCreated && _grabbedVertexIndex < positions.Length)
						{
							var grabTarget = GetGrabTargetPosition(positions[_grabbedVertexIndex], colliderBounds);
							_cloth.UpdateGrabPosition(_grabbedVertexIndex, grabTarget);
						}
					}
					return;
				}

				TryGrabNearestVertex(grabReference, colliderBounds);
			}
		}

		private void TryGrabNearestVertex(float3 grabCenter, Bounds colliderBounds)
		{
			using (TryGrabNearestVertexMarker.Auto())
			{
				// Compute search radius from collider bounds diagonal + fallback margin
				var bounds = colliderBounds;
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
						var grabTarget = GetGrabTargetPosition(positions[i], bounds);
						var dSq = math.lengthsq(positions[i] - grabTarget);
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
						var grabTarget = GetGrabTargetPosition(positions[fallbackIndex], bounds);
						var dSq = math.lengthsq(positions[fallbackIndex] - grabTarget);
						if (dSq < bestDistSq)
						{
							bestDistSq = dSq;
							bestIndex = fallbackIndex;
							bestCloth = cloth;
						}
					}
				}

				if (bestCloth == null || bestIndex < 0)
					return;

				var positionsForBestCloth = bestCloth.GetPositions();
				if (!positionsForBestCloth.IsCreated || bestIndex >= positionsForBestCloth.Length)
					return;

				var grabTargetForBestVertex = GetGrabTargetPosition(positionsForBestCloth[bestIndex], bounds);
				if (bestCloth.GrabVertex(bestIndex, grabTargetForBestVertex))
				{
					_cloth = bestCloth;
					_grabbedVertexIndex = bestIndex;
				}
			}
		}

		private float3 GetGrabTargetPosition(float3 referencePosition)
		{
			return GetGrabTargetPosition(referencePosition, GetColliderBounds());
		}

		private float3 GetGrabTargetPosition(float3 referencePosition, Bounds colliderBounds)
		{
			if (_grabCollider == null)
				return referencePosition;

			var closestPoint = (float3)_grabCollider.ClosestPoint((Vector3)referencePosition);
			if (math.lengthsq(closestPoint - referencePosition) > 1e-8f)
				return closestPoint;

			var boundsClosestPoint = (float3)colliderBounds.ClosestPoint((Vector3)referencePosition);
			if (math.lengthsq(boundsClosestPoint - referencePosition) > 1e-8f)
				return boundsClosestPoint;

			var colliderCenter = (float3)colliderBounds.center;
			var outward = referencePosition - colliderCenter;
			if (math.lengthsq(outward) < 1e-8f)
				outward = (float3)_grabCollider.transform.up;

			var probeDistance = math.length((float3)colliderBounds.extents) + _grabRadius + 0.001f;
			var probePoint = colliderCenter + math.normalize(outward) * probeDistance;
			return (float3)colliderBounds.ClosestPoint((Vector3)probePoint);
		}

		private float GetSurfaceDistanceSq(float3 referencePosition)
		{
			return GetSurfaceDistanceSq(referencePosition, GetColliderBounds());
		}

		private float GetSurfaceDistanceSq(float3 referencePosition, Bounds colliderBounds)
		{
			var grabTarget = GetGrabTargetPosition(referencePosition, colliderBounds);
			var bestDistanceSq = math.lengthsq(referencePosition - grabTarget);

			// Bounds.ClosestPoint is a more stable fallback for proximity-only checks,
			// especially in EditMode tests using procedural colliders.
			var boundsTarget = (float3)colliderBounds.ClosestPoint((Vector3)referencePosition);
			var boundsDistanceSq = math.lengthsq(referencePosition - boundsTarget);
			return math.min(bestDistanceSq, boundsDistanceSq);
		}

		private Bounds GetColliderBounds()
		{
			if (_grabCollider == null)
				return new Bounds(transform.position, Vector3.zero);

			var colliderBounds = _grabCollider.bounds;
			if (colliderBounds.size.sqrMagnitude > 1e-10f)
				return colliderBounds;

			if (_grabCollider.sharedMesh == null)
				return new Bounds(_grabCollider.transform.position, Vector3.zero);

			var localBounds = _grabCollider.sharedMesh.bounds;
			var localCenter = localBounds.center;
			var localExtents = localBounds.extents;
			var colliderTransform = _grabCollider.transform;

			var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
			var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

			for (var x = -1; x <= 1; x += 2)
			{
				for (var y = -1; y <= 1; y += 2)
				{
					for (var z = -1; z <= 1; z += 2)
					{
						var localCorner = localCenter + Vector3.Scale(localExtents, new Vector3(x, y, z));
						var worldCorner = colliderTransform.TransformPoint(localCorner);
						min = Vector3.Min(min, worldCorner);
						max = Vector3.Max(max, worldCorner);
					}
				}
			}

			var bounds = new Bounds();
			bounds.SetMinMax(min, max);
			return bounds;
		}

		private void OnDisable() => Release();
		private void OnDestroy() => Release();
		void Reset() => Release();

#if UNITY_EDITOR
		private void OnDrawGizmosSelected()
		{
			if (_grabCollider != null)
			{
				var bounds = GetColliderBounds();
				Gizmos.color = _isActive ? Color.yellow : Color.gray;
				Gizmos.DrawWireCube(bounds.center, bounds.size);
				Gizmos.color = new Color(1f, 1f, 0f, 0.15f);
				Gizmos.DrawCube(bounds.center, bounds.size);
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
