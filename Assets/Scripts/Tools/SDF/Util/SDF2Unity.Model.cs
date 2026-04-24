/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public static partial class SDF2Unity
{
	public static (string, string) GetModelLinkName(in string value, in string defaultModelName = "__default__")
	{
		var modelName = defaultModelName;
		var linkName = value;

		if (value.Contains("::"))
		{
			var splittedNamePart = value.Split("::", System.StringSplitOptions.RemoveEmptyEntries);
			modelName = splittedNamePart[0];
			linkName = splittedNamePart[1];
		}

		return (modelName, linkName);
	}

	public static bool IsRootModel(this GameObject targetObject)
	{
		return targetObject.transform.IsRootModel();
	}

	public static bool IsRootModel(this Transform targetTransform)
	{
		return (targetTransform.parent == null) ?
			false : targetTransform.parent.Equals(targetTransform.root);
	}
}