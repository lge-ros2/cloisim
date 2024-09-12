/*
 * Copyright (c) 2024 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Collections;
using System;
using UnityEngine;

public class MowingBlade : MonoBehaviour
{
	[SerializeField]
	private float _bladeDiameter = 0;

	[SerializeField]
	private float _height = 0;

	[SerializeField]
	private UInt16 _revSpeed = 0;

	[SerializeField]
	private float _heightMin = 0;

	[SerializeField]
	private float _heightMax = 0;

	[SerializeField]
	private UInt16 _revSpeedMax = 0;

	private bool _doAdjust = false;

	public float Diameter
	{
		get => _bladeDiameter;
	}

	public float HeightMin
	{
		get => _heightMin;
		set => _heightMin = value;
	}

	public float HeightMax
	{
		get => _heightMax;
		set => _heightMax = value;
	}

	public UInt16 RevSpeedMax
	{
		get => _revSpeedMax;
		set => _revSpeedMax = value;
	}

	public float Height
	{
		get => _height;
		set => _height = (value <= _heightMin) ? _heightMin : (value >= _heightMax ? _heightMax : value);
	}

	public UInt16 RevSpeed
	{
		get => _revSpeed;
		set => _revSpeed = (value >= _revSpeedMax ? _revSpeedMax : value);
	}

	public Vector3 Position
	{
		get => this.transform.position;
	}

	public bool IsRunning()
	{
		return _revSpeed > float.Epsilon;
	}

	void Start()
	{
		StartCoroutine(CalculateBladeSize());
	}

	void OnDestroy()
	{
		_doAdjust = false;
	}

	private IEnumerator CalculateBladeSize()
	{
		var meshFilters = GetComponentsInChildren<MeshFilter>();

		var bladeBounds = new Bounds();
		foreach (var meshFilter in meshFilters)
		{
			var bounds = meshFilter.sharedMesh.bounds;
			bladeBounds.Encapsulate(bounds);
		}

		_bladeDiameter = Mathf.Max(bladeBounds.extents.x, bladeBounds.extents.z);

		_doAdjust = true;
		yield return DoAdjust();
	}

	private IEnumerator DoAdjust()
	{
		var targets = new List<float>();
		var indices = new List<int>();
		var articulationBody = GetComponent<ArticulationBody>();

		while (_doAdjust)
		{
			var dof = articulationBody.GetDriveTargets(targets);
			articulationBody.GetDofStartIndices(indices);
			if (dof > 0)
			{
				var bodyIndex = articulationBody.index;
				var targetIndex = indices[bodyIndex];
				var currentTarget = targets[targetIndex];
				var targetHeight = articulationBody.yDrive.lowerLimit + _height;
				if (targetHeight >= articulationBody.yDrive.upperLimit)
				{
					targetHeight = articulationBody.yDrive.upperLimit;
				}

				if (!Mathf.Approximately(currentTarget, targetHeight))
				{
					articulationBody.SetDriveTarget(ArticulationDriveAxis.Y, targetHeight);
				}
			}
			yield return null;
		}

		yield return null;
	}
}