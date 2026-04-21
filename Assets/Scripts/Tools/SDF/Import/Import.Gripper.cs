/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDFormat
{
	using Implement;

	namespace Import
	{
		public partial class Loader : Base
		{
			protected override void ImportGripper(in Gripper gripper, in System.Object parentObject)
			{
				var modelObject = parentObject as UE.GameObject;

				if (string.IsNullOrEmpty(gripper.PalmLink))
				{
					Debug.LogWarning($"[Gripper:{gripper.Name}] PalmLink is not specified — skipping.");
					return;
				}

				var palmTransform = modelObject.FindTransformByName(gripper.PalmLink);
				if (palmTransform == null)
				{
					Debug.LogWarning($"[Gripper:{gripper.Name}] PalmLink '{gripper.PalmLink}' not found under '{modelObject.name}'.");
					return;
				}

				if (gripper.GripperLinks.Count == 0)
				{
					Debug.LogWarning($"[Gripper:{gripper.Name}] No gripper_links defined — skipping.");
					return;
				}

				var gripperHelper = modelObject.AddComponent<Helper.Gripper>();
				gripperHelper.PalmLink = palmTransform;
				gripperHelper.MinContactCount = gripper.GraspCheckMinContactCount;
				gripperHelper.AttachSteps = gripper.GraspCheckAttachSteps;
				// DetachSteps defaults to 2× AttachSteps; not part of parsed SDF data
				gripperHelper.DetachSteps = gripper.GraspCheckAttachSteps * 2;

				for (var i = 0; i < gripper.GripperLinks.Count; i++)
				{
					var linkName = gripper.GripperLinks[i];
					var linkTransform = modelObject.FindTransformByName(linkName);
					if (linkTransform == null)
					{
						Debug.LogWarning($"[Gripper:{gripper.Name}] GripperLink '{linkName}' not found under '{modelObject.name}'.");
						continue;
					}

					gripperHelper.AddGripperLink(linkTransform);

					var contact = linkTransform.gameObject.AddComponent<Helper.GripperLinkContact>();
					contact.Initialize(gripperHelper, i);
				}

				Debug.Log($"[Gripper:{gripper.Name}] Imported — palm='{gripper.PalmLink}' links={gripper.GripperLinks.Count} minContact={gripper.GraspCheckMinContactCount} attachSteps={gripper.GraspCheckAttachSteps}");
			}
		}
	}
}
