/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnityEngine;
using ShadowCastingMode = UnityEngine.Rendering.ShadowCastingMode;

[RequireComponent(typeof(ParticleSystem))]
public class ParticleSystemPlugin : CLOiSimPlugin
{
	private ParticleSystem _particleSystem = null;
	private ParticleSystemRenderer _particleSystemRenderer = null;

	protected override void OnAwake()
	{
		type = ICLOiSimPlugin.Type.NONE;
		_modelName = "World";
		_partsName = this.GetType().Name;

		_particleSystem = this.gameObject.GetComponent<ParticleSystem>();
		_particleSystemRenderer = this.gameObject.GetComponent<ParticleSystemRenderer>();
	}

	protected override void OnStart()
	{
		SetParticleMain();
		SetParticleEmission();
		SetParticleShape();
		SetParticleNoise();
		SetParticleCollision(); // TODO: consider performance
		SetParticleRenderer();
	}

	private void SetParticleMain()
	{
		var startLifetime = GetPluginParameters().GetValue<float>("main/start/lifetime", 5);
		var startSpeed = GetPluginParameters().GetValue<float>("main/start/speed", 5);
		var startSize = GetPluginParameters().GetValue<float>("main/start/size", 1);
		var gravityModifier = GetPluginParameters().GetValue<float>("main/gravity_modifier", 0);
		var simulationSpeed = GetPluginParameters().GetValue<float>("main/simulation_speed", 1);
		var maxParticles = GetPluginParameters().GetValue<int>("main/max_particles", 1000);

		var particleMain = _particleSystem.main;
		particleMain.startLifetimeMultiplier = startLifetime;
		particleMain.startSpeedMultiplier = startSpeed;
		particleMain.gravityModifierMultiplier = gravityModifier;
		particleMain.simulationSpeed = simulationSpeed;
		particleMain.maxParticles = maxParticles;
	}

	private void SetParticleEmission()
	{
		var emissionRateOverTime = GetPluginParameters().GetValue<float>("emission/rate/over_time");
		var particleEmission = _particleSystem.emission;
		particleEmission.rateOverTimeMultiplier = emissionRateOverTime;
	}

	private void SetParticleShape()
	{
		var angle = GetPluginParameters().GetValue<float>("shape/angle");
		var radius = GetPluginParameters().GetValue<float>("shape/radius");
		var rotationStr = GetPluginParameters().GetValue<string>("shape/rotation");
		var rotation = SDF2Unity.Rotation(new SDF.Quaternion<float>(rotationStr));
		// Debug.Log("shapeRotation: " + shapeRotation);

		var particleShape = _particleSystem.shape;
		particleShape.angle = angle;
		particleShape.radius = radius;
		particleShape.rotation = rotation.eulerAngles;
		particleShape.alignToDirection = true;
		particleShape.randomDirectionAmount = 1.0f;
		particleShape.arcMode = ParticleSystemShapeMultiModeValue.Random;
		particleShape.arc = 360;
		particleShape.arcSpread = 1.0f;
	}

	private void SetParticleNoise()
	{
		var particleNoise = _particleSystem.noise;

		if (GetPluginParameters().IsValidNode("noise"))
		{
			particleNoise.enabled = true;

			var noiseStrength = GetPluginParameters().GetValue<float>("noise/strength");
			var noiseFrequency = GetPluginParameters().GetValue<float>("noise/frequency");
			var noiseScrollSpeed = GetPluginParameters().GetValue<float>("noise/scroll_speed");
			particleNoise.strengthMultiplier = noiseStrength;
			particleNoise.frequency = noiseFrequency;
			particleNoise.scrollSpeedMultiplier = noiseScrollSpeed;
		}
	}

	private void SetParticleCollision()
	{
		var particleCollision = _particleSystem.collision;

		if (GetPluginParameters().IsValidNode("collision"))
		{
			particleCollision.enabled = true;

			var bounce = GetPluginParameters().GetValue<float>("collision/bounce");
			particleCollision.type = ParticleSystemCollisionType.World;
			particleCollision.bounceMultiplier = bounce;
		}
	}

	private void SetParticleRenderer()
	{
		var sizeMin = GetPluginParameters().GetValue<float>("renderer/billboard/particle_size/min");
		var sizeMax = GetPluginParameters().GetValue<float>("renderer/billboard/particle_size/max");
		var castShadows = GetPluginParameters().GetValue<bool>("renderer/cast_shadows");
		var materilColorString = GetPluginParameters().GetValue<string>("renderer/material/color");
		var materialColor = SDF2Unity.Color(materilColorString);

		var particleMaterial = Instantiate(Resources.Load<Material>("Materials/Particle Material"));
		particleMaterial.color = materialColor;

		_particleSystemRenderer.minParticleSize = sizeMin;
		_particleSystemRenderer.maxParticleSize = sizeMax;
		_particleSystemRenderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
		_particleSystemRenderer.material = particleMaterial;
	}


	// private IEnumerator DeployObjectsOnPlane(GameObject targetModel)
	// {
	// 	yield return null;

	// 	var bounds = new Bounds();
	// 	var meshFilters = targetModel.GetComponentsInChildren<MeshFilter>();
	// 	foreach (var meshFilter in meshFilters)
	// 	{
	// 		Debug.Log(meshFilter.name + "," + meshFilter.sharedMesh.bounds.center + ", " + meshFilter.sharedMesh.bounds.size);
	// 		bounds.Encapsulate(meshFilter.sharedMesh.bounds);
	// 	}

	// 	yield return null;

	// 	// Debug.Log("bounds.size=" + bounds.size);
	// 	var deployObjectSize = new Rect(0, 0, bounds.size.x, bounds.size.z);

	// 	if (deployObjectSize.width > _planeSize.width ||
	// 		deployObjectSize.height > _planeSize.height)
	// 	{
	// 		Debug.LogWarning("Not enough space on the plane");
	// 	}
	// 	else
	// 	{
	// 		var rowCount = Mathf.CeilToInt(_planeSize.width / deployObjectSize.width);
	// 		var colCount = Mathf.CeilToInt(_planeSize.height / deployObjectSize.height);
	// 		Debug.Log("rowCount=" + rowCount + ", colCount=" + colCount);

	// 		var initialOffset = new Rect(_planeSize.width/2, _planeSize.height/2, 0, 0);


	// 		for (int i = 0; i < rowCount; i++)
	// 		{
	// 			for (int j = 0; j < colCount; j++)
	// 			{
	// 				var cloned = GameObject.Instantiate(targetModel);
	// 				cloned.transform.SetParent(_mowingList.transform);

	// 				var x = i * deployObjectSize.width - initialOffset.x;
	// 				var y = j * deployObjectSize.height - initialOffset.y;
	// 				var offset = new Vector3(x, 0, y);
	// 				cloned.transform.localPosition = offset;
	// 			}
	// 			yield return null;
	// 		}
	// 	}

	// 	yield return null;
	// }

	protected override void OnReset()
	{
	}

#if UNITY_EDITOR
	private void OnDrawGizmos()
	{
	}
#endif

	void LateUpdate()
	{
	}
}