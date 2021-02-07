/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;

namespace SDF
{
	public class Physics : Entity
	{
		// <defualt> : attribute will be ignored.
		private double max_step_size = 0.001;
		private double real_time_factor = 1.0;
		private double real_time_update_rate = 1000.0;
		private int max_contacts = 20;

		//
		// <dart> : TBD
		// <simbody> : TBD
		// <bullet> : TBD
		// <ode> - solver
		//       - constraints
		// <physx> : TBD - I want to add an element as a new feature in SDFormat specification
		//

		public Physics(XmlNode _node)
			: base(_node)
		{
			if (root != null)
			{
				ParseElements();
			}
		}

		protected override void ParseElements()
		{
			max_step_size = GetValue<double>("max_step_size");
			real_time_factor = GetValue<double>("real_time_factor");
			real_time_update_rate = GetValue<double>("real_time_update_rate");
			max_contacts = GetValue<int>("max_contacts");
		}
	}
}