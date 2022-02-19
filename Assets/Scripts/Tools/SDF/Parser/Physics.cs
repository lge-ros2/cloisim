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
		// Description: If true, this physics element is set as the default physics profile for the world. If multiple default physics elements exist, the first element marked as default is chosen. If no default physics element exists, the first physics element is chosen.
		public bool _default = false;

		// <defualt> : attribute will be ignored.
		private double max_step_size = 0.001;
		private double real_time_factor = 1.0;
		private double real_time_update_rate = 1000.0;
		private int max_contacts = 20;

		// <dart> : TBD
		// <simbody> : TBD
		// <bullet> : TBD
		// <ode> : TBD
		// <physx> : TBD - I want to propose a new element in SDFormat specification

		public Physics(XmlNode _node)
			: base(_node)
		{
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