/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using TMPro;

[DefaultExecutionOrder(45)]
public class FollowingTargetList : MonoBehaviour
{
	private TMP_Dropdown dropdown = null;
	private TMP_Dropdown.OptionData _emptyOption = null;

	private GameObject modelRoot = null;
	private FollowingCamera _followingCamera = null;

	void Awake()
	{
		modelRoot = Main.WorldRoot;
		dropdown = GetComponent<TMP_Dropdown>();
		if (Main.UIMainCanvas != null)
		{
			_followingCamera = Main.UIObject.GetComponentInChildren<FollowingCamera>();
		}
		else
		{
			Debug.LogError("Main.UIMainCanvas is not ready!!");
		}
	}

	void Start()
	{
		if (dropdown != null)
		{
			dropdown.onValueChanged.AddListener(OnDropDownValueChanged);

			_emptyOption = new TMP_Dropdown.OptionData("- unfollowing -");
			dropdown.options.Add(_emptyOption);
			SelectItem();
		}
	}

	public void SelectItem(in int selectIndex = 0)
	{
		if (dropdown != null)
		{
			dropdown.value = selectIndex;
			dropdown.Select();
			dropdown.RefreshShownValue();

			OnDropDownValueChanged(selectIndex);
		}
	}

	private void OnDropDownValueChanged(int choice)
	{
		var selected = dropdown.options[choice];
		var target = (choice > 0 && _followingCamera != null) ? selected.text : null;
		_followingCamera?.SetTargetObject(target);
	}

	public void UpdateList()
	{
		if (dropdown == null)
		{
			Debug.LogError("Dropdown is null!!");
			return;
		}

		var currentSelectedText = dropdown.options[dropdown.value].text;
		// Debug.Log("currentSelected: " + dropdown.value + ", " + currentSelectedText + " | " + dropdown.options.Count);

		dropdown.options.Clear();
		dropdown.options.Add(_emptyOption);

		if (modelRoot != null)
		{
			foreach (var modelHelper in modelRoot.GetComponentsInChildren<SDF.Helper.Model>())
			{
				if (modelHelper.IsFirstChild && !modelHelper.isStatic && modelHelper.hasRootArticulationBody)
				{
					var newOption = new TMP_Dropdown.OptionData();
					newOption.text = modelHelper.name;
					dropdown.options.Add(newOption);
				}
			}
		}

		// find selected model index by previous model name
		var selectedValue = 0;
		for (var i = 0; i < dropdown.options.Count; i++)
		{
			if (dropdown.options[i].text.CompareTo(currentSelectedText) == 0)
			{
				selectedValue = i;
				break;
			}
		}
		// Debug.Log("currentSelected: " + selectedValue + " | " + dropdown.options.Count);

		SelectItem(selectedValue);
	}
}
