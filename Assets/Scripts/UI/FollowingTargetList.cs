/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
 
using UnityEngine;
using TMPro;

public class FollowingTargetList : MonoBehaviour
{
	private TMP_Dropdown dropdown = null;

	private GameObject modelRoot = null;

	private FollowingCamera followingCamera = null;


	void Awake()
	{
		modelRoot = GameObject.Find("Models");
		dropdown = GetComponent<TMP_Dropdown>();
		followingCamera = gameObject.transform.root.GetComponentInChildren<FollowingCamera>();
	}

	void Start()
	{
		if (dropdown != null)
		{
			dropdown.onValueChanged.AddListener(OnDropDownValueChanged);
		}
	}

	private void OnDropDownValueChanged(int choice)
	{
		var selected = dropdown.options[choice];

		if (choice > 0 && followingCamera != null)
		{
			followingCamera.SetTargetObject(selected.text);
		}
		else
		{
			followingCamera.SetTargetObject(null);
		}
	}

	public void UpdateList()
	{
		if (dropdown == null)
		{
			Debug.LogError("Dropdown is null!!");
			return;
		}

		dropdown.options.Clear();
		var emptyOption = new TMP_Dropdown.OptionData() { text = "-- unfollowing --" };
		dropdown.options.Add(emptyOption);

		if (modelRoot != null)
		{
			foreach (var modelPlugin in modelRoot.GetComponentsInChildren<ModelPlugin>())
			{
				if (modelPlugin.IsTopModel && !(modelPlugin.isStatic))
				{
					var newOption = new TMP_Dropdown.OptionData();
					newOption.text = modelPlugin.name;
					dropdown.options.Add(newOption);
				}
			}
		}

		dropdown.value = 0;
		dropdown.Select();
		dropdown.RefreshShownValue();
	}
}
