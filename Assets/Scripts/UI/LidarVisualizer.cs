/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LidarVisualizer : MonoBehaviour
{
	public string targetLidarName = string.Empty;

	// this should be even number
	public int textureSize = 0;

	public float updateRate = 0.1f;

	private int centerPosition = 0; // center point - Half of texture size

	private Texture2D targetTexture = null;

	void Awake()
	{
		targetLidarName = "hokuyo_laser";
	}

	void Start()
	{
		SetTextureSize(50);

		targetTexture = new Texture2D(textureSize, textureSize);

		ClearTexture(targetTexture);
		targetTexture.Apply();

		var targetRawImage = GetComponentInChildren<RawImage>();
		if (targetRawImage)
		{
			targetRawImage.texture = targetTexture;
		}

		StartCoroutine(DrawLidarVisualizer());
	}

	private void ClearTexture(in Texture2D targetTexture)
	{
		for (int i = 0; i < targetTexture.width; i++)
		{
			for (int j = 0; j < targetTexture.height; j++)
			{
				targetTexture.SetPixel(i, j, new Color(0, 1, 0, 0.3f));
			}
		}
	}

	private void DrawCenterMarker(in Texture2D targetTexture)
	{
		const int centerMarkerSize = 1;

		for (int i = centerPosition - centerMarkerSize; i < centerPosition + centerMarkerSize; i++)
		{
			for (int j = centerPosition - centerMarkerSize; j < centerPosition + centerMarkerSize; j++)
			{
				targetTexture.SetPixel(i, j, new Color(0, 0, 1, 0.5f));
			}
		}
	}

	private SensorDevices.Lidar FindTargetLidar()
	{
		return GameObject.Find(targetLidarName).GetComponent<SensorDevices.Lidar>();
	}

	private void SetTextureSize(in int size)
	{
		textureSize = size;
		centerPosition = textureSize/2;
	}

	private bool IsCenterRegion(in int index, in uint totalSamples)
	{
		return (index > (totalSamples * 3/7) && index < (totalSamples * 4/7))? true:false;
	}

	private IEnumerator DrawLidarVisualizer()
	{
		SensorDevices.Lidar targetLidar = null;
		var waitForSeconds = new WaitForSeconds(2f);
		while (targetLidar == null)
		{
			yield return waitForSeconds;

			try
			{
				targetLidar = FindTargetLidar();
			}
			catch
			{
				// Debug.Log("TargetLidar is not Ready");
			}
		}

		uint nSample = targetLidar.horizontal.samples;
		float fResolution = (float)targetLidar.horizontal.resolution;
		float fMin_Angle = (float)targetLidar.horizontal.angle.min; // -180 ~ 0
		float fMax_Angle = (float)targetLidar.horizontal.angle.max; //    0 ~ 180
		float fRange_Min = (float)targetLidar.range.min;
		float fRange_Max = (float)targetLidar.range.max;

		// Transparency
		var lidarNormalColor = new Color(1, 0, 0, 1f); // normal Lidar pixel color
		var lidarCenterColor = new Color(1, 1, 0, 1f); // Center Lidar pixel color

		float fResolutionAngle = (Mathf.Abs(fMin_Angle) + Mathf.Abs(fMax_Angle)) / nSample;
		var rayDirection = Vector3.forward;
		waitForSeconds = new WaitForSeconds(updateRate);

		while (true)
		{
			ClearTexture(targetTexture);
			DrawCenterMarker(targetTexture);

			var distances = targetLidar.GetRangeData();
			for (var index = 0; index < distances.Length; index++)
			{
				rayDirection = Quaternion.AngleAxis((fMin_Angle+(fResolutionAngle*index)),Vector3.up)*transform.forward;

				var fDistRate = distances[index] / fRange_Max;

				var pixelColor = (IsCenterRegion(index, nSample))? lidarCenterColor:lidarNormalColor;

				targetTexture.SetPixel(
						(int)(rayDirection.x * fDistRate * centerPosition) + centerPosition,
						(int)(rayDirection.z * fDistRate * centerPosition) + centerPosition,
						pixelColor);
			}

			targetTexture.Apply();

			yield return waitForSeconds;
		}
	}
}