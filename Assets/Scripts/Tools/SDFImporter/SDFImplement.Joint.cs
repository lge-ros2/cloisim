/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System;
using UnityEngine;

using UEJoint = UnityEngine.Joint;

public partial class SDFImplement
{
	public class Joint
	{
		private static Vector3 GetAxis(SDF.Vector3<int> value, SDF.Quaternion<double> rot = null)
		{
			var pos = new Vector3();
			pos.x = -value.X;
			pos.y = -value.Z;
			pos.z = -value.Y;

			if (rot != null)
			{
				if (pos.x != 0)
				{
					pos.y = pos.x * Mathf.Sin((float)rot.Pitch);
					pos.z = pos.x * Mathf.Sin((float)rot.Yaw);
				}
				else if (pos.y != 0)
				{
					pos.x = pos.y * Mathf.Sin((float)rot.Roll);
					pos.z = pos.y * Mathf.Sin((float)rot.Yaw);
				}
				else if (pos.z != 0)
				{
					pos.x = pos.z * Mathf.Sin((float)rot.Roll);
					pos.y = pos.z * Mathf.Sin((float)rot.Pitch);
				}
			}
			return pos;
		}

		public static UEJoint AddRevolute(in SDF.Axis jointInfo, in GameObject targetObject, in Rigidbody connectBody)
		{
			var hingeJointComponent = targetObject.AddComponent<HingeJoint>();
			hingeJointComponent.connectedBody = connectBody;
			hingeJointComponent.axis = GetAxis(jointInfo.xyz);
			hingeJointComponent.useMotor = false;

			var jointMotor = new JointMotor();
			jointMotor.targetVelocity = 0;
			jointMotor.force = 0;
			jointMotor.freeSpin = false;

			hingeJointComponent.motor = jointMotor;

			if (jointInfo.UseLimit())
			{
				var jointLimits = new JointLimits();
				jointLimits.min = (float)jointInfo.limit_lower * Mathf.Rad2Deg;
				jointLimits.max = (float)jointInfo.limit_upper * Mathf.Rad2Deg;
				hingeJointComponent.useLimits = true;
				hingeJointComponent.limits = jointLimits;
			}

			return hingeJointComponent;
		}

		public static UEJoint AddRevolute2(in SDF.Axis jointInfo1, in SDF.Axis jointInfo2, in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<ConfigurableJoint>();

			var confJointComponent = targetObject.AddComponent<ConfigurableJoint>();
			confJointComponent.axis = GetAxis(jointInfo1.xyz);
			confJointComponent.secondaryAxis = GetAxis(jointInfo2.xyz);

			// jointInfo1.limit_lower;
			// jointInfo1.limit_upper;

			// jointInfo2.limit_lower;
			// jointInfo2.limit_upper;

			return jointComponent;
		}

		public static UEJoint AddFixed(in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<FixedJoint>();
			jointComponent.connectedBody = connectBody;
			return jointComponent;
		}

		public static UEJoint AddBall(in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<ConfigurableJoint>();
			jointComponent.connectedBody = connectBody;
			jointComponent.axis = new Vector3(1, 0, 0);
			jointComponent.secondaryAxis = new Vector3(0, 1, 0);

			var configurableJointMotion = ConfigurableJointMotion.Free;

			jointComponent.xMotion = ConfigurableJointMotion.Locked;
			jointComponent.yMotion = ConfigurableJointMotion.Locked;
			jointComponent.zMotion = ConfigurableJointMotion.Locked;
			jointComponent.angularXMotion = configurableJointMotion;
			jointComponent.angularYMotion = configurableJointMotion;
			jointComponent.angularZMotion = configurableJointMotion;

			var zeroJointDriver = new JointDrive();
			zeroJointDriver.positionSpring = 0;
			zeroJointDriver.positionDamper = 0;
			zeroJointDriver.maximumForce = 0;

			jointComponent.xDrive = zeroJointDriver;
			jointComponent.yDrive = zeroJointDriver;
			jointComponent.zDrive = zeroJointDriver;

			return jointComponent;
		}

		public static UEJoint AddPrismatic(in SDF.Axis jointInfo, in SDF.Pose<double> pose, in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<ConfigurableJoint>();
			jointComponent.connectedBody = connectBody;
			jointComponent.secondaryAxis = Vector3.zero;
			jointComponent.axis = GetAxis(jointInfo.xyz, pose.Rot);

			var configurableJointMotion = ConfigurableJointMotion.Free;

			if (jointInfo.UseLimit())
			{
				// Debug.LogWarningFormat("limit uppper{0}, lower{1}", jointInfo.limit_upper, jointInfo.limit_lower);
				configurableJointMotion = ConfigurableJointMotion.Limited;
				var linearLimit = new SoftJointLimit();
				linearLimit.limit = (float)(jointInfo.limit_upper);

				jointComponent.linearLimit = linearLimit;
			}

			var linearLimitSpring = new SoftJointLimitSpring();
			linearLimitSpring.spring = 0;
			linearLimitSpring.damper = 0;
			jointComponent.linearLimitSpring = linearLimitSpring;

			var softJointLimit = new SoftJointLimit();
			softJointLimit.limit = (float)(jointInfo.limit_upper - jointInfo.limit_lower);
			softJointLimit.bounciness = 0.000f;
			softJointLimit.contactDistance = 0.0f;
			jointComponent.linearLimit = softJointLimit;

			var jointDrive = new JointDrive();
			jointDrive.positionSpring = (float)(jointInfo.dynamics_spring_stiffness);
			jointDrive.positionDamper = (float)(jointInfo.dynamics_damping);
			jointDrive.maximumForce = Mathf.Infinity; // 3.402823e+38

			var zeroJointDriver = new JointDrive();
			zeroJointDriver.positionSpring = 0;
			zeroJointDriver.positionDamper = 0;
			zeroJointDriver.maximumForce = 0;

			// joint's local x axis...
			jointComponent.xMotion = configurableJointMotion;
			jointComponent.xDrive = jointDrive;
			jointComponent.yMotion = ConfigurableJointMotion.Locked;
			jointComponent.yDrive = zeroJointDriver;
			jointComponent.zMotion = ConfigurableJointMotion.Locked;
			jointComponent.zDrive = zeroJointDriver;

			jointComponent.angularXMotion = ConfigurableJointMotion.Locked;
			jointComponent.angularYMotion = ConfigurableJointMotion.Locked;
			jointComponent.angularZMotion = ConfigurableJointMotion.Locked;

			jointComponent.angularXDrive = zeroJointDriver;
			jointComponent.angularYZDrive = zeroJointDriver;

			return jointComponent;
		}

		public static void SetCommonConfiguration(UEJoint jointComponent, in SDF.Vector3<double> jointPosition,  in GameObject linkObject)
		{
			var linkTransform = linkObject.transform;

			jointComponent.anchor = SDF2Unity.GetPosition(jointPosition);
			jointComponent.autoConfigureConnectedAnchor = false;

			if (jointComponent.autoConfigureConnectedAnchor == false)
			{
				var connectedAnchor = Vector3.zero;
				var connectedBody = jointComponent.connectedBody;

				var modelObject = linkTransform.parent;
				var childRigidBodies = modelObject.GetComponentsInChildren<Rigidbody>(false);

				// if linkObject is connected to the other object(sibling) which is in modelObject(parent),
				// use the current position of linkObject itself.
				if (Array.Exists(childRigidBodies, element => element == connectedBody))
				{
					// Debug.Log("Sibling connected!!!: " + linkTransform.name);
					connectedAnchor = SDF2Unity.GetPosition(jointPosition) + linkTransform.localPosition;
				}
				else
				{
					var finalAnchor = modelObject.transform.localPosition;

					var targetConnectedBody = connectedBody;
					while (targetConnectedBody != null)
					{
						var targetModelObject = targetConnectedBody.transform.parent;

						// check whether targetModelObject is top or not
						if (SDF2Unity.CheckTopModel(targetModelObject))
						{
							break;
						}
						else
						{
							var targetConnectedBodyTransform = targetModelObject.localPosition;
							finalAnchor -= targetConnectedBodyTransform;

							if (targetConnectedBody.gameObject.TryGetComponent<UEJoint>(out var joint))
							{
								targetConnectedBody = joint.connectedBody;
							}
							else
							{
								break;
							}
						}
					}

					connectedAnchor = finalAnchor;
				}

				jointComponent.connectedAnchor = connectedAnchor;
			}

			jointComponent.enableCollision = false;
			jointComponent.enablePreprocessing = true;

			jointComponent.breakForce = Mathf.Infinity;
			jointComponent.breakTorque = Mathf.Infinity;
		}
	}
}
