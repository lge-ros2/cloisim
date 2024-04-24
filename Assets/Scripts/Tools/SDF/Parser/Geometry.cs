/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Xml;
using System;

namespace SDF
{
	/*
		The class for Collision element based on SDF Specification
		parent element : <collision> or <visual>
	*/
	public class Geometry : Entity
	{
		private bool empty = false;

		private ShapeType shape = null;

		public bool IsEmpty => empty;

		public Geometry(XmlNode _node)
			: base(_node)
		{
		}

		protected override void ParseElements()
		{
			if (IsValidNode("empty"))
			{
				empty = true;
			}
			else if (IsValidNode("box"))
			{
				shape = new Box();
				var sizeStr = GetValue<string>("box/size");
				(shape as Box).size.FromString(sizeStr);
			}
			else if (IsValidNode("mesh"))
			{
				shape = new Mesh();
				var mesh = (shape as Mesh);
				mesh.uri = GetValue<string>("mesh/uri");
				var scale = GetValue<string>("mesh/scale");

				if (string.IsNullOrEmpty(scale))
				{
					mesh.scale.Set(1.0f, 1.0f, 1.0f);
				}
				else
				{
					mesh.scale.FromString(scale);
				}

				if (IsValidNode("mesh/submesh"))
				{
					mesh.submesh_name = GetValue<string>("mesh/submesh/name");
					mesh.submesh_center = GetValue<bool>("mesh/submesh/center");
				}

				// Console.WriteLine("mesh uri : " + mesh.uri + ", scale:" + scale);
			}
			else if (IsValidNode("sphere"))
			{
				shape = new Sphere();
				(shape as Sphere).radius = GetValue<double>("sphere/radius");
			}
			else if (IsValidNode("cylinder"))
			{
				shape = new Cylinder();
				(shape as Cylinder).radius = GetValue<double>("cylinder/radius");
				(shape as Cylinder).length = GetValue<double>("cylinder/length");
			}
			else if (IsValidNode("plane"))
			{
				shape = new Plane();
				var normal = GetValue<string>("plane/normal");
				(shape as Plane).normal.FromString(normal);

				var size = GetValue<string>("plane/size");
				(shape as Plane).size.FromString(size);
			}
			else if (IsValidNode("image"))
			{
				shape = new Image();

				(shape as Image).uri = GetValue<string>("image/uri");
				(shape as Image).scale = GetValue<double>("image/scale");
				(shape as Image).threshold = GetValue<int>("image/threshold");
				(shape as Image).height = GetValue<double>("image/height");
				(shape as Image).granularity = GetValue<int>("image/granularity");
			}
			else if (IsValidNode("heightmap"))
			{
				shape = new Heightmap();

				(shape as Heightmap).uri = GetValue<string>("heightmap/uri");

				var heightmapSize = GetValue<string>("heightmap/size");
				if (!string.IsNullOrEmpty(heightmapSize))
				{
					(shape as Heightmap).size.FromString(heightmapSize);
				}

				var heightmapPos = GetValue<string>("heightmap/pos");
				if (!string.IsNullOrEmpty(heightmapPos))
				{
					(shape as Heightmap).pos.FromString(heightmapPos);
				}

				if (GetValues<double>("heightmap/texture/size", out var textureSizeList))
				{
					// (shape as Heightmap).size.FromString(heightmapPos);
					if (GetValues<string>("heightmap/texture/diffuse", out var textureDiffuseList))
					{
						// (shape as Heightmap).size.FromString(heightmapPos);
						if (GetValues<string>("heightmap/texture/normal", out var textureNomralList))
						{
							// (shape as Heightmap).size.FromString(heightmapPos);
							if (textureSizeList.Count > 0 &&
								textureSizeList.Count == textureDiffuseList.Count &&
								textureDiffuseList.Count == textureNomralList.Count)
							{
								for (var i = 0; i < textureSizeList.Count; i++)
								{
									var texture = new Heightmap.Texture();
									texture.size = textureSizeList[i];
									texture.diffuse = textureDiffuseList[i];
									texture.normal = textureNomralList[i];
									// Console.Write(texture.diffuse);
									(shape as Heightmap).texture.Add(texture);
								}
								(shape as Heightmap).texture.Reverse();
							}
						}
					}
				}

				if (GetValues<double>("heightmap/blend/min_height", out var blendMinHeightList))
				{
					if (GetValues<double>("heightmap/blend/fade_dist", out var blendFadeDistList))
					{
						if (blendMinHeightList.Count > 0 && blendMinHeightList.Count == blendFadeDistList.Count)
						{
							Console.Write("Blend is not supported yet");

							if (blendMinHeightList.Count + 1 > textureSizeList.Count)
							{
								Console.WriteLine(
									"Invalid terrain, too few layers to initialize blend map, texture(" +
									 textureSizeList.Count + ") blend(" + blendMinHeightList.Count + ")");
							}
							else
							{
								for (var i = 0; i < blendMinHeightList.Count; i++)
								{
									var blend = new Heightmap.Blend();
									blend.min_height = blendMinHeightList[i];
									blend.fade_dist = blendFadeDistList[i];
									(shape as Heightmap).blend.Add(blend);
								}
							}
						}
					}
				}

				(shape as Heightmap).use_terrain_paging = GetValue<bool>("heightmap/use_terrain_paging", false);

				if ((shape as Heightmap).use_terrain_paging)
				{
					Console.WriteLine("<use_terrain_paging> is not supported yet.");
				}

				(shape as Heightmap).sampling = GetValue<uint>("heightmap/sampling", 2);
			}
			else if (IsValidNode("polyline"))
			{
				shape = new Polyline();
				if (GetValues<string>("polyline/point", out var pointList))
				{
					foreach (var pointstr in pointList)
					{
						var point = new Vector2<double>(pointstr);
						(shape as Polyline).point.Add(point);
					}
				}
				(shape as Polyline).height = GetValue<double>("polyline/height");
			}

			#region SDF_1.7_feature
			else if (IsValidNode("capsule"))
			{
				shape = new Capsule();
				(shape as Capsule).radius = GetValue<double>("capsule/radius");
				(shape as Capsule).length = GetValue<double>("capsule/length");
			}
			#endregion

			#region SDF_1.8_feature
			else if (IsValidNode("ellipsoid"))
			{
				shape = new Ellipsoid();

				var radii = GetValue<string>("ellipsoid/radii");

				if (string.IsNullOrEmpty(radii))
				{
					(shape as Ellipsoid).radii.Set(1.0f, 1.0f, 1.0f);
				}
				else
				{
					(shape as Ellipsoid).radii.FromString(radii);
				}
			}
			#endregion

			else
			{
				empty = true;
				Console.WriteLine("missing mesh type");
			}

			if (shape != null)
			{
				this.Type = shape.Type();
				// Console.WriteLine(Type);
			}
		}

		public ShapeType GetShape()
		{
			return shape;
		}
	}
}