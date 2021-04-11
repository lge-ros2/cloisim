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
	private TMP_Dropdown.OptionData emptyOption_ = null;


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

			emptyOption_ = new TMP_Dropdown.OptionData("-- unfollowing --");
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

	public void UpdateList(in int selectIndex = 0)
	{
		if (dropdown == null)
		{
			Debug.LogError("Dropdown is null!!");
			return;
		}

		dropdown.options.Clear();
		dropdown.options.Add(emptyOption_);

		if (modelRoot != null)
		{
			foreach (var modelHelper in modelRoot.GetComponentsInChildren<SDF.Helper.Model>())
			{
				if (modelHelper.isTopModel && !modelHelper.isStatic && modelHelper.hasRootArticulationBody)
				{
					var newOption = new TMP_Dropdown.OptionData();
					newOption.text = modelHelper.name;
					dropdown.options.Add(newOption);
				}
			}
		}

		dropdown.value = selectIndex;
		dropdown.Select();
		dropdown.RefreshShownValue();
	}
}
