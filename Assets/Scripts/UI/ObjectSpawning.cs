using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class ObjectSpawning : MonoBehaviour
{
	public enum PropsType {BOX = 0, CYLINDER = 1, SPHERE = 2};

	private GameObject propsRoot = null;

	private Dictionary<PropsType, GameObject> props = new Dictionary<PropsType, GameObject>();
	private Camera mainCam = null;

	private Color32 matColor = new Color32(43, 29, 14, 0);
	public float maxRayDistance = 100;
	private uint propsCount = 0;

	[Header("GUI properties")]
	private const int labelFontSize = 14;
	private const int TextWidth = 45;
	private Rect labelRect  = new Rect(Screen.width / 2 - TextWidth / 2, 10, TextWidth, 22);

	private const float guiHeight = 25f;
	private const float topMargin = 10f;
	private const float toolbarWidth = 190f;
	private string[] toolbarStrings = new string[] { "Box", "Cylinder", "Sphere" };

	private string scaleFactorString = "0.5";
	private int toolbarSelected = 0;

	void Awake()
	{
		propsRoot = GameObject.Find("Props");
		mainCam = Camera.main;
	}

	private GameObject CreateProps(in string name, in Mesh targetMesh)
	{
		var newObject = new GameObject(name);
		newObject.tag = "Props";
		newObject.isStatic = true;

		var meshFilter = newObject.AddComponent<MeshFilter>();
		meshFilter.mesh = targetMesh;

		var newMaterial = new Material(SDF2Unity.commonShader);
		newMaterial.name = targetMesh.name;
		newMaterial.color = matColor;

		var meshRenderer = newObject.AddComponent<MeshRenderer>();
		meshRenderer.material = newMaterial;
		meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;

		var meshCollider = newObject.AddComponent<MeshCollider>();
		meshCollider.sharedMesh = targetMesh;
		meshCollider.convex = true;
		meshCollider.isTrigger = false;

		var rigidBody = newObject.AddComponent<Rigidbody>();

		return newObject;
	}

	// Update is called once per frame
	void LateUpdate()
	{
		if (Input.GetKey(KeyCode.LeftControl))
		{
			if (Input.GetMouseButtonDown(0))
			{
				// Add On left click spawn
				// selected prefab and align its rotation to a surface normal
				var spawnData = GetPositionAndNormalOnClick();
				if (spawnData[0] != Vector3.zero)
				{
					var scaleFactor = float.Parse(scaleFactorString);
					var propsScale = new Vector3(scaleFactor, scaleFactor, scaleFactor);
					StartCoroutine(SpawnTargetObject((PropsType)toolbarSelected, spawnData[0], spawnData[1], propsScale));
				}
			}
			else if (Input.GetMouseButtonDown(1))
			{
				// Remove spawned prefab when holding left shift and left clicking
				var selectedPropsTransform = GetTransformOnClick();
				if (selectedPropsTransform)
				{
					StartCoroutine(DeleteTargetObject(selectedPropsTransform));
				}
			}
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

					var qAngle = Quaternion.AngleAxis(90, Vector3.forward);
					for (var index = 0; index < mesh.vertices.LongLength; index++)
					{
						mesh.vertices[index] = qAngle * mesh.vertices[index];
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
		}

		position.y += mesh.bounds.size.y / 2 + 0.001f;

		var spawanedObjectTransform = spawnedObject.transform;
		spawanedObjectTransform.position = position;
		spawanedObjectTransform.rotation = Quaternion.FromToRotation(spawanedObjectTransform.up, normal);

		spawnedObject.transform.localScale = scale;
		spawnedObject.transform.SetParent(propsRoot.transform);

		yield return null;
	}

	private IEnumerator DeleteTargetObject(Transform targetObjectTransform)
	{
		Destroy(targetObjectTransform.gameObject);
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

	void DrawShadow(in Rect rect, in string value)
	{
		var prevColor = GUI.skin.label.normal.textColor;

		GUI.skin.label.normal.textColor = new Color(0, 0, 0, 0.64f);
		var rectShadow = rect;
		rectShadow.x += 1;
		rectShadow.y += 1;
		GUI.Label(rectShadow, value);

		GUI.skin.label.normal.textColor = prevColor;
	}

	private string prevScaleFactorString;
	private bool checkScaleFactorFocused = false;
	private bool doCheckScaleFactorValue = false;
	void OnGUI()
	{
		var originLabelColor = GUI.skin.label.normal.textColor;

		GUI.skin.label.alignment = TextAnchor.MiddleRight;
		GUI.skin.label.fontSize = labelFontSize;
		GUI.skin.label.alignment = TextAnchor.MiddleCenter;

		var centerPointX = Screen.width / 2;

		var rectToolbar = new Rect(centerPointX - toolbarWidth / 2, topMargin, toolbarWidth, guiHeight);
		GUI.skin.label.normal.textColor = Color.white;
		toolbarSelected = GUI.Toolbar(rectToolbar, toolbarSelected, toolbarStrings);

		var rectToolbarLabel = rectToolbar;
		rectToolbarLabel.x -= 45;
		rectToolbarLabel.width = 45;

		DrawShadow(rectToolbarLabel, "Props: ");
		GUI.skin.label.normal.textColor = Color.white;
		GUI.Label(rectToolbarLabel, "Props: ");

		var rectScaleLabel = rectToolbar;
		rectScaleLabel.x += (toolbarWidth + 5);
		rectScaleLabel.width = 50;
		DrawShadow(rectScaleLabel, "Scale: ");
		GUI.skin.label.normal.textColor = Color.white;
		GUI.Label(rectScaleLabel, "Scale: ");

		var rectScale = rectScaleLabel;
		rectScale.x += 50;
		rectScale.width = 40;
		GUI.SetNextControlName("ScaleField");
		GUI.skin.textField.normal.textColor = Color.white;
		GUI.skin.textField.alignment = TextAnchor.MiddleCenter;
		scaleFactorString = GUI.TextField(rectScale, scaleFactorString, 5);

		if (checkScaleFactorFocused && !GUI.GetNameOfFocusedControl().Equals("ScaleField"))
		{
			doCheckScaleFactorValue = true;
			checkScaleFactorFocused = false;
			// Debug.Log("Focused out!!");
		}
		else if (!checkScaleFactorFocused && GUI.GetNameOfFocusedControl().Equals("ScaleField"))
		{
			// Debug.Log("Focused!!!");
			checkScaleFactorFocused = true;
			prevScaleFactorString = scaleFactorString;
		}

		if (doCheckScaleFactorValue)
		{
			// Debug.Log("Do check!! previous " + prevScaleFactorString);
			if (string.IsNullOrEmpty(scaleFactorString) )
			{
				scaleFactorString = prevScaleFactorString;
			}
			else
			{
				if (float.TryParse(scaleFactorString, out var scaleFactor))
				{
					if (scaleFactor < 0.1f)
					{
						scaleFactorString = "0.1";
					}
					else if (scaleFactor > 5f)
					{
						scaleFactorString = "5";
					}
				}
				else
				{
					scaleFactorString = prevScaleFactorString;
				}
			}
			doCheckScaleFactorValue = false;
		}

		GUI.skin.label.normal.textColor = originLabelColor;
	}
}