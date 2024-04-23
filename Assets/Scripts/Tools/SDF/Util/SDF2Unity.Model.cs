/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;

public partial class SDF2Unity
{
	public static (string, string) GetModelLinkName(in string value, in string defaultModelName = "")
	{
		var modelName = defaultModelName;
		var linkName = value;

		if (value.Contains("::"))
		{
			var splittedName = value.Replace("::", ":").Split(':');
			modelName = splittedName[0];
			linkName = splittedName[1];
		}

		return (modelName, linkName);
	}

	public static bool IsRootModel(in GameObject targetObject)
	{
		return IsRootModel(targetObject.transform);
	}

	public static bool IsRootModel(in Transform targetTransform)
	{
		return (targetTransform.parent == null) ?
			false : (targetTransform.parent.Equals(targetTransform.root));
	}
}