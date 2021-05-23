/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using System.Xml;
using System;

namespace SDF
{
	public class Actors : Entities<Actor>
	{
		private const string TARGET_TAG = "actor";
		public Actors() : base(TARGET_TAG) {}
		public Actors(XmlNode _node) : base(_node, TARGET_TAG) {}
	}

	// A special kind of model which can have a scripted motion. This includes both global waypoint type animations and skeleton animations.
	public class Actor : Entity
	{
		//	Description: Skin file which defines a visual and the underlying skeleton which moves it.
		public class Skin
		{
			// Description: Path to skin file, accepted formats: COLLADA, BVH.
			public string filename = "__default__";

			// Description: Scale the skin's size.
			public double scale = 1;
		}

		// Description: Animation file defines an animation for the skeleton in the skin. The skeleton must be compatible with the skin skeleton.
		public class Animation: Entity
		{
			public Animation(in XmlNode _node)
				: base(_node)
			{
				name = GetAttribute<string>("name");
				filename = GetValue<string>("filename");
				if (IsValidNode("scale"))
				{
					scale = GetValue<double>("scale");
				}
				interpolate_x = GetValue<bool>("interpolate_x");
			}

			// Description: Unique name for animation.
			public string name = "__default__";

			// Description: Path to animation file. Accepted formats: COLLADA, BVH.
			public string filename = "__default__";

			// Description: Scale for the animation skeleton.
			public double scale = 1;

			// Description: Set to true so the animation is interpolated on X.
			public bool interpolate_x = false;
		}

		// Description: Adds scripted trajectories to the actor.
		public class Script
		{
			// Description: The trajectory contains a series of keyframes to be followed.
			public class Trajectory : Entity
			{
				public Trajectory(in XmlNode _node)
					: base(_node)
				{
					id = GetAttribute<int>("id");
					tension = GetAttribute<double>("tension");

					var nodeList = GetNodes("waypoint");
					foreach (XmlNode nodeItem in nodeList)
					{
						var waypoint = new Trajectory.Waypoint(nodeItem);
						waypoints.Add(waypoint);
					}

					waypoints.Sort((waypointA, waypointB) => waypointA.time.CompareTo(waypointB.time));
				}

				// Description: Each point in the trajectory.
				public class Waypoint : Entity
				{
					public Waypoint(in XmlNode _node)
						: base(_node)
					{
						time = GetValue<double>("time");
					}

					// Description: The time in seconds, counted from the beginning of the script, when the pose should be reached.
					public double time = 0;
				}

				// Description: Unique id for a trajectory.
				public int id = 0;

				// Description: The tension of the trajectory spline. The default value of zero equates to a Catmull-Rom spline, which may also cause the animation to overshoot keyframes. A value of one will cause the animation to stick to the keyframes.
				public double tension = 0;

				public List<Waypoint> waypoints = new List<Waypoint>();
			}

			// Description: Set this to true for the script to be repeated in a loop. For a fluid continuous motion, make sure the last waypoint matches the first one.
			public bool loop = true;

			// Description: This is the time to wait before starting the script. If running in a loop, this time will be waited before starting each cycle.
			public double delay_start = 0;

			// Description: Set to true if the animation should start as soon as the simulation starts playing. It is useful to set this to false if the animation should only start playing only when triggered by a plugin, for example.
			public bool auto_start = true;

			public List<Trajectory> trajectories = null;
		}

		public Skin skin = null;

		public List<Animation> animations = null;

		public Script script = new Script();

		private Links links;
		private Joints joints;
		private Plugins plugins;

		public Actor(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			links = new Links(root);
			joints = new Joints(root);
			plugins = new Plugins(root);

			if (IsValidNode("skin"))
			{
				skin = new Skin();
				skin.filename = GetValue<string>("skin/filename");
				if (IsValidNode("script/scale"))
				{
					skin.scale = GetValue<double>("script/scale");
				}
			}

			if (IsValidNode("animation"))
			{
				animations = new List<Animation>();

				var nodeList = GetNodes("animation");
				foreach (XmlNode nodeItem in nodeList)
				{
					var animation = new Animation(nodeItem);
					animations.Add(animation);
				}
			}

			if (IsValidNode("script"))
			{
				script.loop = GetValue<bool>("script/loop");
				script.delay_start = GetValue<double>("script/delay_start");
				script.auto_start = GetValue<bool>("script/auto_start");

				if (IsValidNode("script/trajectory"))
				{
					script.trajectories = new List<Script.Trajectory>();
					var nodeList = GetNodes("script/trajectory");
					foreach (XmlNode nodeItem in nodeList)
					{
						var trajectory = new Script.Trajectory(nodeItem);
						script.trajectories.Add(trajectory);
					}
				}
			}
		}

		public List<Link> GetLinks()
		{
			return links.GetData();
		}

		public List<Joint> GetJoints()
		{
			return joints.GetData();
		}

		public List<Plugin> GetPlugins()
		{
			return plugins.GetData();
		}
	}
}