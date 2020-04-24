/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

// using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModelPlugin : MonoBehaviour
{
	[Header("SDF Properties")]
	public bool isStatic = false;

	private bool isTopModel = false;

	public bool IsTopModel => isTopModel;

	private PoseControl poseControl = null;

	public ModelPlugin GetThisInTopParent()
	{
		var modelPlugins = GetComponentsInParent(typeof(ModelPlugin));
		return (ModelPlugin)modelPlugins[modelPlugins.Length - 1];
	}

	public LinkPlugin[] GetLinksInChildren()
	{
		return GetComponentsInChildren<LinkPlugin>();
	}

	public string GetModelName()
	{
		return name;
	}

	ModelPlugin()
	{
		poseControl = new PoseControl();
	}

	void Awake()
	{
		tag = "Model";
		isTopModel = SDF2Unity.CheckTopModel(transform);
		poseControl.SetTransform(transform);
	}

	private bool MakeBridgeJoint(Rigidbody targetRigidBody)
	{
		if (GetComponent<Rigidbody>() != null)
		{
			return false;
		}

		// Configure rigidbody for root object
		var rigidBody = gameObject.AddComponent<Rigidbody>();
		rigidBody.mass = 0.1f;
		rigidBody.drag = 0;
		rigidBody.angularDrag = 0;
		rigidBody.useGravity = false;
		rigidBody.isKinematic = false;
		rigidBody.ResetCenterOfMass();
		rigidBody.ResetInertiaTensor();

		var fixedJoint = gameObject.AddComponent<FixedJoint>();
		fixedJoint.connectedBody = targetRigidBody;
		fixedJoint.enableCollision = false;
		fixedJoint.enablePreprocessing = true;
		fixedJoint.massScale = 1;
		fixedJoint.connectedMassScale = 1;

		return true;
	}

	private Bounds GetMaxBounds()
	{
		var b = new Bounds(gameObject.transform.position, Vector3.zero);
		foreach (var r in GetComponentsInChildren<Renderer>())
		{
			b.Encapsulate(r.bounds);
		}
		// Debug.Log(name + ": " + b.extents + ", " + b.size + ", " + b.center + "," + b.min + "," + b.max );
		return b;
	}

	private void CreateGizmosSelectableModelBox()
	{
		var modelBound = GetMaxBounds();

		var newMesh = ProceduralMesh.CreateBox(modelBound.size.z, modelBound.size.y, modelBound.size.x);
		newMesh.name = "Model Box";

		// move Offset
		List<Vector3> boxVertices = new List<Vector3>();
		var offset = modelBound.size.y/2;
		newMesh.GetVertices(boxVertices);
		for (var index = 0; index < boxVertices.Count; index++)
		{
			var vertex = boxVertices[index];
			vertex.y += offset;
			boxVertices[index] = vertex;
		}
		newMesh.SetVertices(boxVertices);
		newMesh.Optimize();

		var meshFilter = gameObject.AddComponent<MeshFilter>();
		meshFilter.hideFlags = HideFlags.HideInInspector;
		meshFilter.sharedMesh = newMesh;

		// make it transparent
		var meshRenderer = gameObject.AddComponent<MeshRenderer>();
		meshRenderer.hideFlags = HideFlags.HideInInspector;
		meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		meshRenderer.receiveShadows = false;
		meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
		meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
		meshRenderer.motionVectorGenerationMode = UnityEngine.MotionVectorGenerationMode.ForceNoMotion;

		var newMaterial = new Material(Shader.Find("Standard"));
		newMaterial.SetFloat("_Mode", 2);
		newMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
 		newMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		newMaterial.SetFloat("_ZWrite", 0);
		newMaterial.DisableKeyword("_ALPHATEST_ON");
 		newMaterial.EnableKeyword("_ALPHABLEND_ON");
 		newMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
		newMaterial.SetFloat("_SpecularHighlights", 0); // 0:OFF, 1:ON
		newMaterial.SetFloat("_GlossyReflections", 0); // 0:OFF, 1:ON
		newMaterial.color = new Color(1, 1, 1, 0);
		newMaterial.renderQueue = 3000;
		newMaterial.hideFlags = HideFlags.HideInInspector;

		meshRenderer.material = newMaterial;
	}

	private void FindAndMakeBridgeJoint()
	{
		var rigidBodyChildren = GetComponentsInChildren<Rigidbody>();
		foreach (var rigidBodyChild in rigidBodyChildren)
		{
			// Get child component in only first depth!!!
			// And make bridge joint
			if (rigidBodyChild != null && rigidBodyChild.transform.parent == this.transform)
			{
				if (MakeBridgeJoint(rigidBodyChild) == true)
				{
					break;
				}
			}
		}
	}

	// Start is called before the first frame update
	void Start()
	{
		if (isTopModel)
		{
			FindAndMakeBridgeJoint();

			if (!isStatic)
			{
				CreateGizmosSelectableModelBox();
			}
		}
	}

	public void Reset()
	{
		poseControl.Reset();
	}

	public void SetPose(in Vector3 position, in Quaternion rotation)
	{
		AddPose(position, rotation);
		Reset();
	}

	public void AddPose(in Vector3 position, in Quaternion rotation)
	{
		poseControl.Add(position, rotation);
	}
}
