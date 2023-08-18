/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using System.Collections.Generic;

namespace SDF
{
	public class State
	{
		public class Time
		{
			public uint seconds = 0;
			public uint nanoseconds = 0;
		}

		public class Insertions
		{
			// Description: The model element defines a complete robot or any other physical object.
			public Models models = null;

			// Description: The light element describes a light source.
			public List<Light> lights = null;
		}

		// Description: A list of names of deleted entities/
		public class Deletions
		{
			// Description: The name of a deleted entity.
			// Default: __default__
			public List<string> names = null;
		}

		// Description: Model state
		public class ModelState
		{
			// Description: Joint angle
			public class JointState
			{
				// Description: Name of the joint
				public string name = "__default__";

				public class JointStateAngle
				{
					// Description: Angle of an axis
					public double angle = 0;

					// Description: Index of the axis.
					public uint axis = 0;
				}

				public List<JointStateAngle> angles = null;
			}

			// Description: A frame of reference in which poses may be expressed.
			public class Frame
			{
				// Description: Name of the frame. It must be unique whithin its scope (model/world), i.e., it must not match the name of another frame, link, joint, or model within the same scope.
				public string name = string.Empty;

				// Description: If specified, this frame is attached to the specified frame. The specified frame must be within the same scope and may be defined implicitly, i.e., the name of any //frame, //model, //joint, or //link within the same scope may be used. If missing, this frame is attached to the containing scope's frame. Within a //world scope this is the implicit world frame, and within a //model scope this is the implicit model frame. A frame moves jointly with the frame it is @attached_to. This is different from //pose/@relative_to. @attached_to defines how the frame is attached to a //link, //model, or //world frame, while //pose/@relative_to defines how the frame's pose is represented numerically. As a result, following the chain of @attached_to attributes must always lead to a //link, //model, //world, or //joint (implicitly attached_to its child //link).
				public string attached_to = string.Empty;

				public Pose<double> pose = new Pose<double>();
			}

			// Description: Link state
			public class LinkState
			{
				// Description: Collision state
				public class CollisionState
				{
					// Description: Name of the collision
					// string name = "__default__";
				}

				// Description: Name of the link
				// string name = "__default__";

				// Description: Velocity of the link. The x, y, z components of the pose correspond to the linear velocity of the link, and the roll, pitch, yaw components correspond to the angular velocity of the link
				public Pose<double> velocity = null;

				// Description: Acceleration of the link. The x, y, z components of the pose correspond to the linear acceleration of the link, and the roll, pitch, yaw components correspond to the angular acceleration of the link
				public Pose<double> acceleration = null;

				// Description: Force and torque applied to the link. The x, y, z components of the pose correspond to the force applied to the link, and the roll, pitch, yaw components correspond to the torque applied to the link
				public Pose<double> wrench = null;

				public List<CollisionState> collisions = null;

				public Pose<double> pose = new Pose<double>();
			}


			// Description: Name of the model
			public string name = "__default__";

			public List<JointState> joints = null;

			// Description: A nested model state element
			public Models models = null;

			// Description: Scale for the 3 dimensions of the model.
			public Vector3<double> scale = new Vector3<double>(1, 1, 1);

			public List<Frame> frames = null;

			public Pose<double> pose = new Pose<double>();

			public List<LinkState> links = null;
		}

		public class LightState
		{
			// Description: Name of the light
			public string name = "__default__";
			public Pose<double> pose = null;
		}

		// Description: Name of the world this state applies to
		public string world_name = "__default__";

		// Description: Simulation time stamp of the state [seconds nanoseconds]
		public Time sim_time = new Time();


		// Description: Wall time stamp of the state [seconds nanoseconds]
		public Time wall_time = new Time();

		// Description: Real time stamp of the state [seconds nanoseconds]
		public Time real_time = new Time();

		// Description: Number of simulation iterations.
		public uint iterations = 0;

		// Description: A list containing the entire description of entities inserted.
		public Insertions insertions = null;

		public Deletions deletions = null;

		public List<ModelState> models = null;

		public List<LightState> lights = null;
	}
}