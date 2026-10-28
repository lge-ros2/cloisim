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
	private VisualElement _worldConfirmOverlay = null;
	private Label _worldConfirmDetail = null;
	private string _pendingWorldFilename = null;

	void Awake()
	{
		var rootVisualElement = Main.UIController.RootVisualElement;
		_worldListOverlay = rootVisualElement.Q<VisualElement>("WorldListOverlay");
		_worldListContent = rootVisualElement.Q<ScrollView>("WorldListContent");

		_worldConfirmOverlay = rootVisualElement.Q<VisualElement>("WorldConfirmOverlay");
		_worldConfirmDetail = rootVisualElement.Q<Label>("WorldConfirmDetail");
		rootVisualElement.Q<Button>("WorldConfirmYes").clickable.clicked += ConfirmWorld;
		rootVisualElement.Q<Button>("WorldConfirmNo").clickable.clicked += () => ShowWorldConfirm(false);
	}

	public void ShowWorldList(in bool open)
	{
		_worldListOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
		Main.CameraControl.BlockMouseWheelControl(open);

		if (!open)
		{
			ShowWorldConfirm(false);
		}
	}

	private void ShowWorldConfirm(in bool open)
	{
		_worldConfirmOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;
	}

	private void SelectWorld(in string worldName, in string worldFilename)
	{
		if (!Main.Instance.IsWorldLoaded)
		{
			StartCoroutine(Main.Instance.LoadWorld(worldFilename));
			ShowWorldList(false);
			return;
		}

		_pendingWorldFilename = worldFilename;
		_worldConfirmDetail.text = $"Load '{worldName}'?\nThe current world will be closed.";
		ShowWorldConfirm(true);
	}

	private void ConfirmWorld()
	{
		var worldFilename = _pendingWorldFilename;
		_pendingWorldFilename = null;
		ShowWorldList(false);

		if (!string.IsNullOrEmpty(worldFilename))
		{
			Main.Instance.RestartWithWorld(worldFilename);
		}
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
			var button = new Button(() => SelectWorld(itemKey, itemValue.filename))
			{
				text = itemKey
			};
			button.AddToClassList("world-list-button");
			_worldListContent.Add(button);
		}
	}

	void LateUpdate()
	{
		if (Keyboard.current[Key.Escape].wasReleasedThisFrame)
		{
			if (_worldConfirmOverlay.style.display == DisplayStyle.Flex)
			{
				ShowWorldConfirm(false);
			}
			else if (_worldListOverlay.style.display == DisplayStyle.Flex)
			{
				ShowWorldList(false);
			}
		}
	}
}
