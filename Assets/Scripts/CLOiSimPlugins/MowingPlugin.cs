/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using System.Linq;
using System;
using UnityEngine;

[DefaultExecutionOrder(800)]
public class MowingPlugin : CLOiSimPlugin
{
	public struct Target
	{
		public string modelName;
		public string linkName;

		public Target(in string model, in string link)
		{
			modelName = model;
			linkName = link;
		}
	}

	private class Blade
	{
		public float ratio = 0.1f;

		public Target target;

		public float size = 0;

		public void SetBladingRatio(in float bladeMin, in float bladeMax, in float bladingHeight)
		{
			this.ratio = Mathf.Lerp(bladeMin, bladeMax, bladingHeight);
			// Debug.Log("_bladingRatio: " + this.ratio);
		}
	}

	private class Grass
	{
		public struct Blade
		{
			public float heightMin;
			public float heightMax;

			public Blade(in float min, in float max)
			{
				heightMin = min;
				heightMax = max;
			}

		}

		public float mapResolution = 0.05f; // m/pixel
		public Blade blade = new Blade(-1, -1);

		public Target targetPlane;
		public Material material = null;
		public Bounds bounds = new Bounds();
		public Texture2D texture = null;

		public Grass(Shader shader)
		{
			material = new Material(shader);
		}

		public void SetBound(in Mesh mesh)
		{
			if (blade.heightMin < 0 || blade.heightMax < 0)
			{
				Debug.LogWarning("SetMaterial first");
				return;
			}

			var boundSize = mesh.bounds.size;
			var boundCenter = mesh.bounds.center;
			boundSize.y = blade.heightMin;
			boundCenter.y += boundSize.y * 0.5f;

			bounds.size = boundSize;
			bounds.center = boundCenter;
		}

		public void SetMaterial(in SDF.Plugin plugin)
		{
			var colorBaseStr = plugin.GetValue<string>("grass/color/base");
			var colorTipStr = plugin.GetValue<string>("grass/color/tip");

			var bladeWidthMin = plugin.GetValue<float>("grass/blade/width/min");
			var bladeWidthMax = plugin.GetValue<float>("grass/blade/width/max");
			blade.heightMin = plugin.GetValue<float>("grass/blade/height/min");
			blade.heightMax = plugin.GetValue<float>("grass/blade/height/max");

			var bendBladeForwardAmount = plugin.GetValue<float>("grass/bend/blade_amount/forward");
			var bendBladeCurvatureAmount = plugin.GetValue<float>("grass/bend/blade_amount/curvature");
			var bendVariation = plugin.GetValue<float>("grass/bend/variation");

			var tessAmount = plugin.GetValue<float>("grass/tessellation/amount");
			var tessDistanceMin = plugin.GetValue<float>("grass/tessellation/distance/min");
			var tessDistanceMax = plugin.GetValue<float>("grass/tessellation/distance/max");

			var visibilityThreshold = plugin.GetValue<float>("grass/visibility/threshold");
			var visibilityFalloff = plugin.GetValue<float>("grass/visibility/falloff");

			mapResolution = plugin.GetValue<float>("grass/map/resolution", 0.05f);

			if (material != null)
			{
				if (string.IsNullOrEmpty(colorBaseStr) == false)
				{
					var colorBase = SDF2Unity.Color(colorBaseStr);
					material.SetColor("_BaseColor", colorBase);
				}

				if (string.IsNullOrEmpty(colorTipStr) == false)
				{
					var colorTip = SDF2Unity.Color(colorTipStr);
					material.SetColor("_TipColor", colorTip);
				}

				material.SetFloat("_BladeWidthMin", bladeWidthMin);
				material.SetFloat("_BladeWidthMax", bladeWidthMax);
				material.SetFloat("_BladeHeightMin", blade.heightMin);
				material.SetFloat("_BladeHeightMax", blade.heightMax);
				material.SetFloat("_BladeBendDistance", bendBladeForwardAmount);
				material.SetFloat("_BladeBendCurve", bendBladeCurvatureAmount);
				material.SetFloat("_BladeBendDelta", bendVariation);
				material.SetFloat("_TessAmount", tessAmount);
				material.SetFloat("_TessMinDistance", tessDistanceMin);
				material.SetFloat("_TessMaxDistance", tessDistanceMax);

				material.SetFloat("_GrassThreshold", visibilityThreshold);
				material.SetFloat("_GrassFalloff", visibilityFalloff);
			}
		}

		private void CreateGrassMapTexture()
		{
			var targetWidth = (int)(bounds.size.z / mapResolution);
			var targetHeight = (int)(bounds.size.x / mapResolution);

			texture = new Texture2D(targetWidth, targetHeight, TextureFormat.R8, false);
			texture.name = "Grass Map";

			var pixels = new Color[targetWidth * targetHeight];
			for (var i = 0; i < pixels.Length; i++)
			{
				pixels[i] = Color.red;
			}

			texture.SetPixels(pixels);
			texture.Apply();
		}

		public void Generate()
		{
			CreateGrassMapTexture();

			if (texture != null)
			{
				material.SetTexture("_GrassMap", texture);
				material.EnableKeyword("VISIBILITY_ON");
			}
		}
	}

	private Grass _grass = null;
	private Blade _blade = null;

	private Color[] _initialTexturePixels = null;
	private Transform _targetPlane = null;
	private Transform _targetBlade = null;

	private bool _startMowing = true;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;

		modelName = "Mowing";
		partsName = "_";

		var geomGrassShader = Shader.Find("Custom/GeometryGrass");
		_grass = new Grass(geomGrassShader);
		_blade = new Blade();
	}

	protected override void OnStart()
	{
		StartCoroutine(Start());
	}

	private IEnumerator Start()
	{
		yield return new WaitForEndOfFrame();

		var grassTarget = GetPluginParameters().GetValue<string>("grass/target");
		var grassTragetSplit = grassTarget.Split("::");

		_grass.targetPlane = new Target(grassTragetSplit[0], grassTragetSplit[1]);

		if (FindTargetPlane(_grass.targetPlane))
		{
			PlantGrass();
		}
		else
		{
			Debug.LogWarning("Target is not Plane");
		}

		var bladeTarget = GetPluginParameters().GetValue<string>("mowing/blade/target");
		var bladeTargetSplit = bladeTarget.Split("::");

		_blade.target = new Target(bladeTargetSplit[0], bladeTargetSplit[1]);

		if (FindTargetBlade(_blade.target) == false)
		{
			Debug.LogWarning("Target blade not found");
		}
	}

	protected override void OnReset()
	{
		if (_initialTexturePixels != null)
		{
			Debug.Log("Reset grass texture");
			_grass.texture.SetPixels(_initialTexturePixels);
			_grass.texture.Apply();
		}
	}

	private bool FindTargetPlane(Target targetPlane)
	{
		var modelHelpers = GetComponentsInChildren<SDF.Helper.Model>();
		var targetModel = modelHelpers.FirstOrDefault(x => x.name == targetPlane.modelName);

		_targetPlane = targetModel?.GetComponentsInChildren<SDF.Helper.Link>()
			.FirstOrDefault(x => x.name == targetPlane.linkName)?.transform;

		var targetPlaneCollision
				= _targetPlane?.GetComponentsInChildren<SDF.Helper.Collision>()
					.FirstOrDefault(x => x.gameObject.layer == LayerMask.NameToLayer("Plane"));

		return targetPlaneCollision != null;
	}

	private bool FindTargetBlade(Target targetBlade)
	{
		var modelHelpers = GetComponentsInChildren<SDF.Helper.Model>();
		var targetModel = modelHelpers.FirstOrDefault(x => x.name == targetBlade.modelName);

		_targetBlade = targetModel?.GetComponentsInChildren<SDF.Helper.Link>()
			.FirstOrDefault(x => x.name == targetBlade.linkName)?.transform;

		if (_targetBlade == null)
		{
			return false;
		}

		var meshFilters = _targetBlade.GetComponentsInChildren<MeshFilter>();

		var bladeBounds = new Bounds();
		foreach (var meshFilter in meshFilters)
		{
			// Debug.Log(meshFilter.name);
			// Debug.Log(meshFilter.sharedMesh.bounds);
			var bounds = meshFilter.sharedMesh.bounds;
			bounds.center = Vector3.zero;
			bladeBounds.Encapsulate(bounds);
		}

		_blade.size = Mathf.Max(bladeBounds.extents.x, bladeBounds.extents.z);
		var bladeMin = _targetPlane.TransformPoint(_targetBlade.position);
		// Debug.Log(bladeMin.y);

		_blade.SetBladingRatio(_grass.blade.heightMin, _grass.blade.heightMax, bladeMin.y);

		return true;
	}

	private void PlantGrass()
	{
		if (_targetPlane == null)
			return;

		var targetPlaneMesh = _targetPlane?.GetComponentInChildren<MeshFilter>();
		_grass.SetMaterial(GetPluginParameters());
		_grass.SetBound(targetPlaneMesh.sharedMesh);
		_grass.Generate();

		StartCoroutine(PunchingGrass());

		AssignMaterial();
	}

	private IEnumerator PunchingGrass()
	{
		yield return null;

		var layerMask = LayerMask.GetMask("Default");

		var hitColliders = Physics.OverlapBox(_grass.bounds.center, _grass.bounds.size * 0.5f, Quaternion.identity, layerMask);
		var i = 0;
		while (i < hitColliders.Length)
		{
			var hitCollider = hitColliders[i++];
			var helperModel = hitCollider.transform.GetComponentInParent<SDF.Helper.Model>();

			if (helperModel == null)
			{
				continue;
			}

			// Debug.Log("Hit : " + hitCollider.name + "-" + i + "   " + helperModel.name);
			if (helperModel.name.CompareTo(_grass.targetPlane.modelName) == 0)
			{
				var helperLink = hitCollider.transform.GetComponentInParent<SDF.Helper.Link>();

				if (helperLink == null || helperLink.name.CompareTo(_grass.targetPlane.linkName) == 0)
				{
					continue;
				}

				var meshFilter = helperLink.GetComponentInChildren<MeshFilter>();
				PunchingTexture(_grass.texture, meshFilter);
				yield return null;
			}
		}


		_initialTexturePixels = new Color[_grass.texture.GetPixels().LongLength];
		Array.Copy(_grass.texture.GetPixels(), _initialTexturePixels, _initialTexturePixels.LongLength);

		yield return StartMowing();
	}

	private void AssignMaterial()
	{
		var targetPlaneMeshRenderer = _targetPlane?.GetComponentInChildren<MeshRenderer>();
		var materials = targetPlaneMeshRenderer.materials;
		var newMaterials = new Material[materials.Length + 1];
		materials.CopyTo(newMaterials, 0);
		newMaterials[newMaterials.Length - 1] = _grass.material;
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
		var threshold = _grass.bounds.center.y;

		// Debug.Log("threshold = " + threshold.ToString("F10"));
		// Debug.Log(triangles.Length);
		// Debug.Log(vertices.Length);

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var p0 = vertices[triangles[i + 0]];
			var p1 = vertices[triangles[i + 1]];
			var p2 = vertices[triangles[i + 2]];
			var tp0 = meshFilter.transform.TransformPoint(p0);
			var tp1 = meshFilter.transform.TransformPoint(p1);
			var tp2 = meshFilter.transform.TransformPoint(p2);

			// Debug.Log("Punch >> " + p0 + ", " + p1 + ", " + p2);

			if (!(tp0.y > threshold && tp1.y > threshold && tp2.y > threshold))
			{
				var P1 = new Vector2(tp0.z, tp0.x);
				var P2 = new Vector2(tp1.z, tp1.x);
				var P3 = new Vector2(tp2.z, tp2.x);

				P1 /= _grass.mapResolution;
				P2 /= _grass.mapResolution;
				P3 /= _grass.mapResolution;

				P1 += textureCenter;
				P2 += textureCenter;
				P3 += textureCenter;

				texture.FillTriangle(P1, P2, P3, Color.clear);
			}
#if false // UNITY_EDITOR
			else
			{
				Debug.Log(tp0 + ", " + tp1 + ", " + tp2);
			}
#endif
		}
	}

	void LateUpdate()
	{
	}

	private IEnumerator StartMowing()
	{
		yield return null;

		var color = new Color(_blade.ratio, 0, 0, 0);
		var planeCenterPosition = _targetPlane.position;
		var bladeRadiusIntexture = (int)(_blade.size /_grass.mapResolution);

		while (true)
		{
			if (_startMowing && _targetBlade != null)
			{
				var bladePositionInTexture = new Vector3(
						_targetBlade.position.x,
						0,
						_targetBlade.position.z);
				bladePositionInTexture -= planeCenterPosition;
				bladePositionInTexture -= _grass.bounds.extents;
				// Debug.Log(bladePositionInTexture);

				bladePositionInTexture /= _grass.mapResolution;

				_grass.texture.FillCircle(
					(int)bladePositionInTexture.z,
					(int)bladePositionInTexture.x,
					bladeRadiusIntexture,
					color);
			}
			yield return new WaitForEndOfFrame();
		}
	}
}
