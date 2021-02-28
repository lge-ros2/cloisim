/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;
using Debug = UnityEngine.Debug;

namespace SDF
{
	namespace Import
	{
		public partial class Loader : Base
		{
			private static UE.Quaternion ToQuaternion(UE.Vector3 eigenvector0, UE.Vector3 eigenvector1, UE.Vector3 eigenvector2)
			{
				//From http://www.euclideanspace.com/maths/geometry/rotations/conversions/matrixToQuaternion/
				float tr = eigenvector0[0] + eigenvector1[1] + eigenvector2[2];
				float qw, qx, qy, qz;
				if (tr > 0)
				{
					float s = UE.Mathf.Sqrt(tr + 1.0f) * 2f; // S=4*qw
					qw = 0.25f * s;
					qx = (eigenvector1[2] - eigenvector2[1]) / s;
					qy = (eigenvector2[0] - eigenvector0[2]) / s;
					qz = (eigenvector0[1] - eigenvector1[0]) / s;
				}
				else if ((eigenvector0[0] > eigenvector1[1]) & (eigenvector0[0] > eigenvector2[2]))
				{
					float s = UE.Mathf.Sqrt(1.0f + eigenvector0[0] - eigenvector1[1] - eigenvector2[2]) * 2; // S=4*qx
					qw = (eigenvector1[2] - eigenvector2[1]) / s;
					qx = 0.25f * s;
					qy = (eigenvector1[0] + eigenvector0[1]) / s;
					qz = (eigenvector2[0] + eigenvector0[2]) / s;
				}
				else if (eigenvector1[1] > eigenvector2[2])
				{
					float s = UE.Mathf.Sqrt(1.0f + eigenvector1[1] - eigenvector0[0] - eigenvector2[2]) * 2; // S=4*qy
					qw = (eigenvector2[0] - eigenvector0[2]) / s;
					qx = (eigenvector1[0] + eigenvector0[1]) / s;
					qy = 0.25f * s;
					qz = (eigenvector2[1] + eigenvector1[2]) / s;
				}
				else
				{
					float s = UE.Mathf.Sqrt(1.0f + eigenvector2[2] - eigenvector0[0] - eigenvector1[1]) * 2; // S=4*qz
					qw = (eigenvector0[1] - eigenvector1[0]) / s;
					qx = (eigenvector2[0] + eigenvector0[2]) / s;
					qy = (eigenvector2[1] + eigenvector1[2]) / s;
					qz = 0.25f * s;
				}
				return new UE.Quaternion(qx, qy, qz, qw);
			}

			private UE.Pose GetInertiaTensor(in SDF.Inertial inertia)
			{
				 UE.Pose momentumInertiaTensor = UE.Pose.identity;
				// Debug.LogWarningFormat("GetInertiaTensor: {0}, {1}, {2}", inertia.ixx, inertia.iyy, inertia.izz);
				// var inertiaVector = new UE.Vector3((float)inertia.iyy, (float)inertia.izz, (float)inertia.ixx);
				var inertiaMatrix = new Matrix3x3(
																 (float)inertia.ixx, (float)inertia.ixy, (float)inertia.ixz,
																 (float)inertia.ixy, (float)inertia.iyy, (float)inertia.iyz,
																 (float)inertia.ixz, (float)inertia.iyz, (float)inertia.izz);

				inertiaMatrix.DiagonalizeRealSymmetric(out var eigenvalues, out var eigenvectors);

				// var inertiaVector = eigenvalues(inertia);
				var quaternion = ToQuaternion(eigenvectors[0], eigenvectors[1], eigenvectors[2]);

				momentumInertiaTensor.position = new UE.Vector3(eigenvalues.y, eigenvalues.z, eigenvalues.x);
				momentumInertiaTensor.rotation = new UE.Quaternion(quaternion.y, -quaternion.z, -quaternion.x, quaternion.w);

				const float minimumInertiaTensor = 1e-6f;
				for (var index = 0; index < 3; index++)
				{
					if (momentumInertiaTensor.position[index] <= minimumInertiaTensor)
					{
						momentumInertiaTensor.position[index] = minimumInertiaTensor;
					}
				}

				return momentumInertiaTensor;
			}

			protected override System.Object ImportLink(in SDF.Link link, in System.Object parentObject)
			{
				var targetObject = (parentObject as UE.GameObject);
				var newLinkObject = new UE.GameObject(link.Name);
				newLinkObject.tag = "Link";

				SetParentObject(newLinkObject, targetObject);

				var localPosition = SDF2Unity.GetPosition(link.Pose.Pos);
				var localRotation = SDF2Unity.GetRotation(link.Pose.Rot);

				var linkPlugin = newLinkObject.AddComponent<Helper.Link>();
				linkPlugin.isSelfCollide = link.SelfCollide;
				linkPlugin.SetPose(localPosition, localRotation);

				return newLinkObject as System.Object;
			}

			protected override void AfterImportLink(in SDF.Link link, in System.Object targetObject)
			{
				var linkObject = (targetObject as UE.GameObject);

				if (linkObject == null)
				{
					Debug.LogError("Link Object is null: " + link.Name);
					return;
				}

				// skip to create articulation body when mass is ZERO
				if (link.Inertial != null && link.Inertial.mass != 0)
				{
					var articulationBody = linkObject.AddComponent<UE.ArticulationBody>();

					foreach (var collider in linkObject.GetComponentsInChildren<UE.Collider>())
					{
						if (collider.attachedArticulationBody == null)
						{
							Debug.LogWarningFormat(linkObject.name + " > " + collider.name + " [=] null ArticulationBody ");
						}
					}

					articulationBody.velocity = UE.Vector3.zero;
					articulationBody.angularVelocity = UE.Vector3.zero;
					articulationBody.useGravity = (link.Kinematic)? false:link.Gravity;
					articulationBody.mass = (float)link.Inertial.mass;
					articulationBody.centerOfMass = SDF2Unity.GetPosition(link.Inertial.pose.Pos);
					articulationBody.jointType = UE.ArticulationJointType.FixedJoint;

					var childCollider = articulationBody.transform.GetComponentInChildren<UE.Collider>();
					if (childCollider != null && childCollider.transform.parent.Equals(articulationBody.transform))
					{
						articulationBody.ResetInertiaTensor();
						var momentum = GetInertiaTensor(link.Inertial);
						articulationBody.inertiaTensor = momentum.position;
						// articulationBody.inertiaTensorRotation = momentum.rotation;
						// articulationBody.inertiaTensorRotation = UE.Quaternion.Euler(0, 360, 0);
					}
					else
					{
						articulationBody.inertiaTensor = UE.Vector3.one * 1e-6f;
						articulationBody.inertiaTensorRotation = UE.Quaternion.identity;
					}

					// TODO: NOT Recommended to use innertia values from SDF
					// articulationBody.inertiaTensor = GetInertiaTensor(link.Inertial);
					// articulationBody.inertiaTensorRotation = Quaternion.identity;
					// Debug.Log(linkObject.name + "  => Center Of Mass: " + articulationBody.centerOfMass.ToString("F6") + ", intertia: " + articulationBody.inertiaTensor.ToString("F6") + ", " + articulationBody.inertiaTensorRotation.ToString("F6"));
					// Debug.Log("Create link body " + linkObject.name);
				}
				else
				{
					// If the child does not have articulation body, collider of child would disable convex.
					// Sholud be handled after set parent object!!
					var meshColliders = linkObject.GetComponentsInChildren<UE.MeshCollider>();
					foreach (var meshCollider in meshColliders)
					{
						meshCollider.convex = false;
						// Debug.LogWarning("Make convex false:" + meshCollider.name);
					}
				}
			}
		}
	}
}