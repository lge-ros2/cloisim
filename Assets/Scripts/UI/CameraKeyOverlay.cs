/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;
using UnityEngine.UIElements;

[Flags]
public enum CameraKeyOverlayInput
{
	None = 0,
	KeyQ = 1 << 0,
	KeyW = 1 << 1,
	KeyE = 1 << 2,
	KeyR = 1 << 3,
	KeyA = 1 << 4,
	KeyS = 1 << 5,
	KeyD = 1 << 6,
	KeyF = 1 << 7,
	LeftShift = 1 << 8
}

public sealed class CameraKeyOverlay
{
	private readonly struct KeyElement
	{
		public readonly CameraKeyOverlayInput Input;
		public readonly VisualElement Element;

		public KeyElement(CameraKeyOverlayInput input, VisualElement element)
		{
			Input = input;
			Element = element;
		}
	}

	private const string ActiveClassName = "camera-key--active";
	private const float VisibleOpacity = 0.72f;
	private const float FadeDelay = 0.1f;
	private const float FadeDuration = 0.28f;

	private readonly VisualElement _root = null;
	private readonly KeyElement[] _keyElements = null;

	private CameraKeyOverlayInput _currentInputs = CameraKeyOverlayInput.None;
	private float _lastActiveTime = float.NegativeInfinity;
	private bool _isVisible = false;

	public CameraKeyOverlay(VisualElement parent)
	{
		if (parent == null)
		{
			return;
		}

		_root = new VisualElement
		{
			name = "CameraKeyOverlay",
			pickingMode = PickingMode.Ignore
		};
		_root.style.display = DisplayStyle.None;
		_root.style.opacity = 0f;

		var board = CreateElement("camera-key-overlay-board");
		var cluster = CreateElement("camera-key-overlay-cluster");

		var keyQ = CreateKey(CameraKeyOverlayInput.KeyQ, "Q");
		var keyW = CreateKey(CameraKeyOverlayInput.KeyW, "W");
		var keyE = CreateKey(CameraKeyOverlayInput.KeyE, "E");
		var keyR = CreateKey(CameraKeyOverlayInput.KeyR, "R");
		var keyA = CreateKey(CameraKeyOverlayInput.KeyA, "A");
		var keyS = CreateKey(CameraKeyOverlayInput.KeyS, "S");
		var keyD = CreateKey(CameraKeyOverlayInput.KeyD, "D");
		var keyF = CreateKey(CameraKeyOverlayInput.KeyF, "F");
		var keyLeftShift = CreateKey(CameraKeyOverlayInput.LeftShift, "Left Shift", "camera-key--wide");

		var qRowSpacer = new VisualElement { pickingMode = PickingMode.Ignore };
		qRowSpacer.AddToClassList("camera-key--wide");
		qRowSpacer.style.visibility = Visibility.Hidden;
		qRowSpacer.style.height = 0;
		qRowSpacer.style.marginTop = 0;
		qRowSpacer.style.marginBottom = 0;

		AddRow(cluster, qRowSpacer, keyQ.Element, keyW.Element, keyE.Element, keyR.Element);
		AddRow(cluster, keyLeftShift.Element, keyA.Element, keyS.Element, keyD.Element, keyF.Element);

		board.Add(cluster);
		_root.Add(board);
		parent.Add(_root);
		_root.BringToFront();

		_keyElements = new[]
		{
			keyQ,
			keyW,
			keyE,
			keyR,
			keyA,
			keyS,
			keyD,
			keyF,
			keyLeftShift
		};
	}

	public void Update(CameraKeyOverlayInput activeInputs)
	{
		if (_root == null)
		{
			return;
		}

		var currentTime = Time.unscaledTime;
		if (activeInputs != CameraKeyOverlayInput.None)
		{
			_lastActiveTime = currentTime;
			SetVisible(true);
			_root.BringToFront();
			_root.style.opacity = VisibleOpacity;
			SetActiveInputs(activeInputs);
			return;
		}

		if (!_isVisible)
		{
			return;
		}

		SetActiveInputs(CameraKeyOverlayInput.None);

		var fadeElapsedTime = currentTime - _lastActiveTime - FadeDelay;
		if (fadeElapsedTime >= FadeDuration)
		{
			SetVisible(false);
			return;
		}

		var fadeRatio = fadeElapsedTime <= 0f ? 0f : Mathf.Clamp01(fadeElapsedTime / FadeDuration);
		_root.style.opacity = Mathf.Lerp(VisibleOpacity, 0f, fadeRatio);
	}

	private static VisualElement CreateElement(string className)
	{
		var element = new VisualElement
		{
			pickingMode = PickingMode.Ignore
		};
		element.AddToClassList(className);
		return element;
	}

	private static KeyElement CreateKey(CameraKeyOverlayInput input, string label, string extraClassName = null)
	{
		var key = new Label(label)
		{
			pickingMode = PickingMode.Ignore
		};
		key.AddToClassList("camera-key");

		if (!string.IsNullOrEmpty(extraClassName))
		{
			key.AddToClassList(extraClassName);
		}

		return new KeyElement(input, key);
	}

	private static void AddRow(VisualElement parent, params VisualElement[] keys)
	{
		var row = CreateElement("camera-key-overlay-row");
		foreach (var key in keys)
		{
			row.Add(key);
		}
		parent.Add(row);
	}

	private void SetVisible(bool value)
	{
		if (_isVisible == value)
		{
			return;
		}

		_isVisible = value;
		_root.style.display = value ? DisplayStyle.Flex : DisplayStyle.None;
		if (!value)
		{
			_root.style.opacity = 0f;
		}
	}

	private void SetActiveInputs(CameraKeyOverlayInput activeInputs)
	{
		if (_currentInputs == activeInputs)
		{
			return;
		}

		_currentInputs = activeInputs;
		foreach (var keyElement in _keyElements)
		{
			var isActive = (activeInputs & keyElement.Input) != CameraKeyOverlayInput.None;
			keyElement.Element.EnableInClassList(ActiveClassName, isActive);
		}
	}
}