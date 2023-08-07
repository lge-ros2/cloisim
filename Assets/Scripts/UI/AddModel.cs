
using UnityEngine;

public class AddModel : MonoBehaviour
{
	private GameObject modelList = null;
	private Transform targetObject = null;
	private SDF.Helper.Model _modelHelper = null;

	#region variables for the object with articulation body
	private ArticulationBody rootArticulationBody = null;
	private Vector3 articulationBodyDeployOffset = new Vector3(0, 0, 0);
	#endregion

	public float maxRayDistance = 100.0f;

	void Awake()
	{
		modelList = transform.parent.Find("ModelList").gameObject;
	}

	public void OnButtonClicked()
	{
		modelList.SetActive(!modelList.activeSelf);

		if (modelList.activeSelf)
			Main.CameraControl.BlockMouseWheelControl();
		else
			Main.CameraControl.UnBlockMouseWheelControl();
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
		if (targetObject != null)
		{
			GameObject.Destroy(targetObject.gameObject);
			targetObject = null;
		}
		rootArticulationBody = null;
		_modelHelper = null;
	}

	public void SetAddingModelForDeploy(in Transform targetTransform)
	{
		const float DeployOffset = 0.1f;

		RemoveAddingModel();

		targetObject = targetTransform;
		ChangeColliderObjectLayer(targetObject, "Ignore Raycast");

		rootArticulationBody = targetObject.GetComponentInChildren<ArticulationBody>();
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
		foreach (var renderer in targetObject.GetComponentsInChildren<Renderer>())
		{
			totalBound.Encapsulate(renderer.bounds);
		}

		// Debug.Log(totalBound.extents + " " + totalBound.center + " "  + totalBound.size);
		// Debug.Log(totalBound.extents.y + " " + totalBound.center.y + " "  + totalBound.size.y);
		articulationBodyDeployOffset.y = totalBound.extents.y + DeployOffset;

		_modelHelper = targetObject.GetComponent<SDF.Helper.Model>();
	}

	private bool GetPointAndNormalOnClick(out Vector3 point, out Vector3 normal)
	{
		point = Vector3.zero;
		normal = Vector3.zero;

		var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
		var layerMask = ~LayerMask.GetMask("Ignore Raycast");
		if (Physics.Raycast(ray, out var hitInfo, maxRayDistance, layerMask))
		{
			point = hitInfo.point;
			normal = hitInfo.normal;
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
			_modelHelper.SetPose(targetObject.position, targetObject.rotation);

			ChangeColliderObjectLayer(targetObject, "Default");

			targetObject = null;
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
				if (targetObject.position != point)
				{
					if (rootArticulationBody != null)
					{
						rootArticulationBody.Sleep();
						rootArticulationBody.TeleportRoot(point + articulationBodyDeployOffset, targetObject.rotation);
					}
					else
					{
						targetObject.position = point;
					}
				}
			}
		}
	}

	void LateUpdate()
	{
		if (targetObject != null)
		{
			HandlingAddedObject();
		}
		else
		{
			// close 'Add model' list Panel
			if (Input.GetKeyUp(KeyCode.Escape))
			{
				modelList.SetActive(false);
				Main.CameraControl.UnBlockMouseWheelControl();
			}
		}
	}
}