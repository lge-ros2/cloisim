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
	public class Material : Entity
	{
		public class Script
		{
			public List<string> uri = new List<string>();

			#region save original path before converting uri path
			public List<string> original_uri = new List<string>();
			#endregion
			public string name;
		}

		public class Shader
		{
			// Description: vertex, pixel, normal_map_object_space, normal_map_tangent_space
			public string type = "pixel";

			// Description: filename of the normal map
			public string normal_map = "__default__";
		}

		// Description: Physically Based Rendering (PBR) material. There are two PBR workflows: metal and specular. While both workflows and their parameters can be specified at the same time, typically only one of them will be used (depending on the underlying renderer capability). It is also recommended to use the same workflow for all materials in the world.
		public class PBR
		{
			// Description: PBR using the Metallic/Roughness workflow.
			public class Metal
			{
				// Description: Filename of the diffuse/albedo map.
				public string albedo_map = string.Empty;

				// Description: Filename of the roughness map.
				public string roughness_map = string.Empty;

				// Description: Material roughness in the range of [0,1], where 0 represents a smooth surface and 1 represents a rough surface. This is the inverse of a specular map in a PBR specular workflow.
				public string roughness = "0.5";

				// Description: Filename of the metalness map.
				public string metalness_map = string.Empty;

				// Description: Material metalness in the range of [0,1], where 0 represents non-metal and 1 represents raw metal
				public string metalness = "0.5";

				// Description: Filename of the environment / reflection map, typically in the form of a cubemap
				public string environment_map = string.Empty;

				// Description: Filename of the ambient occlusion map. The map defines the amount of ambient lighting on the surface.
				public string ambient_occlusion_map = string.Empty;

				// Description: Filename of the normal map. The normals can be in the object space or tangent space as specified in the 'type' attribute
				public string normal_map = string.Empty;

				// Description: The space that the normals are in. Values are: 'object' or 'tangent'
				public string normal_map_type = "tangent";

				// Description: Filename of the emissive map.
				public string emissive_map = string.Empty;

				// Description: Filename of the light map. The light map is a prebaked light texture that is applied over the albedo map
				public string light_map = string.Empty;

				// Description: Index of the texture coordinate set to use.
				public uint light_map_uv_set = 0;
			}

			// Description: PBR using the Specular/Glossiness workflow.
			public class Specular
			{
				// Description: Filename of the diffuse/albedo map.
				public string albedo_map = string.Empty;

				// Description: Filename of the specular map.
				public string specular_map = string.Empty;

				// Description: Filename of the glossiness map.
				public string glossiness_map = string.Empty;

				// Description: Material glossiness in the range of [0-1], where 0 represents a rough surface and 1 represents a smooth surface. This is the inverse of a roughness map in a PBR metal workflow.
				public string glossiness = string.Empty;

				// Description: Filename of the environment / reflection map, typically in the form of a cubemap
				public string environment_map = string.Empty;

				// Description: Filename of the ambient occlusion map. The map defines the amount of ambient lighting on the surface.
				public string ambient_occlusion_map = string.Empty;

				// Description: Filename of the normal map. The normals can be in the object space or tangent space as specified in the 'type' attribute
				public string normal_map = string.Empty;

				// Description: The space that the normals are in. Values are: 'object' or 'tangent'
				public string normal_map_type = "tangent";

				// Description: Filename of the emissive map.
				public string emissive_map = string.Empty;

				// Description: Filename of the light map. The light map is a prebaked light texture that is applied over the albedo map
				public string light_map = string.Empty;

				// Description: Index of the texture coordinate set to use.
				public uint light_map_uv_set = 0;
			}

			public Metal metal = null;

			public Specular specular = null;
		}

		public Script script = null;

		public Shader shader = null;

		// Description: Set render order for coplanar polygons. The higher value will be rendered on top of the other coplanar polygons
		public float render_order = 0;

		// Description: If false, dynamic lighting will be disabled
		public bool lighting = true;

		#region SDF 1.6 feature
		public double shininess = 0;
		#endregion

		public Color ambient = null;
		public Color diffuse = null;
		public Color specular = null;
		public Color emissive = null;

		// Description: If true, the mesh that this material is applied to will be rendered as double sided
		public bool double_sided = false;

		public PBR pbr = null;

		public Material(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			if (IsValidNode("script"))
			{
				script = new Script();

				if (GetValues<string>("script/uri", out var script_uri_list))
				{
					script.uri.AddRange(script_uri_list);

					// foreach (var uri in script.uri)
					// {
					// 	Console.Write(uri);
					// }
				}

				#region save original path before converting uri path
				if (GetValues<string>("script/original_uri", out var script_original_uri_list))
				{
					script.original_uri.AddRange(script_original_uri_list);

					// foreach (var uri in script.original_uri)
					// {
					// 	Console.Write(uri);
					// }
				}
				#endregion

				if (IsValidNode("script/name"))
				{
					script.name = GetValue<string>("script/name");
					// Console.Write(script.name);
				}
			}

			if (IsValidNode("shader"))
			{
				shader = new Shader();
				shader.type = GetAttributeInPath<string>("shader", "type");
				shader.normal_map = GetValue<string>("shader/normal_map");
			}

			lighting = GetValue<bool>("lighting", true);

			if (IsValidNode("ambient"))
			{
				ambient = new Color();
				ambient.FromString(GetValue<string>("ambient"));
				// Console.Write("sdf/material/ambient");
			}

			if (IsValidNode("diffuse"))
			{
				diffuse = new Color();
				diffuse.FromString(GetValue<string>("diffuse"));
				// Console.Write("sdf/material/diffuse");
			}

			if (IsValidNode("specular"))
			{
				specular = new Color();
				specular.FromString(GetValue<string>("specular"));
				// Console.Write("sdf/material/specular");
			}

			shininess = GetValue<float>("shininess");

			if (IsValidNode("emissive"))
			{
				emissive = new Color();
				emissive.FromString(GetValue<string>("emissive"));
				// Console.Write("sdf/material/emissive");
			}

			double_sided = GetValue<bool>("double_sided");

			if (IsValidNode("pbr"))
			{
				Console.Write("pbr: Not Supported yet");
			}
		}
	}
}