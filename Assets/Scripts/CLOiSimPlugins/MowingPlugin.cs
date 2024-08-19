/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
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

		public float heightThreshold = 0;

		public void SetBladingRatio(in float bladeMin, in float bladeMax, in float bladingHeight)
		{
			this.heightThreshold = bladingHeight;
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

			var dryMapUri = plugin.GetValue<string>("grass/dry/map/uri");
			if (!string.IsNullOrEmpty(dryMapUri))
			{
				var dryMapColorStr = plugin.GetValue<string>("grass/dry/color");
				var dryMapColor = SDF2Unity.Color(dryMapColorStr);
				SetDryGrass(dryMapUri, dryMapColor);
			}
		}

		private void CreateGrassMapTexture()
		{
			var targetWidth = (int)(bounds.size.z / mapResolution);
			var targetHeight = (int)(bounds.size.x / mapResolution);

			texture = new Texture2D(targetWidth, targetHeight, TextureFormat.R8, false);
			texture.name = "Grass Map";
			texture.Fill(Color.red);
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

		private void SetDryGrass(in string mapUri, in Color color)
		{
			// Debug.Log(mapUri);
			var texture = MeshLoader.GetTexture(mapUri);
			texture.name = "Dry Grass Map";
			material.SetColor("_DryGrassColor", color);
			material.SetTexture("_DryGrassMap", texture);
			material.EnableKeyword("DRY_GRASS_ON");
		}

		public void SetGrassOffset(Vector3 offset)
		{
			material.SetVector("_GrassOffset", offset);
		}

		public Vector3 GetGrassOffset()
		{
			return material.GetVector("_GrassOffset");
		}
	}

	private Grass _grass = null;
	private Blade _blade = null;

	private Color[] _initialTexturePixels = null;
	private Transform _targetPlane = null;
	private Transform _targetBlade = null;

	private List<MeshFilter> _punchingMeshFilters = new List<MeshFilter>();
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
			_grass.texture.Fill(ref _initialTexturePixels);
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
			bladeBounds.Encapsulate(bounds);
		}

		// Debug.Log(bladeBounds);
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
		_grass.SetGrassOffset(_targetPlane.position);
		_grass.SetBound(targetPlaneMesh.sharedMesh);
		_grass.Generate();

		StartCoroutine(PunchingGrass());

		AssignMaterial();
	}

	private IEnumerator PunchingGrass()
	{
		yield return null;

		var tempVisualMeshCollider = new List<MeshCollider>();

		CreateTempColliderInVisuals(ref tempVisualMeshCollider);

		yield return FindMeshFiltersToPunching();

		RemoveTempColliderInVisuals(ref tempVisualMeshCollider);

		yield return null;

		foreach (var meshFilter in _punchingMeshFilters)
		{
			PunchingTexture(_grass.texture, meshFilter);
			yield return null;
		}

		_initialTexturePixels = new Color[_grass.texture.GetPixels().LongLength];
		Array.Copy(_grass.texture.GetPixels(), _initialTexturePixels, _initialTexturePixels.LongLength);

		yield return StartMowing();
	}

	private void CreateTempColliderInVisuals(ref List<MeshCollider> tempMeshColliders)
	{
		var helperLinks = GetComponentsInChildren<SDF.Helper.Link>();
		foreach (var helperLink in helperLinks)
		{
			var meshColliders = helperLink.GetComponentsInChildren<MeshCollider>();
			if (meshColliders.Length == 0)
			{
				var helperVisuals = GetComponentsInChildren<SDF.Helper.Visual>();
				foreach (var helperVisual in helperVisuals)
				{
					var meshFilters = helperVisual.GetComponentsInChildren<MeshFilter>();
					foreach (var meshFilter in meshFilters)
					{
						var meshCollider = meshFilter.transform.gameObject.AddComponent<MeshCollider>();
						// Debug.Log(helperVisual.name + "," + meshFilter.name + "," + meshCollider.name);
						meshCollider.convex = true;
						meshCollider.isTrigger = true;
						tempMeshColliders.Add(meshCollider);
					}
				}
			}
		}
	}

	private IEnumerator FindMeshFiltersToPunching()
	{
		yield return null;

		var layerMask = LayerMask.GetMask("Default");

		var hitColliders = Physics.OverlapBox(_grass.bounds.center, _grass.bounds.extents, Quaternion.identity, layerMask);
		var i = 0;
		while (i < hitColliders.Length)
		{
			var hitCollider = hitColliders[i++];
			var helperModel = hitCollider.GetComponentInParent<SDF.Helper.Model>();
			// Debug.Log("Hit : " + hitCollider.name + "-" + i);

			if (helperModel != null)
			{
				// punching  other object on same target model
				if (helperModel.name.Equals(_grass.targetPlane.modelName))
				{
					var helperLink = hitCollider.GetComponentInParent<SDF.Helper.Link>();
					if (helperLink != null && !helperLink.name.Equals(_grass.targetPlane.linkName))
					{
						var meshFilters = helperLink.GetComponentsInChildren<MeshFilter>();
						_punchingMeshFilters.AddRange(meshFilters);
					}
				}
				else
				{
					var meshFilters = hitCollider.GetComponentsInChildren<MeshFilter>();
					_punchingMeshFilters.AddRange(meshFilters);
				}
			}
			else
			{
				if (hitCollider.CompareTag("Road"))
				{
					var meshFilters = hitCollider.GetComponentsInChildren<MeshFilter>();
					_punchingMeshFilters.AddRange(meshFilters);
				}
			}
			yield return null;
		}
	}

	private void RemoveTempColliderInVisuals(ref List<MeshCollider> meshColliders)
	{
		for (var i = 0; i < meshColliders.Count; i++)
		{
			GameObject.Destroy(meshColliders[i]);
		}
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
		var offset = _grass.GetGrassOffset();

		var mesh = meshFilter.sharedMesh;
		var vertices = mesh.vertices;
		var triangles = mesh.triangles;

		// Debug.Log(triangles.Length);
		// Debug.Log(vertices.Length);

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var p0 = vertices[triangles[i + 0]];
			var p1 = vertices[triangles[i + 1]];
			var p2 = vertices[triangles[i + 2]];
			var tp0 = meshFilter.transform.TransformPoint(p0) - offset;
			var tp1 = meshFilter.transform.TransformPoint(p1) - offset;
			var tp2 = meshFilter.transform.TransformPoint(p2) - offset;

			// Debug.Log("Punch >> " + tp0.y + ", " + tp1.y + ", " + tp2.y);
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
		}
	}

	private IEnumerator StartMowing()
	{
		const float mowingMargin = 0.01f;

		yield return null;

		var threshold =  _blade.heightThreshold + mowingMargin;
		var color = new Color(_blade.ratio, 0, 0, 0);
		var planeCenterPosition = _targetPlane.position;
		var bladeRadiusIntexture = _blade.size /_grass.mapResolution;

		// Debug.Log("blade threshold= " + threshold + ", bladeRadiusIntexture=" + bladeRadiusIntexture);
		while (true)
		{
			if (_startMowing && _targetBlade != null)
			{
				var bladePositionInTexture = _targetBlade.position;

				// Debug.Log("blade " + bladePositionInTexture.y.ToString("F4") + " threshold= " + threshold);
				if (bladePositionInTexture.y <= threshold)
				{
					bladePositionInTexture -= planeCenterPosition;
					bladePositionInTexture += _grass.bounds.extents;
					bladePositionInTexture /= _grass.mapResolution;
					// Debug.Log(bladePositionInTexture);

					_grass.texture.FillCircle(
						bladePositionInTexture.z,
						bladePositionInTexture.x,
						bladeRadiusIntexture,
						color);

					yield return null;
				}
			}
			yield return new WaitForEndOfFrame();
		}
	}
}
