/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;


[DefaultExecutionOrder(800)]
public class MowingPlugin : CLOiSimPlugin
{
	private float _grassMapResolution = 0.05f; // m/pixel
	private float _bladingRatio = 0.1f;

	private string _targetPlaneLinkName = string.Empty;
	private GameObject _mowingList = null;
	private Transform _targetPlaneTranform = null;
	private Material _grassMaterial = null;
	private Bounds _grassBounds = new Bounds();
	private Texture2D _grassTexture = null;
	private Texture2D _grassTextureInit = null;
	private bool _started = false;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;

		modelName = "Mowing";
		partsName = "_";

		_mowingList = new GameObject("MowingList");
		_mowingList.transform.SetParent(transform);

		var geomGrassShader = Shader.Find("Custom/GeometryGrass");
		_grassMaterial = new Material(geomGrassShader);
	}

	protected override void OnStart()
	{
		_targetPlaneLinkName = GetPluginParameters().GetValue<string>("target/link");

		if (FindTargetPlane(_targetPlaneLinkName) == false)
		{
			Debug.LogWarning("Target is not Plane");
		}
		else
		{
			PlantGrass();
		}
	}

	protected override void OnReset()
	{
		_grassTexture = Instantiate(_grassTextureInit);
	}

	private void SetGrassMaterial()
	{
		var colorBaseStr = GetPluginParameters().GetValue<string>("grass/color/base");
		var colorTipStr = GetPluginParameters().GetValue<string>("grass/color/tip");

		var bladeWidthMin = GetPluginParameters().GetValue<float>("grass/blade/width/min");
		var bladeWidthMax = GetPluginParameters().GetValue<float>("grass/blade/width/max");
		var bladeHeightMin = GetPluginParameters().GetValue<float>("grass/blade/height/min");
		var bladeHeightMax = GetPluginParameters().GetValue<float>("grass/blade/height/max");

		var bendBladeForwardAmount = GetPluginParameters().GetValue<float>("grass/bend/blade_amount/forward");
		var bendBladeCurvatureAmount = GetPluginParameters().GetValue<float>("grass/bend/blade_amount/curvature");
		var bendVariation = GetPluginParameters().GetValue<float>("grass/bend/variation");

		var tessAmount = GetPluginParameters().GetValue<float>("grass/tessellation/amount");
		var tessDistanceMin = GetPluginParameters().GetValue<float>("grass/tessellation/distance/min");
		var tessDistanceMax = GetPluginParameters().GetValue<float>("grass/tessellation/distance/max");

		var visibilityThreshold = GetPluginParameters().GetValue<float>("grass/visibility/threshold");
		var visibilityFalloff = GetPluginParameters().GetValue<float>("grass/visibility/falloff");

		_grassMapResolution = GetPluginParameters().GetValue<float>("grass/map/resolution", 0.05f);

		var bladingHeight = GetPluginParameters().GetValue<float>("mowing/blade/height", 0.01f);
		_bladingRatio = Mathf.Lerp(bladeHeightMin, bladeHeightMax, bladingHeight);
		// Debug.Log("_bladingRatio: " + _bladingRatio);

		if (_grassMaterial != null)
		{
			if (string.IsNullOrEmpty(colorBaseStr) == false)
			{
				var colorBase = SDF2Unity.Color(colorBaseStr);
				_grassMaterial.SetColor("_BaseColor", colorBase);
			}

			if (string.IsNullOrEmpty(colorTipStr) == false)
			{
				var colorTip = SDF2Unity.Color(colorTipStr);
				_grassMaterial.SetColor("_TipColor", colorTip);
			}

			_grassMaterial.SetFloat("_BladeWidthMin", bladeWidthMin);
			_grassMaterial.SetFloat("_BladeWidthMax", bladeWidthMax);
			_grassMaterial.SetFloat("_BladeHeightMin", bladeHeightMin);
			_grassMaterial.SetFloat("_BladeHeightMax", bladeHeightMax);
			_grassMaterial.SetFloat("_BladeBendDistance", bendBladeForwardAmount);
			_grassMaterial.SetFloat("_BladeBendCurve", bendBladeCurvatureAmount);
			_grassMaterial.SetFloat("_BladeBendDelta", bendVariation);
			_grassMaterial.SetFloat("_TessAmount", tessAmount);
			_grassMaterial.SetFloat("_TessMinDistance", tessDistanceMin);
			_grassMaterial.SetFloat("_TessMaxDistance", tessDistanceMax);

			_grassMaterial.SetFloat("_GrassThreshold", visibilityThreshold);
			_grassMaterial.SetFloat("_GrassFalloff", visibilityFalloff);
		}
	}

	private void SetGrassTexture()
	{
		if (_grassTexture != null)
		{
			_grassMaterial.SetTexture("_GrassMap", _grassTexture);
			_grassMaterial.EnableKeyword("VISIBILITY_ON");

			StartCoroutine(PunchingGrass());
		}
	}

	private bool FindTargetPlane(string targetPlaneLinkName)
	{
		_targetPlaneTranform = GetComponentsInChildren<Transform>().FirstOrDefault(x => x.name == targetPlaneLinkName);
		var targetPlaneCollision
				= _targetPlaneTranform?.GetComponentsInChildren<Transform>()
					.FirstOrDefault(x => x.gameObject.layer == LayerMask.NameToLayer("Plane"));

		return targetPlaneCollision != null;
	}

	private void PlantGrass()
	{
		if (_targetPlaneTranform == null)
			return;

		var targetPlaneMesh = _targetPlaneTranform?.GetComponentInChildren<MeshFilter>();
		SetGrassBound(targetPlaneMesh.sharedMesh);

		CreateGrassMap();

		SetGrassMaterial();
		SetGrassTexture();

		AssignMaterial();

		_started = true;
	}

	private void SetGrassBound(in Mesh mesh)
	{
		var grassBoundSize = mesh.bounds.size;
		var grassBoundsCenter = mesh.bounds.center;
		grassBoundSize.y = _grassMaterial.GetFloat("_BladeHeightMax");
		grassBoundsCenter.y += grassBoundSize.y * 0.5f;

		_grassBounds.size = grassBoundSize;
		_grassBounds.center = grassBoundsCenter;
	}

	private IEnumerator PunchingGrass()
	{
		yield return null;

		var layerMask = LayerMask.GetMask("Default");

		var hitColliders = Physics.OverlapBox(_grassBounds.center, _grassBounds.size * 0.5f, Quaternion.identity, layerMask);
		var i = 0;
		while (i < hitColliders.Length)
		{
			var hitCollider = hitColliders[i++];
			var helperModel = hitCollider.transform.GetComponentInParent<SDF.Helper.Model>();

			if (helperModel != null)
			{
				// Debug.Log("Hit : " + hitCollider.name + "-" + i + "   " + helperModel.name);
				if (helperModel.name.CompareTo(this.name) == 0)
				{
					var helperLink = hitCollider.transform.GetComponentInParent<SDF.Helper.Link>();
					if (helperLink != null)
					{
						if (helperLink.name.CompareTo(_targetPlaneLinkName) == 0)
						{
							continue;
						}

						var meshFilter = helperLink.GetComponentInChildren<MeshFilter>();
						PunchingTexture(_grassTexture, meshFilter);
						yield return null;
					}
				}
#if false // UNITY_EDITOR
				else
					Debug.Log("Another model Hit : " + helperModel.name + "-" + i);
#endif
			}
#if fasle // UNITY_EDITOR
			else
				Debug.Log("Hit : " + hitCollider.name + "-" + i);
#endif
		}

		_grassTextureInit = Instantiate(_grassTexture);

		yield return null;
	}

	private void AssignMaterial()
	{
		var targetPlaneMeshRenderer = _targetPlaneTranform?.GetComponentInChildren<MeshRenderer>();
		var materials = targetPlaneMeshRenderer.materials;
		var newMaterials = new Material[materials.Length + 1];
		materials.CopyTo(newMaterials, 0);
		newMaterials[newMaterials.Length - 1] = _grassMaterial;
		targetPlaneMeshRenderer.materials = newMaterials;
	}

	private void PunchingTexture(Texture2D texture, MeshFilter meshFilter)
	{
		var textureCenterX = (int)(texture.width * 0.5f);
		var textureCenterY = (int)(texture.height * 0.5f);
		var textureCenter = new Vector2(textureCenterX, textureCenterY);

		var mesh = meshFilter.sharedMesh;
		var vertices = mesh.vertices;
		var triangles = mesh.triangles;
		var threshold =  _grassBounds.center.y;

		// Debug.Log("threshold = " + threshold.ToString("F10"));
		// Debug.Log(triangles.Length);
		// Debug.Log(vertices.Length);

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var t0 = triangles[i + 0];
			var t1 = triangles[i + 1];
			var t2 = triangles[i + 2];
			var p0 = vertices[t0];
			var p1 = vertices[t1];
			var p2 = vertices[t2];
			var tp0 = meshFilter.transform.TransformPoint(p0);
			var tp1 = meshFilter.transform.TransformPoint(p1);
			var tp2 = meshFilter.transform.TransformPoint(p2);

			// Debug.Log("Punch >> " + p0 + ", " + p1 + ", " + p2);

			if (!(tp0.y > threshold && tp1.y > threshold && tp2.y > threshold))
			{
				var P1 = new Vector2(tp0.z, tp0.x);
				var P2 = new Vector2(tp1.z, tp1.x);
				var P3 = new Vector2(tp2.z, tp2.x);

				// Debug.Log("Punch >> " + P1 + ", " + P2 + ", " + P3);

				P1 /= _grassMapResolution;
				P2 /= _grassMapResolution;
				P3 /= _grassMapResolution;

				// Debug.Log("Punch >>>> " + P1 + ", " + P2 + ", " + P3);

				P1 += textureCenter;
				P2 += textureCenter;
				P3 += textureCenter;

				// Debug.Log("Punch >>>>>> " + P1 + ", " + P2 + ", " + P3);

				FillTriangle(_grassTexture, P1, P2, P3, Color.clear);
			}
#if false // UNITY_EDITOR
			else
			{
				Debug.Log(tp0 + ", " + tp1 + ", " + tp2);
			}
#endif
		}
	}

	private void FillTriangle(Texture2D texture, Vector2 v1, Vector2 v2, Vector2 v3, Color color)
	{
		var minX = Mathf.FloorToInt(Mathf.Min(v1.x, v2.x, v3.x));
		var minY = Mathf.FloorToInt(Mathf.Min(v1.y, v2.y, v3.y));
		var maxX = Mathf.CeilToInt(Mathf.Max(v1.x, v2.x, v3.x));
		var maxY = Mathf.CeilToInt(Mathf.Max(v1.y, v2.y, v3.y));

		// Iterate over the pixels within the bounding box and set the color of the pixels inside the triangle:
		for (var x = minX; x <= maxX; x++)
		{
			for (var y = minY; y <= maxY; y++)
			{
				var pixelCoord = new Vector2(x, y);
				if (IsPointInTriangle(pixelCoord, v1, v2, v3))
				{
					texture.SetPixel(x, y, color);
				}
			}
		}

		texture.Apply();
	}

	private bool IsPointInTriangle(Vector2 p, Vector2 v1, Vector2 v2, Vector2 v3)
	{
		// Calculate the barycentric coordinates
		var dominator = (v2.y - v3.y) * (v1.x - v3.x) + (v3.x - v2.x) * (v1.y - v3.y);
		if (Mathf.Abs(dominator) < float.Epsilon)
		{
			// Triangle is degenerate (i.e., all vertices are collinear)
			// Debug.LogWarning("denominator == 0 ");
			return false;
		}

		var alpha = ((v2.y - v3.y) * (p.x - v3.x) + (v3.x - v2.x) * (p.y - v3.y)) /
					  dominator;
		var beta = ((v3.y - v1.y) * (p.x - v3.x) + (v1.x - v3.x) * (p.y - v3.y)) /
					  dominator;
		var gamma = 1 - alpha - beta;

		// Check if the point is inside the triangle
		return alpha >= 0 && beta >= 0 && gamma >= 0;
	}

	private void CreateGrassMap()
	{
		var targetWidth = (int)(_grassBounds.size.z / _grassMapResolution);
		var targetHeight = (int)(_grassBounds.size.x / _grassMapResolution);

		_grassTexture = new Texture2D(targetWidth, targetHeight, TextureFormat.R8, false);
		_grassTexture.name = "Grass Map";

		var pixels = new Color[targetWidth * targetHeight];
		for (var i = 0; i < pixels.Length; i++)
		{
			pixels[i] = Color.red;
		}

		_grassTexture.SetPixels(pixels);
		_grassTexture.Apply();
	}

	void LateUpdate()
	{
	}

	List<Vector3> collisionPoints = new List<Vector3>();
	List<string> colliderList = new List<string>();

	private void OnTriggerStay(Collider collider)
	{
		if (_started)
		{
			// var collisionPoint = collider.ClosestPoint(transform.position);
			// collisionPoints.Add(collisionPoint);
			var helperLink = collider.transform.GetComponentInParent<SDF.Helper.Link>();
			var helperModel = collider.transform.GetComponentInParent<SDF.Helper.Model>();

			if (helperLink == null)
				Debug.Log("collider name = " + collider.transform.name);
			else
				Debug.Log("linkhelper name = " + helperLink.name);

			if (helperModel != null)
				Debug.Log("modelhelper name = " + helperModel.name);
		}
	}

	private void OnDrawGizmos()
	{
		foreach (var item in collisionPoints)
			Gizmos.DrawSphere(item, 0.1f);
	}
}