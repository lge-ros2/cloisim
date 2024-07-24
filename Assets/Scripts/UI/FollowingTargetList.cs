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

			_emptyOption = new TMP_Dropdown.OptionData("UNFOLLOWING");
			dropdown.options.Add(_emptyOption);
			SelectItem(0);
		}
	}

	private void SelectItem(in int selectIndex)
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

	void LateUpdate()
	{
		if (Input.GetKey(KeyCode.LeftControl))
		{
		 	if (Input.GetKeyUp(KeyCode.F))
			{
				if (Input.GetKey(KeyCode.LeftShift))
				{
					SelectItem(0);
				}
				else
				{
					Main.Gizmos.GetSelectedTargets(out var objectListForFollowing);

					if (objectListForFollowing.Count > 0)
					{
						if (objectListForFollowing.Count > 1)
						{
							Main.UIController?.SetWarningMessage("Multiple Object is selected. Only single object can be followed.");
						}

						foreach (var target in objectListForFollowing)
						{
							var articulationBody = target.GetComponent<ArticulationBody>();
							if (articulationBody != null && articulationBody.isRoot)
							{
								var selectedObjectName = target.gameObject.name;
								var selectedIndex = FindItemIndex(selectedObjectName);
								SelectItem(selectedIndex);
								Main.Gizmos.ClearTargets();
								break;
							}
						}
					}
				}
			}
		}
	}

	private int FindItemIndex(in string name)
	{
		foreach (var option in dropdown.options)
		{
			if (option.text.CompareTo(name) == 0)
			{
				return dropdown.options.IndexOf(option);
			}
		}
		return 0;
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
