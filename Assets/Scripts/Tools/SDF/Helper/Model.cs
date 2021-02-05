/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UE = UnityEngine;

namespace SDF
{
	namespace Helper
	{
		public class Model : Base
		{
			public bool isTopModel;

			[UE.Header("SDF Properties")]
			public bool isStatic = false;

			new void Awake()
			{
				base.Awake();

				isTopModel = SDF2Unity.CheckTopModel(transform);
			}

			void Start()
			{
				if (isTopModel)
				{
					FindAndMakeBridgeJoint();
				}
			}

			public Model GetThisInTopParent()
			{
				var modelHelpers = GetComponentsInParent(typeof(Model));
				return (Model)modelHelpers[modelHelpers.Length - 1];
			}

			public Link[] GetLinksInChildren()
			{
				return GetComponentsInChildren<Link>();
			}

			private bool MakeBridgeJoint(UE.Rigidbody targetRigidBody)
			{
				if (GetComponent<UE.Rigidbody>() != null)
				{
					return false;
				}

				// Configure rigidbody for root object
				var rigidBody = gameObject.AddComponent<UE.Rigidbody>();
				rigidBody.mass = 0.00000001f;
				rigidBody.drag = 0;
				rigidBody.angularDrag = 0;
				rigidBody.useGravity = false;
				rigidBody.isKinematic = false;
				rigidBody.ResetCenterOfMass();
				rigidBody.ResetInertiaTensor();
				rigidBody.inertiaTensor = new UE.Vector3(0.000001f, 0.000001f, 0.000001f);

				var fixedJoint = gameObject.AddComponent<UE.FixedJoint>();
				fixedJoint.connectedBody = targetRigidBody;
				fixedJoint.enableCollision = false;
				fixedJoint.enablePreprocessing = false;
				fixedJoint.massScale = 1;
				fixedJoint.connectedMassScale = 1;

				return true;
			}

			private void FindAndMakeBridgeJoint()
			{
				var rigidBodyChildren = GetComponentsInChildren<UE.Rigidbody>();
				foreach (var rigidBodyChild in rigidBodyChildren)
				{
					// Get child component in only first depth!!!
					// And make bridge joint
					if (rigidBodyChild != null && rigidBodyChild.transform.parent == this.transform)
					{
						if (MakeBridgeJoint(rigidBodyChild) == true)
						{
							break;
						}
					}
				}
			}
		}
	}
}