using System;
using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace RuntimeGizmos
{
	//To be safe, if you are changing any transforms hierarchy, such as parenting an object to something,
	//you should call ClearTargets before doing so just to be sure nothing unexpected happens..
	//For example, if you select an object that has children, move the children elsewhere, deselect the original object, then try to add those old children to the selection, I think it wont work.

	[RequireComponent(typeof(Camera))]
	public partial class TransformGizmo : MonoBehaviour
	{
		public TransformSpace space = TransformSpace.Global;
		public TransformType transformType = TransformType.Move;
		public TransformPivot pivot = TransformPivot.Pivot;
		public CenterType centerType = CenterType.All;


		[Header("Handle Properties")]
		public float planesOpacity = .8f;

		public float movementSnap = .25f;
		public float rotationSnap = 15f;

		public float handleLength = .25f;
		public float handleWidth = .003f;
		public float planeSize = .035f;
		public float triangleSize = .03f;
		public float boxSize = .03f;
		public int circleDetail = 40;
		public float allMoveHandleLengthMultiplier = 1f;
		public float allRotateHandleLengthMultiplier = 1.4f;
		public float minSelectedDistanceCheck = .01f;
		public float moveSpeedMultiplier = 1f;
		public float rotateSpeedMultiplier = 1f;
		public float allRotateSpeedMultiplier = 20f;

		public bool useFirstSelectedAsMain = true;

		//If circularRotationMethod is true, when rotating you will need to move your mouse around the object as if turning a wheel.
		//If circularRotationMethod is false, when rotating you can just click and drag in a line to rotate.
		public bool circularRotationMethod;

		//Mainly for if you want the pivot point to update correctly if selected objects are moving outside the transformgizmo.
		//Might be poor on performance if lots of objects are selected...
		public bool forceUpdatePivotPointOnChange = true;

		public bool manuallyHandleGizmo;

		//These are the same as the unity editor hotkeys
		[Header("Key configurations")]
		public KeyCode SetMoveType = KeyCode.T;
		public KeyCode SetRotateType = KeyCode.R;
		public KeyCode SetAllTransformType = KeyCode.Y;
		public KeyCode SetSpaceToggle = KeyCode.X;
		public KeyCode translationSnapping = KeyCode.LeftShift;

		public Action onCheckForSelectedAxis;
		public Action onDrawCustomGizmo;

		public Camera myCamera {get; private set;}

		public bool isTransforming {get; private set;}
		public Quaternion totalRotationAmount {get; private set;}
		public Axis translatingAxis {get {return nearAxis;}}
		public Axis translatingAxisPlane {get {return planeAxis;}}
		public bool hasTranslatingAxisPlane {get {return translatingAxisPlane != Axis.None && translatingAxisPlane != Axis.Any;}}
		public TransformType transformingType {get {return translatingType;}}

		public Vector3 pivotPoint {get; private set;}

		public Transform mainTargetRoot {get {return (targetRootsOrdered.Count > 0)? ((useFirstSelectedAsMain)? targetRootsOrdered[0]:targetRootsOrdered[targetRootsOrdered.Count - 1]):null;}}

		private Axis nearAxis = Axis.None;
		private Axis planeAxis = Axis.None;
		private TransformType translatingType;

		// We use a HashSet and a List for targetRoots so that we get fast lookup with the hashset while also keeping track of the order with the list.
		private List<Transform> targetRootsOrdered = new List<Transform>();
		private Dictionary<Transform, TargetInfo> targetRoots = new Dictionary<Transform, TargetInfo>();
		private HashSet<Transform> children = new HashSet<Transform>();
		private List<Transform> childrenBuffer = new List<Transform>();
		private Coroutine forceUpdatePivotCoroutine;

		void Awake()
		{
			myCamera = GetComponent<Camera>();

			if (myCamera == null)
			{
				myCamera = Camera.main;
			}

			SetMaterial();
		}

		void OnEnable()
		{
			forceUpdatePivotCoroutine = StartCoroutine(ForceUpdatePivotPointAtEndOfFrame());
			RenderPipelineManager.endCameraRendering += EndCameraRendering;
		}

		void OnDisable()
		{
			ClearTargets(); //Just so things gets cleaned up, such as removing any materials we placed on objects.

			StopCoroutine(forceUpdatePivotCoroutine);
			RenderPipelineManager.endCameraRendering -= EndCameraRendering;
		}

		void OnDestroy()
		{
			ClearAllHighlightedRenderers();
			Resources.UnloadUnusedAssets();
		}

		void Update()
		{
			SetSpaceAndType();

			if (manuallyHandleGizmo)
			{
				if (onCheckForSelectedAxis != null)
				{
					onCheckForSelectedAxis();
				}
			}
			else
			{
				SetNearAxis();
			}

			GetTarget();

			if (mainTargetRoot == null)
			{
				return;
			}

			TransformSelected();

			// Clear Tagets when ESC
			if (Input.GetKey(KeyCode.Escape))
			{
				transformType = TransformType.Move;
				ClearTargets();
			}
		}

		void LateUpdate()
		{
			if (mainTargetRoot == null)
			{
				 return;
			}

			// We run this in lateupdate since coroutines run after update and we want our gizmos to have the updated target transform position after TransformSelected()
			SetAxisInfo();

			if (manuallyHandleGizmo)
			{
				if (onDrawCustomGizmo != null)
				{
					onDrawCustomGizmo();
				}
			}
			else
			{
				SetLines();
			}
		}

		// We only support scaling in local space.
		public TransformSpace GetProperTransformSpace()
		{
			return space;
		}

		public bool TransformTypeContains(TransformType type)
		{
			return TransformTypeContains(transformType, type);
		}

		public bool TranslatingTypeContains(TransformType type, bool checkIsTransforming = true)
		{
			var transType = !checkIsTransforming || isTransforming ? translatingType : transformType;
			return TransformTypeContains(transType, type);
		}

		public bool TransformTypeContains(TransformType mainType, TransformType type)
		{
			return ExtTransformType.TransformTypeContains(mainType, type, GetProperTransformSpace());
		}

		public float GetHandleLength(TransformType type, Axis axis = Axis.None, bool multiplyDistanceMultiplier = true)
		{
			float length = handleLength;
			if (transformType == TransformType.All)
			{
				switch (type)
				{
					case TransformType.Move:
						length *= allMoveHandleLengthMultiplier;
						break;
					case TransformType.Rotate:
						length *= allRotateHandleLengthMultiplier;
						break;
				}
			}

			if (multiplyDistanceMultiplier)
			{
				length *= GetDistanceMultiplier();
			}

			return length;
		}

		void SetSpaceAndType()
		{
			if (Input.GetKeyDown(SetMoveType))
			{
				transformType = TransformType.Move;
			}
			else if (Input.GetKeyDown(SetRotateType))
			{
				transformType = TransformType.Rotate;
			}
			else if (Input.GetKeyDown(SetAllTransformType))
			{
				transformType = TransformType.All;
			}

			if (!isTransforming) translatingType = transformType;

			if (Input.GetKeyDown(SetSpaceToggle))
			{
				switch (space)
				{
					case TransformSpace.Global:
						space = TransformSpace.Local;
						break;

					case TransformSpace.Local:
						space = TransformSpace.Global;
						break;

					default:
						break;
				}
			}
		}

		void TransformSelected()
		{
			if (mainTargetRoot != null)
			{
				if (nearAxis != Axis.None && Input.GetMouseButtonDown(0))
				{
					StartCoroutine(TransformSelected(translatingType));
				}
			}
		}

		IEnumerator TransformSelected(TransformType transType)
		{
			isTransforming = true;
			totalRotationAmount = Quaternion.identity;

			var originalPivot = pivotPoint;
			var axis = GetNearAxisDirection(out var otherAxis1, out var otherAxis2);
			var planeNormal = hasTranslatingAxisPlane ? axis : (transform.position - originalPivot).normalized;
			var projectedAxis = Vector3.ProjectOnPlane(axis, planeNormal).normalized;
			var previousMousePosition = Vector3.zero;

			var currentSnapMovementAmount = Vector3.zero;
			var currentSnapRotationAmount = 0f;

			while (!Input.GetMouseButtonUp(0))
			{
				var mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
				var mousePosition = Geometry.LinePlaneIntersect(mouseRay.origin, mouseRay.direction, originalPivot, planeNormal);
				var isSnapping = Input.GetKey(translationSnapping);

				if (previousMousePosition != Vector3.zero && mousePosition != Vector3.zero)
				{
					switch (transType)
					{
						case TransformType.Move:
							{
								Vector3 movement = Vector3.zero;

								if (hasTranslatingAxisPlane)
								{
									movement = mousePosition - previousMousePosition;
								}
								else
								{
									float moveAmount = ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projectedAxis) * moveSpeedMultiplier;
									movement = axis * moveAmount;
								}

								if (isSnapping && movementSnap > 0)
								{
									currentSnapMovementAmount += movement;
									movement = Vector3.zero;

									if (hasTranslatingAxisPlane)
									{
										float amountInAxis1 = ExtVector3.MagnitudeInDirection(currentSnapMovementAmount, otherAxis1);
										float amountInAxis2 = ExtVector3.MagnitudeInDirection(currentSnapMovementAmount, otherAxis2);

										float snapAmount1 = CalculateSnapAmount(movementSnap, amountInAxis1, out var remainder1);
										float snapAmount2 = CalculateSnapAmount(movementSnap, amountInAxis2, out var remainder2);

										if (snapAmount1 != 0)
										{
											var snapMove = (otherAxis1 * snapAmount1);
											movement += snapMove;
											currentSnapMovementAmount -= snapMove;
										}

										if (snapAmount2 != 0)
										{
											var snapMove = (otherAxis2 * snapAmount2);
											movement += snapMove;
											currentSnapMovementAmount -= snapMove;
										}
									}
									else
									{
										float snapAmount = CalculateSnapAmount(movementSnap, currentSnapMovementAmount.magnitude, out var remainder);

										if (snapAmount != 0)
										{
											movement = currentSnapMovementAmount.normalized * snapAmount;
											currentSnapMovementAmount = currentSnapMovementAmount.normalized * remainder;
										}
									}
								}

								for (int i = 0; i < targetRootsOrdered.Count; i++)
								{
									Transform target = targetRootsOrdered[i];

									var articulationBody = target.GetComponent<ArticulationBody>();
									if (articulationBody != null && articulationBody.isRoot)
									{
										var newPose = new Pose(target.transform.position, target.transform.rotation);
										newPose.position += movement;

										articulationBody.Sleep();
										articulationBody.TeleportRoot(newPose.position, newPose.rotation);
									}
									else
									{
										var actor = target.GetComponent<SDF.Helper.Actor>();
										if (actor != null && actor.HasWayPoints)
										{
											actor.AddPose(movement);
										}
										else
										{
											target.Translate(movement, Space.World);
										}
									}
								}

								SetPivotPointOffset(movement);
							}
							break;

						case TransformType.Rotate:
							{
								float rotateAmount = 0;
								Vector3 rotationAxis = axis;

								if (nearAxis == Axis.Any)
								{
									Vector3 rotation = transform.TransformDirection(new Vector3(Input.GetAxis("Mouse Y"), -Input.GetAxis("Mouse X"), 0));
									Quaternion.Euler(rotation).ToAngleAxis(out rotateAmount, out rotationAxis);
									rotateAmount *= allRotateSpeedMultiplier;
								}
								else
								{
									if (circularRotationMethod)
									{
										float angle = Vector3.SignedAngle(previousMousePosition - originalPivot, mousePosition - originalPivot, axis);
										rotateAmount = angle * rotateSpeedMultiplier;
									}
									else
									{
										Vector3 projected = (nearAxis == Axis.Any || ExtVector3.IsParallel(axis, planeNormal)) ? planeNormal : Vector3.Cross(axis, planeNormal);
										rotateAmount = (ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projected) * (rotateSpeedMultiplier * 100f)) / GetDistanceMultiplier();
									}
								}

								if (isSnapping && rotationSnap > 0)
								{
									currentSnapRotationAmount += rotateAmount;
									rotateAmount = 0;

									float snapAmount = CalculateSnapAmount(rotationSnap, currentSnapRotationAmount, out var remainder);

									if (snapAmount != 0)
									{
										rotateAmount = snapAmount;
										currentSnapRotationAmount = remainder;
									}
								}

								for (int i = 0; i < targetRootsOrdered.Count; i++)
								{
									Transform target = targetRootsOrdered[i];

									if (pivot == TransformPivot.Pivot)
									{
										target.Rotate(rotationAxis, rotateAmount, Space.World);
									}
									else if (pivot == TransformPivot.Center)
									{
										target.RotateAround(originalPivot, rotationAxis, rotateAmount);
									}

									var articulationBody = target.GetComponent<ArticulationBody>();
									if (articulationBody != null && articulationBody.isRoot)
									{
										var newPose = new Pose(target.transform.position, target.transform.rotation);
										articulationBody.Sleep();
										articulationBody.TeleportRoot(newPose.position, newPose.rotation);
									}
								}

								totalRotationAmount *= Quaternion.Euler(rotationAxis * rotateAmount);
							}
							break;
					}
				}

				previousMousePosition = mousePosition;

				yield return null;
			}

			totalRotationAmount = Quaternion.identity;
			isTransforming = false;
			SetTranslatingAxis(transformType, Axis.None);

			SetPivotPoint();
		}

		float CalculateSnapAmount(float snapValue, float currentAmount, out float remainder)
		{
			remainder = 0;
			if (snapValue <= 0) return currentAmount;

			float currentAmountAbs = Mathf.Abs(currentAmount);
			if (currentAmountAbs > snapValue)
			{
				remainder = currentAmountAbs % snapValue;
				return snapValue * (Mathf.Sign(currentAmount) * Mathf.Floor(currentAmountAbs / snapValue));
			}

			return 0;
		}

		void GetTarget()
		{
			if (nearAxis == Axis.None && !Input.GetKey(KeyCode.LeftControl) && Input.GetMouseButtonDown(0))
			{
				if (Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out var hitInfo, Mathf.Infinity))
				{
					Transform target = null;
					var hitObject = hitInfo.transform;

					if (hitObject.tag.Equals("Props"))
					{
						target = hitObject.transform;
					}
					else
					{
						var hitParentActor = hitObject?.GetComponent<SDF.Helper.Actor>();

						if (hitParentActor != null && hitParentActor.CompareTag("Actor"))
						{
							target = hitParentActor.transform;
						}
						else
						{
							// avoid plane object
							if (!hitObject.gameObject.layer.Equals(SDF.Implement.Collision.PlaneLayerIndex))
							{
								var hitParentLinkHelper = hitObject?.GetComponentInParent<SDF.Helper.Link>();
								var hitTopModelHelper = hitParentLinkHelper?.TopModel;

								if (hitTopModelHelper != null && !(hitTopModelHelper.isStatic || hitParentLinkHelper.Model.isStatic))
								{
									// Debug.Log(hitParentObject.name + " Selected!!!!");
									target = (hitTopModelHelper.hasRootArticulationBody) ? hitTopModelHelper.transform : hitParentLinkHelper.Model.transform;
								}
							}
						}
					}

					if (target == null)
					{
						ClearTargets();
					}
					else
					{
						ClearAndAddTarget(target);
					}
				}
				else
				{
					ClearTargets();
				}
			}
		}

		public void AddTarget(Transform target)
		{
			if (target != null)
			{
				if (targetRoots.ContainsKey(target))
				{
					return;
				}

				if (children.Contains(target))
				{
					return;
				}

				AddTargetRoot(target);
				AddTargetHighlightedRenderers(target);

				SetPivotPoint();
			}
		}

		public void RemoveTarget(Transform target)
		{
			if (target != null)
			{
				if (!targetRoots.ContainsKey(target))
				{
					return;
				}

				RemoveTargetHighlightedRenderers(target);
				RemoveTargetRoot(target);

				SetPivotPoint();
			}
		}

		public void ClearTargets()
		{
			ClearAllHighlightedRenderers();
			targetRoots.Clear();
			targetRootsOrdered.Clear();
			children.Clear();
		}

		void ClearAndAddTarget(Transform target)
		{
			ClearTargets();
			AddTarget(target);
		}

		void AddTargetRoot(Transform targetRoot)
		{
			targetRoots.Add(targetRoot, new TargetInfo());
			targetRootsOrdered.Add(targetRoot);

			AddAllChildren(targetRoot);
		}

		void RemoveTargetRoot(Transform targetRoot)
		{
			if (targetRoots.Remove(targetRoot))
			{
				targetRootsOrdered.Remove(targetRoot);

				RemoveAllChildren(targetRoot);
			}
		}

		void AddAllChildren(Transform target)
		{
			childrenBuffer.Clear();
			target.GetComponentsInChildren<Transform>(true, childrenBuffer);
			childrenBuffer.Remove(target);

			for (int i = 0; i < childrenBuffer.Count; i++)
			{
				Transform child = childrenBuffer[i];
				children.Add(child);
				RemoveTargetRoot(child); //We do this in case we selected child first and then the parent.
			}

			childrenBuffer.Clear();
		}

		void RemoveAllChildren(Transform target)
		{
			childrenBuffer.Clear();
			target.GetComponentsInChildren<Transform>(true, childrenBuffer);
			childrenBuffer.Remove(target);

			for (int i = 0; i < childrenBuffer.Count; i++)
			{
				children.Remove(childrenBuffer[i]);
			}

			childrenBuffer.Clear();
		}
	}
}
