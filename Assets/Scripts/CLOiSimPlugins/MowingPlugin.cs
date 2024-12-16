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

		public string modelName;
		public string linkName;

		public float mapResolution = 0.05f; // m/pixel
		public Blade blade = new Blade(-1, -1);

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
	private MowingBlade _mowingBlade = null;

	private Color[] _initialTexturePixels = null;
	private Transform _targetPlane = null;

	private List<MeshFilter> _punchingMeshFilters = new List<MeshFilter>();


	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;
		modelName = "World";
		partsName = this.GetType().Name;

		var geomGrassShader = Shader.Find("Custom/GeometryGrass");
		_grass = new Grass(geomGrassShader);
	}

	protected override void OnStart()
	{
		StartCoroutine(Start());
	}

	private IEnumerator Start()
	{
		yield return new WaitForEndOfFrame();

		var grassTarget = GetPluginParameters().GetValue<string>("grass/target");

		if (FindTargetPlane(grassTarget))
		{
			PlantGrass();
		}
		else
		{
			Debug.LogWarning("Target is not Plane");
		}

		var bladeTarget = GetPluginParameters().GetValue<string>("mowing/blade/target");
		if (FindTargetBlade(bladeTarget) == false)
		{
			Debug.LogWarning("Target blade not found");
		}
	}

	protected override void OnReset()
	{
		if (_initialTexturePixels != null)
		{
			Debug.Log($"{this.GetType().Name}: Reset Grass Texture");
			_grass.texture.Fill(ref _initialTexturePixels);
			_grass.texture.Apply();
		}
	}

	private bool FindTargetPlane(in string targetPlane)
	{
		(_grass.modelName, _grass.linkName) = SDF2Unity.GetModelLinkName(targetPlane);

		var modelHelpers = GetComponentsInChildren<SDF.Helper.Model>();
		var targetModel = modelHelpers.FirstOrDefault(x => x.name == _grass.modelName);

		_targetPlane = targetModel?.GetComponentsInChildren<SDF.Helper.Link>()
			.FirstOrDefault(x => x.name == _grass.linkName)?.transform;

		var targetPlaneCollision
				= _targetPlane?.GetComponentsInChildren<SDF.Helper.Collision>()
					.FirstOrDefault(x => x.gameObject.layer == LayerMask.NameToLayer("Plane"));

		return targetPlaneCollision != null;
	}

	private bool FindTargetBlade(in string targetBlade)
	{
		var (targetBladeModelName, targetBladeLinkName) = SDF2Unity.GetModelLinkName(targetBlade);

		var modelHelpers = GetComponentsInChildren<SDF.Helper.Model>();
		var targetModel = modelHelpers.FirstOrDefault(x => x.name == targetBladeModelName);

		var targetBladeLinkHelper = targetModel?.GetComponentsInChildren<SDF.Helper.Link>()
			.FirstOrDefault(x => x.name == targetBladeLinkName)?.transform;

		if (targetBladeLinkHelper == null)
		{
			return false;
		}

		_mowingBlade = targetBladeLinkHelper.GetComponent<MowingBlade>();

		if (_mowingBlade == null)
		{
			_mowingBlade = targetBladeLinkHelper.gameObject.AddComponent<MowingBlade>();
		}

		return true;
	}

	private void PlantGrass()
	{
		if (_targetPlane != null)
		{
			var targetPlaneMesh = _targetPlane?.GetComponentInChildren<MeshFilter>();
			_grass.SetMaterial(GetPluginParameters());
			_grass.SetGrassOffset(_targetPlane.position);
			_grass.SetBound(targetPlaneMesh.sharedMesh);
			_grass.Generate();

			StartCoroutine(PunchingGrass());

			AssignMaterial();
		}
	}

	private IEnumerator InitializeTexture()
	{
		_initialTexturePixels = new Color[_grass.texture.GetPixels().LongLength];
		Array.Copy(_grass.texture.GetPixels(), _initialTexturePixels, _initialTexturePixels.LongLength);
		yield return null;
	}

	private IEnumerator PunchingGrass()
	{
		var tempVisualMeshCollider = new List<MeshCollider>();

		CreateTempColliderInVisuals(ref tempVisualMeshCollider);

		FindMeshFiltersToPunching();

		RemoveTempColliderInVisuals(ref tempVisualMeshCollider);

		foreach (var meshFilter in _punchingMeshFilters)
		{
			PunchingTexture(ref _grass.texture, meshFilter);
		}

		yield return InitializeTexture();

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
				var helperVisuals = helperLink.GetComponentsInChildren<SDF.Helper.Visual>();
				foreach (var helperVisual in helperVisuals)
				{
					var meshFilters = helperVisual.GetComponentsInChildren<MeshFilter>();
					foreach (var meshFilter in meshFilters)
					{
						var meshCollider = meshFilter.gameObject.AddComponent<MeshCollider>();
						meshCollider.convex = true;
						meshCollider.isTrigger = false;
						tempMeshColliders.Add(meshCollider);
					}
				}
			}
		}
	}

	private void FindMeshFiltersToPunching()
	{
		var layerMask = LayerMask.GetMask("Default");

		var hitColliders = Physics.OverlapBox(_grass.bounds.center, _grass.bounds.extents, Quaternion.identity, layerMask);
		var i = 0;
		while (i < hitColliders.Length)
		{
			var hitCollider = hitColliders[i++];
			var helperModel = hitCollider.GetComponentInParent<SDF.Helper.Model>();
			// Debug.Log($"Hit: {helperModel?.name} {hitCollider.name}-{i}");

			if (helperModel != null)
			{
				// punching other object on same target model
				if (helperModel.name.Equals(_grass.modelName))
				{
					var helperLink = hitCollider.GetComponentInParent<SDF.Helper.Link>();
					if (helperLink != null && !helperLink.name.Equals(_grass.linkName))
					{
						var meshFilters = helperLink.GetComponentsInChildren<MeshFilter>();
						_punchingMeshFilters.AddRange(meshFilters);
					}
				}
				else
				{
					var meshFilters = helperModel.GetComponentsInChildren<MeshFilter>();
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

	private void PunchingTexture(ref Texture2D texture, in MeshFilter meshFilter)
	{
		var textureCenterX = (int)(texture.width * 0.5f);
		var textureCenterY = (int)(texture.height * 0.5f);
		var textureCenter = new Vector2(textureCenterX, textureCenterY);
		var offset = _grass.GetGrassOffset();
		var targetPlaneY = _targetPlane.position.y;

		var mesh = meshFilter.sharedMesh;
		var vertices = mesh.vertices;
		var triangles = mesh.triangles;

		for (var i = 0; i < triangles.Length; i += 3)
		{
			var p0 = vertices[triangles[i + 0]];
			var p1 = vertices[triangles[i + 1]];
			var p2 = vertices[triangles[i + 2]];
			var tp0 = meshFilter.transform.TransformPoint(p0) - offset;
			var tp1 = meshFilter.transform.TransformPoint(p1) - offset;
			var tp2 = meshFilter.transform.TransformPoint(p2) - offset;

			if ((tp0.y > _grass.blade.heightMax &&
				 tp1.y > _grass.blade.heightMax &&
				 tp2.y > _grass.blade.heightMax)
				|| (tp0.y < targetPlaneY && tp1.y < targetPlaneY && tp2.y < targetPlaneY))
			{
				// Debug.Log($"{meshFilter.name} {p0.y} {p1.y} {p2.y} => {tp0.y} {tp1.y} {tp2.y}");
				continue;
			}

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

	private IEnumerator StartMowing()
	{
		yield return null;

		var mowingThreshold = _grass.blade.heightMax;
		var mowingRatioInColor = Color.clear;
		var planeCenterPosition = _targetPlane.position;

		while (true)
		{
			if (_mowingBlade != null && _mowingBlade.IsRunning())
			{
				var bladeRadiusIntexture = _mowingBlade.Diameter /_grass.mapResolution;
				var bladeToPlaneDistance = _targetPlane.TransformPoint(_mowingBlade.Position);

				if (bladeToPlaneDistance.y <= mowingThreshold)
				{
					mowingRatioInColor.r = bladeToPlaneDistance.y / mowingThreshold;
					// Debug.Log($"{_grass.blade.heightMin}, {_grass.blade.heightMax}, {bladeToPlaneDistance.y} => {mowingRatioInColor.r }");

					var bladePositionInTexture = _mowingBlade.Position;
					bladePositionInTexture -= planeCenterPosition;
					bladePositionInTexture += _grass.bounds.extents;
					bladePositionInTexture /= _grass.mapResolution;

					_grass.texture.FillCircle(
						bladePositionInTexture.z,
						bladePositionInTexture.x,
						bladeRadiusIntexture,
						mowingRatioInColor,
						TextureUtil.FillOptions.Lesser);

					yield return null;
				}
				else
				{
					yield return new WaitForEndOfFrame();
				}
			}
			else
			{
				yield return new WaitForEndOfFrame();
			}
		}
	}
}
