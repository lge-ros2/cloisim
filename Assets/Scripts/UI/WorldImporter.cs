/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

public class WorldImporter : MonoBehaviour
{
	private VisualElement _worldListOverlay = null;
	private ScrollView _worldListContent = null;

	void Awake()
	{
		var rootVisualElement = Main.UIController.RootVisualElement;
		_worldListOverlay = rootVisualElement.Q<VisualElement>("WorldListOverlay");
		_worldListContent = rootVisualElement.Q<ScrollView>("WorldListContent");
	}

	public void ShowWorldList(in bool open)
	{
		_worldListOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
		Main.CameraControl.BlockMouseWheelControl(open);
	}

	public void UpdateUIWorldList(in SDFormat.ResourceWorldTable resourceWorldTable)
	{
		if (_worldListContent == null)
		{
			Debug.LogWarning("_worldListContent is null");
			return;
		}

		_worldListContent.Clear();

		foreach (var item in resourceWorldTable)
		{
			var itemKey = item.Key;
			var itemValue = item.Value;
			var button = new Button(() =>
			{
				StartCoroutine(Main.Instance.LoadWorld(itemValue.filename));
				ShowWorldList(false);
			})
			{
				text = itemKey
			};
			button.AddToClassList("world-list-button");
			_worldListContent.Add(button);
		}
	}

	void LateUpdate()
	{
		if (_worldListOverlay.style.display == DisplayStyle.Flex && Keyboard.current[Key.Escape].wasReleasedThisFrame)
		{
			ShowWorldList(false);
		}
	}
}
