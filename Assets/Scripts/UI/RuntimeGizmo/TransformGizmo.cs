using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using CommandUndoRedo;

namespace RuntimeGizmos
{
	//To be safe, if you are changing any transforms hierarchy, such as parenting an object to something,
	//you should call ClearTargets before doing so just to be sure nothing unexpected happens... as well as call UndoRedoManager.Clear()
	//For example, if you select an object that has children, move the children elsewhere, deselect the original object, then try to add those old children to the selection, I think it wont work.

	[RequireComponent(typeof(Camera))]
	public partial class TransformGizmo : MonoBehaviour
	{
		public TransformSpace space = TransformSpace.Global;
		public TransformType transformType = TransformType.Move;
		public TransformPivot pivot = TransformPivot.Pivot;
		public CenterType centerType = CenterType.All;
		public ScaleType scaleType = ScaleType.FromPoint;

		[Header("Handle Properties")]
		public float planesOpacity = .5f;
		//public Color rectPivotColor = new Color(0, 0, 1, 0.8f);
		//public Color rectCornerColor = new Color(0, 0, 1, 0.8f);
		//public Color rectAnchorColor = new Color(.7f, .7f, .7f, 0.8f);
		//public Color rectLineColor = new Color(.7f, .7f, .7f, 0.8f);

		public float movementSnap = .25f;
		public float rotationSnap = 15f;
		public float scaleSnap = 1f;

		public float handleLength = .25f;
		public float handleWidth = .003f;
		public float planeSize = .035f;
		public float triangleSize = .03f;
		public float boxSize = .03f;
		public int circleDetail = 40;
		public float allMoveHandleLengthMultiplier = 1f;
		public float allRotateHandleLengthMultiplier = 1.4f;
		public float allScaleHandleLengthMultiplier = 1.6f;
		public float minSelectedDistanceCheck = .01f;
		public float moveSpeedMultiplier = 1f;
		public float scaleSpeedMultiplier = 1f;
		public float rotateSpeedMultiplier = 1f;
		public float allRotateSpeedMultiplier = 20f;

		public bool useFirstSelectedAsMain = true;

		//If circularRotationMethod is true, when rotating you will need to move your mouse around the object as if turning a wheel.
		//If circularRotationMethod is false, when rotating you can just click and drag in a line to rotate.
		public bool circularRotationMethod;

		//Mainly for if you want the pivot point to update correctly if selected objects are moving outside the transformgizmo.
		//Might be poor on performance if lots of objects are selected...
		public bool forceUpdatePivotPointOnChange = true;

		public int maxUndoStored = 100;

		public bool manuallyHandleGizmo;

		public LayerMask selectionMask = Physics.DefaultRaycastLayers;

		//These are the same as the unity editor hotkeys
		[Header("Key configurations")]
		public KeyCode SetMoveType = KeyCode.W;
		public KeyCode SetRotateType = KeyCode.E;
		public KeyCode SetScaleType = KeyCode.R;
		//public KeyCode SetRectToolType = KeyCode.T;
		public KeyCode SetAllTransformType = KeyCode.Y;
		public KeyCode SetSpaceToggle = KeyCode.X;
		public KeyCode SetPivotModeToggle = KeyCode.Z;
		public KeyCode SetCenterTypeToggle = KeyCode.C;
		public KeyCode SetScaleTypeToggle = KeyCode.S;
		public KeyCode translationSnapping = KeyCode.LeftControl;
		public KeyCode AddSelection = KeyCode.LeftShift;
		public KeyCode RemoveSelection = KeyCode.LeftControl;
		public KeyCode ActionKey = KeyCode.LeftShift; //Its set to shift instead of control so that while in the editor we dont accidentally undo editor changes =/
		public KeyCode UndoAction = KeyCode.Z;
		public KeyCode RedoAction = KeyCode.Y;

		public Action onCheckForSelectedAxis;
		public Action onDrawCustomGizmo;

		public Camera myCamera {get; private set;}

		public bool isTransforming {get; private set;}
		public float totalScaleAmount {get; private set;}
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
		}

		void OnDisable()
		{
			ClearTargets(); //Just so things gets cleaned up, such as removing any materials we placed on objects.

			StopCoroutine(forceUpdatePivotCoroutine);
		}

		void OnDestroy()
		{
			ClearAllHighlightedRenderers();
		}

		void Update()
		{
			HandleUndoRedo();

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

			// Clear Tagets when ESC or Ctrl+R was pressed
			if (Input.GetKey(KeyCode.Escape) || (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.R)))
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


		void HandleUndoRedo()
		{
			if (maxUndoStored != UndoRedoManager.maxUndoStored) { UndoRedoManager.maxUndoStored = maxUndoStored; }

			if (Input.GetKey(ActionKey))
			{
				if (Input.GetKeyDown(UndoAction))
				{
					UndoRedoManager.Undo();
				}
				else if (Input.GetKeyDown(RedoAction))
				{
					UndoRedoManager.Redo();
				}
			}
		}

		// We only support scaling in local space.
		public TransformSpace GetProperTransformSpace()
		{
			return transformType == TransformType.Scale ? TransformSpace.Local : space;
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
				if (type == TransformType.Move) length *= allMoveHandleLengthMultiplier;
				if (type == TransformType.Rotate) length *= allRotateHandleLengthMultiplier;
				if (type == TransformType.Scale) length *= allScaleHandleLengthMultiplier;
			}

			if (multiplyDistanceMultiplier)
			{
				length *= GetDistanceMultiplier();
			}

			if (type == TransformType.Scale && isTransforming && (translatingAxis == axis || translatingAxis == Axis.Any))
			{
				length += totalScaleAmount;
			}

			return length;
		}

		void SetSpaceAndType()
		{
			if (Input.GetKey(ActionKey))
			{
				return;
			}

			if (Input.GetKeyDown(SetMoveType))
				transformType = TransformType.Move;
			else if (Input.GetKeyDown(SetRotateType))
				transformType = TransformType.Rotate;
			else if (Input.GetKeyDown(SetScaleType))
				transformType = TransformType.Scale;
			//else if (Input.GetKeyDown(SetRectToolType)) type = TransformType.RectTool;
			else if (Input.GetKeyDown(SetAllTransformType))
				transformType = TransformType.All;

			if (!isTransforming) translatingType = transformType;

			if (Input.GetKeyDown(SetPivotModeToggle))
			{
				if (pivot == TransformPivot.Pivot) pivot = TransformPivot.Center;
				else if (pivot == TransformPivot.Center) pivot = TransformPivot.Pivot;

				SetPivotPoint();
			}

			if (Input.GetKeyDown(SetCenterTypeToggle))
			{
				if (centerType == CenterType.All) centerType = CenterType.Solo;
				else if (centerType == CenterType.Solo) centerType = CenterType.All;

				SetPivotPoint();
			}

			if (Input.GetKeyDown(SetSpaceToggle))
			{
				if (space == TransformSpace.Global)
					space = TransformSpace.Local;
				else if (space == TransformSpace.Local)
					space = TransformSpace.Global;
			}

			if (Input.GetKeyDown(SetScaleTypeToggle))
			{
				if (scaleType == ScaleType.FromPoint) scaleType = ScaleType.FromPointOffset;
				else if (scaleType == ScaleType.FromPointOffset) scaleType = ScaleType.FromPoint;
			}

			if (transformType == TransformType.Scale)
			{
				if (pivot == TransformPivot.Pivot) scaleType = ScaleType.FromPoint; //FromPointOffset can be inaccurate and should only really be used in Center mode if desired.
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
			totalScaleAmount = 0;
			totalRotationAmount = Quaternion.identity;

			Vector3 originalPivot = pivotPoint;
			Vector3 otherAxis1, otherAxis2;
			Vector3 axis = GetNearAxisDirection(out otherAxis1, out otherAxis2);
			Vector3 planeNormal = hasTranslatingAxisPlane ? axis : (transform.position - originalPivot).normalized;
			Vector3 projectedAxis = Vector3.ProjectOnPlane(axis, planeNormal).normalized;
			Vector3 previousMousePosition = Vector3.zero;

			Vector3 currentSnapMovementAmount = Vector3.zero;
			float currentSnapRotationAmount = 0;
			float currentSnapScaleAmount = 0;

			List<ICommand> transformCommands = new List<ICommand>();
			for (int i = 0; i < targetRootsOrdered.Count; i++)
			{
				transformCommands.Add(new TransformCommand(this, targetRootsOrdered[i]));
			}

			while (!Input.GetMouseButtonUp(0))
			{
				var mouseRay = myCamera.ScreenPointToRay(Input.mousePosition);
				var mousePosition = Geometry.LinePlaneIntersect(mouseRay.origin, mouseRay.direction, originalPivot, planeNormal);
				var isSnapping = Input.GetKey(translationSnapping);

				if (previousMousePosition != Vector3.zero && mousePosition != Vector3.zero)
				{
					if (transType == TransformType.Move)
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

							target.Translate(movement, Space.World);
						}

						SetPivotPointOffset(movement);
					}
					else if (transType == TransformType.Scale)
					{
						Vector3 projected = (nearAxis == Axis.Any)? transform.right : projectedAxis;
						float scaleAmount = ExtVector3.MagnitudeInDirection(mousePosition - previousMousePosition, projected) * scaleSpeedMultiplier;

						if (isSnapping && scaleSnap > 0)
						{
							currentSnapScaleAmount += scaleAmount;
							scaleAmount = 0;

							float snapAmount = CalculateSnapAmount(scaleSnap, currentSnapScaleAmount, out var remainder);

							if (snapAmount != 0)
							{
								scaleAmount = snapAmount;
								currentSnapScaleAmount = remainder;
							}
						}

						//WARNING - There is a bug in unity 5.4 and 5.5 that causes InverseTransformDirection to be affected by scale which will break negative scaling. Not tested, but updating to 5.4.2 should fix it - https://issuetracker.unity3d.com/issues/transformdirection-and-inversetransformdirection-operations-are-affected-by-scale
						Vector3 localAxis = (GetProperTransformSpace() == TransformSpace.Local && nearAxis != Axis.Any)? mainTargetRoot.InverseTransformDirection(axis) : axis;

						Vector3 targetScaleAmount = Vector3.one;
						if (nearAxis == Axis.Any) targetScaleAmount = (ExtVector3.Abs(mainTargetRoot.localScale.normalized) * scaleAmount);
						else targetScaleAmount = localAxis * scaleAmount;

						for (int i = 0; i < targetRootsOrdered.Count; i++)
						{
							Transform target = targetRootsOrdered[i];

							Vector3 targetScale = target.localScale + targetScaleAmount;

							if (pivot == TransformPivot.Pivot)
							{
								target.localScale = targetScale;
							}
							else if (pivot == TransformPivot.Center)
							{
								if (scaleType == ScaleType.FromPoint)
								{
									target.SetScaleFrom(originalPivot, targetScale);
								}
								else if (scaleType == ScaleType.FromPointOffset)
								{
									target.SetScaleFromOffset(originalPivot, targetScale);
								}
							}
						}

						totalScaleAmount += scaleAmount;
					}
					else if (transType == TransformType.Rotate)
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
								Vector3 projected = (nearAxis == Axis.Any || ExtVector3.IsParallel(axis, planeNormal))? planeNormal : Vector3.Cross(axis, planeNormal);
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
						}

						totalRotationAmount *= Quaternion.Euler(rotationAxis * rotateAmount);
					}
				}

				previousMousePosition = mousePosition;

				yield return null;
			}

			for (int i = 0; i < transformCommands.Count; i++)
			{
				((TransformCommand)transformCommands[i]).StoreNewTransformValues();
			}
			CommandGroup commandGroup = new CommandGroup();
			commandGroup.Set(transformCommands);
			UndoRedoManager.Insert(commandGroup);

			totalRotationAmount = Quaternion.identity;
			totalScaleAmount = 0;
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
			if (nearAxis == Axis.None && Input.GetMouseButtonDown(0))
			{
				bool isAdding = Input.GetKey(AddSelection);
				bool isRemoving = Input.GetKey(RemoveSelection);

				if (Physics.Raycast(myCamera.ScreenPointToRay(Input.mousePosition), out var hitInfo, Mathf.Infinity, selectionMask))
				{
					Transform target = null;
					var hitObject = hitInfo.transform.gameObject;
					var hitParentObject = hitObject.transform.parent.gameObject;

					if (hitParentObject.tag.Equals("Model"))
					{
						// Debug.Log(hitParentObject.name + " Selected!!!!");
						target = hitParentObject.transform;
					}

					if (target == null)
					{
						ClearTargets();
					}
					else
					{
						if (isAdding)
						{
							AddTarget(target);
						}
						else if (isRemoving)
						{
							RemoveTarget(target);
						}
						else if (!isAdding && !isRemoving)
						{
							ClearAndAddTarget(target);
						}
					}
				}
				else
				{
					if (!isAdding && !isRemoving)
					{
						ClearTargets();
					}
				}
			}
		}

		public void AddTarget(Transform target, in bool addCommand = true)
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

				if (addCommand)
				{
					UndoRedoManager.Insert(new AddTargetCommand(this, target, targetRootsOrdered));
				}

				AddTargetRoot(target);
				AddTargetHighlightedRenderers(target);

				SetPivotPoint();
			}
		}

		public void RemoveTarget(Transform target, bool addCommand = true)
		{
			if (target != null)
			{
				if (!targetRoots.ContainsKey(target))
				{
					return;
				}

				if (addCommand)
				{
					UndoRedoManager.Insert(new RemoveTargetCommand(this, target));
				}

				RemoveTargetHighlightedRenderers(target);
				RemoveTargetRoot(target);

				SetPivotPoint();
			}
		}

		public void ClearTargets(bool addCommand = true)
		{
			if (addCommand)
			{
				UndoRedoManager.Insert(new ClearTargetsCommand(this, targetRootsOrdered));
			}

			ClearAllHighlightedRenderers();
			targetRoots.Clear();
			targetRootsOrdered.Clear();
			children.Clear();
		}

		void ClearAndAddTarget(Transform target)
		{
			UndoRedoManager.Insert(new ClearAndAddTargetCommand(this, target, targetRootsOrdered));

			ClearTargets(false);
			AddTarget(target, false);
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