using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine;

public class ObjectSpawning : MonoBehaviour
{
	public enum PropsType {BOX = 0, CYLINDER = 1, SPHERE = 2};

	private GameObject propsRoot = null;
	private Camera mainCam = null;
	private RuntimeGizmos.TransformGizmo transformGizmo = null;

	private Quaternion _cylinderRotationAngle = Quaternion.AngleAxis(90, Vector3.forward);

	private Dictionary<PropsType, GameObject> props = new Dictionary<PropsType, GameObject>();
	public float maxRayDistance = 100;
	private uint propsCount = 0;

	private string scaleFactorString = "0.5";
	private int propType = 0;

	public void SetScaleFactor(in string value)
	{
		scaleFactorString = value;
	}

	public void SetPropType(in int value)
	{
		propType = value;
	}

	void Awake()
	{
		propsRoot = GameObject.Find("Props");
		mainCam = Camera.main;
		transformGizmo = Main.Gizmos;
	}

	// Update is called once per frame
	void LateUpdate()
	{
		var leftControlPressed = Input.GetKey(KeyCode.LeftControl);

		if (leftControlPressed && Input.GetMouseButtonDown(0))
		{
			// Add On left click spawn
			// selected prefab and align its rotation to a surface normal
			var spawnData = GetPositionAndNormalOnClick();
			if (spawnData[0] != Vector3.zero)
			{
				var scaleFactor = float.Parse(scaleFactorString);
				var propsScale = Vector3.one * scaleFactor;
				StartCoroutine(SpawnTargetObject((PropsType)propType, spawnData[0], spawnData[1], propsScale));
			}
		}
		else if ((leftControlPressed && Input.GetMouseButtonDown(1)))
		{
			// Remove spawned prefab when holding left control and right clicking
			var selectedPropsTransform = GetTransformOnClick();
			if (selectedPropsTransform)
			{
				StartCoroutine(DeleteTargetObject(selectedPropsTransform));
			}
		}
		else if (Input.GetKey(KeyCode.Delete))
		{
			transformGizmo.GetSelectedTargets(out var list);

			foreach (var target in list)
			{
				StartCoroutine(DeleteTargetObject(target));
			}
			transformGizmo.ClearTargets();
		}
	}

	private IEnumerator SpawnTargetObject(PropsType type, Vector3 position, Vector3 normal, Vector3 scale)
	{
		GameObject spawnedObject = null;
		Mesh mesh = null;
		var propsName = type.ToString() + "-" + propsCount++;

		if (!props.ContainsKey(type))
		{
			switch (type)
			{
				case PropsType.BOX:
					mesh = ProceduralMesh.CreateBox();
					break;

				case PropsType.CYLINDER:
					mesh = ProceduralMesh.CreateCylinder(0.5f, 1, 20);

					for (var index = 0; index < mesh.vertices.LongLength; index++)
					{
						mesh.vertices[index] = _cylinderRotationAngle * mesh.vertices[index];
					}
					break;

				case PropsType.SPHERE:
					mesh = ProceduralMesh.CreateSphere(0.5f, 14, 10);
					break;
			}

			if (mesh != null)
			{
				var newTempPropsObject = CreateProps(propsName, mesh);
				props.Add(type, newTempPropsObject);
				newTempPropsObject.hideFlags = HideFlags.HideAndDontSave;
				newTempPropsObject.SetActive(false);
			}
		}

		if (props.TryGetValue(type, out var targetObject))
		{
			spawnedObject = Instantiate(targetObject);
			spawnedObject.name = propsName;
			spawnedObject.SetActive(true);
			var meshFilter = spawnedObject.GetComponentInChildren<MeshFilter>();
			mesh = meshFilter.sharedMesh;

			var meshRender = spawnedObject.GetComponentInChildren<MeshRenderer>();
			meshRender.material.color = Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.6f, 1f);
		}

		if (mesh != null)
		{
			position.y += mesh.bounds.extents.y + 0.001f;
		}

		var spawanedObjectTransform = spawnedObject.transform;
		spawanedObjectTransform.position = position;
		spawanedObjectTransform.rotation = Quaternion.FromToRotation(spawanedObjectTransform.up, normal);

		spawnedObject.transform.localScale = scale;
		spawnedObject.transform.SetParent(propsRoot.transform);

		yield return null;
	}

	private GameObject CreateProps(in string name, in Mesh targetMesh)
	{
		var newObject = new GameObject(name);
		newObject.tag = "Props";
		newObject.isStatic = true;

		var meshFilter = newObject.AddComponent<MeshFilter>();
		meshFilter.sharedMesh = targetMesh;

		var newMaterial = new Material(SDF2Unity.CommonShader);
		newMaterial.name = targetMesh.name;
		newMaterial.color = Color.white;

		Debug.Log(Random.ColorHSV(0f, 1f, 0.7f, 1f, 0.6f, 1f));

		var meshRenderer = newObject.AddComponent<MeshRenderer>();
		meshRenderer.material = newMaterial;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;

		var meshCollider = newObject.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = targetMesh;
		meshCollider.convex = true;
		meshCollider.isTrigger = false;

		var rigidBody = newObject.AddComponent<Rigidbody>();
		rigidBody.drag = 0.5f;
		rigidBody.angularDrag= 0.8f;

		var navMeshObstacle = newObject.AddComponent<NavMeshObstacle>();
		navMeshObstacle.carving = true;
		navMeshObstacle.size = targetMesh.bounds.size;

		return newObject;
	}

	private IEnumerator DeleteTargetObject(Transform targetObjectTransform)
	{
		if (targetObjectTransform.CompareTag("Props"))
		{
			Destroy(targetObjectTransform.gameObject);
		}
		yield return null;
	}

	private Vector3[] GetPositionAndNormalOnClick()
	{
		var returnData = new Vector3[] { Vector3.zero, Vector3.zero }; //0 = spawn poisiton, 1 = surface normal
		var ray = mainCam.ScreenPointToRay(Input.mousePosition);

		if (Physics.Raycast(ray, out var hit, maxRayDistance))
		{
			returnData[0] = hit.point;
			returnData[1] = hit.normal;
		}

		return returnData;
	}

	private Transform GetTransformOnClick()
	{
		var screenPoint2Ray = mainCam.ScreenPointToRay(Input.mousePosition);

		if (Physics.Raycast(screenPoint2Ray, out var hit, maxRayDistance))
		{
			var parent = hit.transform.parent;
			if (parent.name.Equals("Props") && hit.transform.CompareTag("Props"))
			{
				return hit.transform;
			}
		}

		return null;
	}
}