/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	public partial class Implement
	{
		public class Joint
		{
			public static UE.Joint AddRevolute(in SDF.Axis axisInfo, in UE.GameObject targetObject, in UE.Rigidbody connectBody)
			{
				var hingeJointComponent = targetObject.AddComponent<UE.HingeJoint>();
				hingeJointComponent.connectedBody = connectBody;
				hingeJointComponent.axis = SDF2Unity.GetAxis(axisInfo.xyz);
				hingeJointComponent.useMotor = false;

				var jointMotor = new UE.JointMotor();
				jointMotor.targetVelocity = 0;
				jointMotor.force = 0;
				jointMotor.freeSpin = false;

				hingeJointComponent.motor = jointMotor;

				if (axisInfo.UseLimit())
				{
					var jointLimits = new UE.JointLimits();
					jointLimits.min = -(float)axisInfo.limit_upper * UE.Mathf.Rad2Deg;
					jointLimits.max = -(float)axisInfo.limit_lower * UE.Mathf.Rad2Deg;
					hingeJointComponent.useLimits = true;
					hingeJointComponent.limits = jointLimits;
				}

				return hingeJointComponent;
			}

			public static UE.Joint AddRevolute2(in SDF.Axis axisInfo1, in SDF.Axis axisInfo2, in UE.GameObject targetObject, in UE.Rigidbody connectBody)
			{
				var jointComponent = targetObject.AddComponent<UE.ConfigurableJoint>();
				jointComponent.axis = SDF2Unity.GetAxis(axisInfo1.xyz);
				jointComponent.secondaryAxis = SDF2Unity.GetAxis(axisInfo2.xyz);

				// axisInfo1.limit_lower;
				// axisInfo1.limit_upper;

				// axisInfo2.limit_lower;
				// axisInfo2.limit_upper;

				jointComponent.projectionMode = UE.JointProjectionMode.PositionAndRotation;

				return jointComponent;
			}

			public static UE.Joint AddFixed(in UE.GameObject targetObject, in UE.Rigidbody connectBody)
			{
				var jointComponent = targetObject.AddComponent<UE.FixedJoint>();
				jointComponent.connectedBody = connectBody;
				return jointComponent;
			}

			public static UE.Joint AddBall(in UE.GameObject targetObject, in UE.Rigidbody connectBody)
			{
				var jointComponent = targetObject.AddComponent<UE.ConfigurableJoint>();
				jointComponent.connectedBody = connectBody;
				jointComponent.axis = new UE.Vector3(1, 0, 0);
				jointComponent.secondaryAxis = new UE.Vector3(0, 1, 0);

				var configurableJointMotion = UE.ConfigurableJointMotion.Free;

				jointComponent.xMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.yMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.zMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.angularXMotion = configurableJointMotion;
				jointComponent.angularYMotion = configurableJointMotion;
				jointComponent.angularZMotion = configurableJointMotion;

				var zeroJointDriver = new UE.JointDrive();
				zeroJointDriver.positionSpring = 0;
				zeroJointDriver.positionDamper = 0;
				zeroJointDriver.maximumForce = 0;

				jointComponent.xDrive = zeroJointDriver;
				jointComponent.yDrive = zeroJointDriver;
				jointComponent.zDrive = zeroJointDriver;

				jointComponent.projectionMode = UE.JointProjectionMode.PositionAndRotation;

				return jointComponent;
			}

			public static UE.Joint AddPrismatic(in SDF.Axis axisInfo, in SDF.OdePhysics physicsInfo, in SDF.Pose<double> pose, in UE.GameObject targetObject, in UE.Rigidbody connectBody)
			{
				var jointComponent = targetObject.AddComponent<UE.ConfigurableJoint>();
				jointComponent.connectedBody = connectBody;
				jointComponent.secondaryAxis = UE.Vector3.zero;
				jointComponent.axis = SDF2Unity.GetAxis(axisInfo.xyz, pose.Rot);

				var configurableJointMotion = UE.ConfigurableJointMotion.Free;

				if (axisInfo.UseLimit())
				{
					// Debug.LogWarningFormat("limit uppper{0}, lower{1}", axisInfo.limit_upper, axisInfo.limit_lower);
					configurableJointMotion = UE.ConfigurableJointMotion.Limited;
					var linearLimit = new UE.SoftJointLimit();
					linearLimit.limit = (float)(axisInfo.limit_upper);

					jointComponent.linearLimit = linearLimit;
				}

				var linearLimitSpring = new UE.SoftJointLimitSpring();
				linearLimitSpring.spring = 0;
				linearLimitSpring.damper = 0;
				jointComponent.linearLimitSpring = linearLimitSpring;

				var softJointLimit = new UE.SoftJointLimit();
				softJointLimit.limit = (float)(axisInfo.limit_upper - axisInfo.limit_lower);
				softJointLimit.bounciness = 0.000f;
				softJointLimit.contactDistance = 0.0f;
				jointComponent.linearLimit = softJointLimit;

				var jointDrive = new UE.JointDrive();
				jointDrive.positionSpring = (float)axisInfo.dynamics_spring_stiffness;
				jointDrive.positionDamper = (float)axisInfo.dynamics_damping;
				jointDrive.maximumForce = (float)physicsInfo.max_force;

				var zeroJointDriver = new UE.JointDrive();
				zeroJointDriver.positionSpring = 0;
				zeroJointDriver.positionDamper = 0;
				zeroJointDriver.maximumForce = 0;

				// joint's local x axis...
				jointComponent.xMotion = configurableJointMotion;
				jointComponent.xDrive = jointDrive;
				jointComponent.yMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.yDrive = zeroJointDriver;
				jointComponent.zMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.zDrive = zeroJointDriver;

				jointComponent.angularXMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.angularYMotion = UE.ConfigurableJointMotion.Locked;
				jointComponent.angularZMotion = UE.ConfigurableJointMotion.Locked;

				jointComponent.angularXDrive = zeroJointDriver;
				jointComponent.angularYZDrive = zeroJointDriver;

				jointComponent.projectionMode = UE.JointProjectionMode.PositionAndRotation;

				return jointComponent;
			}

			public static void SetCommonConfiguration(UE.Joint jointComponent, in SDF.Vector3<double> jointPosition, in UE.GameObject linkObject)
			{
				var linkTransform = linkObject.transform;

				jointComponent.anchor = SDF2Unity.GetPosition(jointPosition);
				jointComponent.autoConfigureConnectedAnchor = true;

				jointComponent.enableCollision = false;
				jointComponent.enablePreprocessing = true;

				jointComponent.breakForce = UE.Mathf.Infinity;
				jointComponent.breakTorque = UE.Mathf.Infinity;
			}
		}
	}
}