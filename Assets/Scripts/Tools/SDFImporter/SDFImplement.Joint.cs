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
		public static UEJoint AddRevolute(in SDF.Axis axisInfo, in GameObject targetObject, in Rigidbody connectBody)
		{
			var hingeJointComponent = targetObject.AddComponent<HingeJoint>();
			hingeJointComponent.connectedBody = connectBody;
			hingeJointComponent.axis = SDF2Unity.GetAxis(axisInfo.xyz);
			hingeJointComponent.useMotor = false;

			var jointMotor = new JointMotor();
			jointMotor.targetVelocity = 0;
			jointMotor.force = 0;
			jointMotor.freeSpin = false;

			hingeJointComponent.motor = jointMotor;

			if (axisInfo.UseLimit())
			{
				var jointLimits = new JointLimits();
				jointLimits.min = (float)axisInfo.limit_lower * Mathf.Rad2Deg;
				jointLimits.max = (float)axisInfo.limit_upper * Mathf.Rad2Deg;
				hingeJointComponent.useLimits = true;
				hingeJointComponent.limits = jointLimits;
			}

			return hingeJointComponent;
		}

		public static UEJoint AddRevolute2(in SDF.Axis axisInfo1, in SDF.Axis axisInfo2, in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<ConfigurableJoint>();
			jointComponent.axis = SDF2Unity.GetAxis(axisInfo1.xyz);
			jointComponent.secondaryAxis = SDF2Unity.GetAxis(axisInfo2.xyz);

			// axisInfo1.limit_lower;
			// axisInfo1.limit_upper;

			// axisInfo2.limit_lower;
			// axisInfo2.limit_upper;

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

		public static UEJoint AddPrismatic(in SDF.Axis axisInfo, in SDF.OdePhysics physicsInfo, in SDF.Pose<double> pose, in GameObject targetObject, in Rigidbody connectBody)
		{
			var jointComponent = targetObject.AddComponent<ConfigurableJoint>();
			jointComponent.connectedBody = connectBody;
			jointComponent.secondaryAxis = Vector3.zero;
			jointComponent.axis = SDF2Unity.GetAxis(axisInfo.xyz, pose.Rot);

			var configurableJointMotion = ConfigurableJointMotion.Free;

			if (axisInfo.UseLimit())
			{
				// Debug.LogWarningFormat("limit uppper{0}, lower{1}", axisInfo.limit_upper, axisInfo.limit_lower);
				configurableJointMotion = ConfigurableJointMotion.Limited;
				var linearLimit = new SoftJointLimit();
				linearLimit.limit = (float)(axisInfo.limit_upper);

				jointComponent.linearLimit = linearLimit;
			}

			var linearLimitSpring = new SoftJointLimitSpring();
			linearLimitSpring.spring = 0;
			linearLimitSpring.damper = 0;
			jointComponent.linearLimitSpring = linearLimitSpring;

			var softJointLimit = new SoftJointLimit();
			softJointLimit.limit = (float)(axisInfo.limit_upper - axisInfo.limit_lower);
			softJointLimit.bounciness = 0.000f;
			softJointLimit.contactDistance = 0.0f;
			jointComponent.linearLimit = softJointLimit;

			var jointDrive = new JointDrive();
			jointDrive.positionSpring = (float)axisInfo.dynamics_spring_stiffness;
			jointDrive.positionDamper = (float)axisInfo.dynamics_damping;
			jointDrive.maximumForce = (float)physicsInfo.max_force;

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
