
using UnityEngine;

public class AddModel : MonoBehaviour
{
	private GameObject modelList = null;
	private Transform _targetObject = null;
	private SDF.Helper.Model _modelHelper = null;

	#region variables for the object with articulation body
	private ArticulationBody rootArticulationBody = null;
	private Vector3 modelDeployOffset = Vector3.zero;
	#endregion

	public float maxRayDistance = 60.0f;

	void Awake()
	{
		modelList = transform.parent.Find("ModelList").gameObject;
	}

	public void OnButtonClicked()
	{
		modelList.SetActive(!modelList.activeSelf);

		if (modelList.activeSelf)
			Main.CameraControl.BlockMouseWheelControl(true);
		else
			Main.CameraControl.BlockMouseWheelControl(false);
	}

	private static void ChangeColliderObjectLayer(Transform target, in string layerName)
	{
		foreach (var collider in target.GetComponentsInChildren<Collider>())
		{
			collider.gameObject.layer = LayerMask.NameToLayer(layerName);
		}
	}

	private void RemoveAddingModel()
	{
		if (_targetObject != null)
		{
			GameObject.Destroy(_targetObject.gameObject);
			_targetObject = null;
		}
		rootArticulationBody = null;
		_modelHelper = null;
	}

	public void SetAddingModelForDeploy(in Transform targetTransform)
	{
		const float DeployOffsetMargin = 0.1f;

		RemoveAddingModel();

		_targetObject = targetTransform;
		ChangeColliderObjectLayer(_targetObject, "Ignore Raycast");

		rootArticulationBody = _targetObject.GetComponentInChildren<ArticulationBody>();
		if (rootArticulationBody != null)
		{
			if (rootArticulationBody.isRoot)
			{
				rootArticulationBody.immovable = true;
			}
			else
			{
				rootArticulationBody = null;
			}
		}

		var totalBound = new Bounds();
		foreach (var collider in _targetObject.GetComponentsInChildren<Collider>())
		{
			// Debug.Log(collider.bounds.min + ", " + collider.bounds.max);
			totalBound.Encapsulate(collider.bounds);
		}

		modelDeployOffset.y = DeployOffsetMargin + ((totalBound.min.y < 0) ? -totalBound.min.y : 0);
		// Debug.Log("Deploy == " + modelDeployOffset.y + " " + totalBound.min + ", " + totalBound.center + "," + totalBound.extents);

		_modelHelper = _targetObject.GetComponent<SDF.Helper.Model>();
	}

	private bool GetPointAndNormalOnClick(out Vector3 point, out Vector3 normal)
	{
		point = Vector3.zero;
		normal = Vector3.zero;

		var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		var layerMask = ~(LayerMask.GetMask("Ignore Raycast") |
							LayerMask.GetMask("TransparentFX") |
							LayerMask.GetMask("UI") |
							LayerMask.GetMask("Water"));
		if (Physics.Raycast(ray, out var hitInfo, maxRayDistance, layerMask))
		{
			point = hitInfo.point;
			normal = hitInfo.normal;
			// Debug.Log(point + ", " + normal);
			return true;
		}
		return false;
	}

	private void HandlingAddedObject()
	{
		if (Input.GetMouseButtonUp(0))
		{
			if (rootArticulationBody != null)
			{
				rootArticulationBody.immovable = false;
			}

			// Update init pose
			_modelHelper.SetPose(_targetObject.position + modelDeployOffset, _targetObject.rotation);

			ChangeColliderObjectLayer(_targetObject, "Default");

			_targetObject = null;
			rootArticulationBody = null;
			_modelHelper = null;
		}
		else if (Input.GetKeyUp(KeyCode.Escape))
		{
			RemoveAddingModel();
		}
		else
		{
			if (GetPointAndNormalOnClick(out var point, out var normal))
			{
				if (_targetObject.position != point)
				{
					if (rootArticulationBody != null)
					{
						rootArticulationBody.Sleep();
						var bodyRotation = Quaternion.FromToRotation(transform.up, normal);
						rootArticulationBody.TeleportRoot(point + modelDeployOffset, bodyRotation);
					}
					else
					{
						_targetObject.position = point + modelDeployOffset;
					}
				}
			}
		}
	}

	void LateUpdate()
	{
		if (_targetObject != null)
		{
			HandlingAddedObject();
		}
		else
		{
			// close 'Add model' list Panel
			if (Input.GetKeyUp(KeyCode.Escape))
			{
				modelList.SetActive(false);
				Main.CameraControl.BlockMouseWheelControl(false);
			}
			else if (Input.GetKeyUp(KeyCode.F3))
			{
				OnButtonClicked();
			}
		}
	}
}