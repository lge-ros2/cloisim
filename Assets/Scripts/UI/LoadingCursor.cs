/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public class LoadingCursor : MonoBehaviour
{
	private const int FrameCount = 8;
	private const float FrameInterval = 0.05f;
	private const string ResourcePath = "icons/cursor_loading_";

	private readonly Texture2D[] _frames = new Texture2D[FrameCount];
	private int _frameIndex = 0;
	private float _nextFrameTime = 0f;

	public bool IsActive { get; private set; } = false;

	void Awake()
	{
		for (var i = 0; i < FrameCount; i++)
		{
			_frames[i] = Resources.Load<Texture2D>(ResourcePath + i);
		}

		enabled = false;
	}

	public void Activate()
	{
		IsActive = true;
		_frameIndex = 0;
		_nextFrameTime = 0f;
		enabled = true;
	}

	public void Deactivate()
	{
		IsActive = false;
		enabled = false;
		Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
	}

	void Update()
	{
		if (Time.unscaledTime >= _nextFrameTime)
		{
			var frame = _frames[_frameIndex];
			if (frame != null)
			{
				var hotspot = new Vector2(frame.width * 0.5f, frame.height * 0.5f);
				Cursor.SetCursor(frame, hotspot, CursorMode.Auto);
			}

			_frameIndex = (_frameIndex + 1) % FrameCount;
			_nextFrameTime = Time.unscaledTime + FrameInterval;
		}
	}
}
