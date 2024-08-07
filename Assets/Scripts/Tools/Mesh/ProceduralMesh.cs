/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using System.Collections.Generic;
using UnityEngine;
using Game.Utils.Math;
using Game.Utils.Triangulation;

/// <summary>https://wiki.unity3d.com/index.php/ProceduralPrimitives</summary>
public class ProceduralMesh
{
	public enum Type { BOX, CYLINDER, SPHERE, ELLIPSOID, PLANE };

	private static Dictionary<Type, Mesh> MeshObjectCache = new Dictionary<Type, Mesh>();

	private const float PI = Mathf.PI;
	private const float PI2 = PI * 2f;

	public static Mesh CreateBox(
		in float length = 1f, in float width = 1f, in float height = 1f)
	{
		Mesh mesh;

		if (!MeshObjectCache.ContainsKey(Type.BOX))
		{
			mesh = new Mesh();

			#region Vertices
			var p0 = new Vector3(-.5f, -.5f, .5f);
			var p1 = new Vector3(.5f, -.5f, .5f);
			var p2 = new Vector3(.5f, -.5f, -.5f);
			var p3 = new Vector3(-.5f, -.5f, -.5f);

			var p4 = new Vector3(-.5f, .5f, .5f);
			var p5 = new Vector3(.5f, .5f, .5f);
			var p6 = new Vector3(.5f, .5f, -.5f);
			var p7 = new Vector3(-.5f, .5f, -.5f);

			// var p0 = new Vector3(-length * .5f, -width * .5f, height * .5f);
			// var p1 = new Vector3(length * .5f, -width * .5f, height * .5f);
			// var p2 = new Vector3(length * .5f, -width * .5f, -height * .5f);
			// var p3 = new Vector3(-length * .5f, -width * .5f, -height * .5f);

			// var p4 = new Vector3(-length * .5f, width * .5f, height * .5f);
			// var p5 = new Vector3(length * .5f, width * .5f, height * .5f);
			// var p6 = new Vector3(length * .5f, width * .5f, -height * .5f);
			// var p7 = new Vector3(-length * .5f, width * .5f, -height * .5f);

			var vertices = new Vector3[]
			{
			p0, p1, p2, p3, // Bottom
			p7, p4, p0, p3, // Left
			p4, p5, p1, p0, // Front
			p6, p7, p3, p2, // Back
			p5, p6, p2, p1, // Right
			p7, p6, p5, p4 // Top
			};
			#endregion

			#region Normals
			var up = Vector3.up;
			var down = Vector3.down;
			var front = Vector3.forward;
			var back = Vector3.back;
			var left = Vector3.left;
			var right = Vector3.right;

			var normals = new Vector3[]
			{
			down, down, down, down, // Bottom
			left, left, left, left, // Left
			front, front, front, front, // Front
			back, back, back, back, // Back
			right, right, right, right, // Right
			up, up, up, up // Top
			};
			#endregion

			#region UVs
			var _00 = new Vector2(0f, 0f);
			var _10 = new Vector2(1f, 0f);
			var _01 = new Vector2(0f, 1f);
			var _11 = new Vector2(1f, 1f);

			var uvs = new Vector2[]
			{
			_11, _01, _00, _10, // Bottom
			_11, _01, _00, _10, // Left
			_11, _01, _00, _10, // Front
			_11, _01, _00, _10, // Back
			_11, _01, _00, _10, // Right
			_11, _01, _00, _10, // Top
			};
			#endregion

			#region Triangles
			var triangles = new int[]
			{
			// Bottom
			3, 1, 0,
			3, 2, 1,

			// Left
			3 + 4 * 1, 1 + 4 * 1, 0 + 4 * 1,
			3 + 4 * 1, 2 + 4 * 1, 1 + 4 * 1,

			// Front
			3 + 4 * 2, 1 + 4 * 2, 0 + 4 * 2,
			3 + 4 * 2, 2 + 4 * 2, 1 + 4 * 2,

			// Back
			3 + 4 * 3, 1 + 4 * 3, 0 + 4 * 3,
			3 + 4 * 3, 2 + 4 * 3, 1 + 4 * 3,

			// Right
			3 + 4 * 4, 1 + 4 * 4, 0 + 4 * 4,
			3 + 4 * 4, 2 + 4 * 4, 1 + 4 * 4,

			// Top
			3 + 4 * 5, 1 + 4 * 5, 0 + 4 * 5,
			3 + 4 * 5, 2 + 4 * 5, 1 + 4 * 5,
			};
			#endregion

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			MeshObjectCache.Add(Type.BOX, mesh);
		}

		mesh = Object.Instantiate(MeshObjectCache[Type.BOX]);
		mesh.name = "Box";

		var meshVertices = mesh.vertices;
		for (var i = 0; i < mesh.vertexCount; i++)
		{
			var vertex = meshVertices[i];
			vertex.Scale(new Vector3(length, width, height));
			meshVertices[i] = vertex;
		}
		mesh.vertices = meshVertices;

		return mesh;
	}

	public static Mesh CreateCylinder(
		in float radius = 1f, in float height = 1f, in int nbSides = 36,
		in float upRotationAngle = 0)
	{
		Mesh mesh;

		if (!MeshObjectCache.ContainsKey(Type.CYLINDER))
		{
			mesh = CreateCone(1, 1, 1, nbSides);
			mesh.name = "Cylinder";
			MeshObjectCache.Add(Type.CYLINDER, mesh);
		}

		mesh = Object.Instantiate(MeshObjectCache[Type.CYLINDER]);
		mesh.name = "Cylinder";

		var rotationMatrix = Quaternion.AngleAxis(upRotationAngle, Vector3.up);

		var meshVertices = mesh.vertices;
		for (var i = 0; i < mesh.vertexCount; i++)
		{
			var vertex = meshVertices[i];
			vertex.Scale(new Vector3(radius, height, radius));

			meshVertices[i] = rotationMatrix * vertex;
		}
		mesh.vertices = meshVertices;

		return mesh;
	}

	public static Mesh CreateCone(
		in float topRadius = .01f, in float bottomRadius = 0.5f,
		in float height = 1f, in int nbSides = 18)
	{
		var mesh = new Mesh();
		mesh.name = "Cone";

		var heightHalf = height / 2;

		const int nbHeightSeg = 1; // Not implemented yet

		var nbVerticesCap = nbSides + 1;

		#region Vertices
		// bottom + top + sides
		var vertices = new Vector3[nbVerticesCap + nbVerticesCap + nbSides * nbHeightSeg * 2 + 2];
		var vert = 0;

		// Bottom cap
		vertices[vert++] = new Vector3(0f, -heightHalf, 0f);
		while (vert <= nbSides)
		{
			float rad = (float)vert / nbSides * PI2;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * bottomRadius, -heightHalf, Mathf.Sin(rad) * bottomRadius);
			vert++;
		}

		// Top cap
		vertices[vert++] = new Vector3(0f, heightHalf, 0f);
		while (vert <= nbSides * 2 + 1)
		{
			var rad = (float)(vert - nbSides - 1) / nbSides * PI2;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, heightHalf, Mathf.Sin(rad) * topRadius);
			vert++;
		}

		// Sides
		int v = 0;
		while (vert <= vertices.Length - 4)
		{
			var rad = (float)v / nbSides * PI2;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, heightHalf, Mathf.Sin(rad) * topRadius);
			vertices[vert + 1] = new Vector3(Mathf.Cos(rad) * bottomRadius, -heightHalf, Mathf.Sin(rad) * bottomRadius);
			vert += 2;
			v++;
		}
		vertices[vert] = vertices[nbSides * 2 + 2];
		vertices[vert + 1] = vertices[nbSides * 2 + 3];
		#endregion

		#region Normals
		// bottom + top + sides
		var normals = new Vector3[vertices.Length];
		vert = 0;

		// Bottom cap
		while (vert <= nbSides)
		{
			normals[vert++] = Vector3.down;
		}

		// Top cap
		while (vert <= nbSides * 2 + 1)
		{
			normals[vert++] = Vector3.up;
		}

		// Sides
		v = 0;
		while (vert <= vertices.Length - 4)
		{
			var rad = (float)v / nbSides * PI2;
			var cos = Mathf.Cos(rad);
			var sin = Mathf.Sin(rad);

			normals[vert] = new Vector3(cos, 0f, sin);
			normals[vert + 1] = normals[vert];

			vert += 2;
			v++;
		}
		normals[vert] = normals[nbSides * 2 + 2];
		normals[vert + 1] = normals[nbSides * 2 + 3];
		#endregion

		#region UVs
		var uvs = new Vector2[vertices.Length];
		var u = 0;

		// Bottom cap
		uvs[u++] = new Vector2(0.5f, 0.5f);
		while (u <= nbSides)
		{
			var rad = (float)u / nbSides * PI2;
			uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			u++;
		}

		// Top cap
		uvs[u++] = new Vector2(0.5f, 0.5f);
		while (u <= nbSides * 2 + 1)
		{
			var rad = (float)u / nbSides * PI2;
			uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			u++;
		}

		// Sides
		var u_sides = 0;
		while (u <= uvs.Length - 4)
		{
			var t = (float)u_sides / nbSides;
			uvs[u] = new Vector3(t, 1f);
			uvs[u + 1] = new Vector3(t, 0f);
			u += 2;
			u_sides++;
		}

		uvs[u] = new Vector2(1f, 1f);
		uvs[u + 1] = new Vector2(1f, 0f);
		#endregion

		#region Triangles
		var nbTriangles = nbSides + nbSides + nbSides * 2;
		var triangles = new int[nbTriangles * 3 + 3];

		// Bottom cap
		var tri = 0;
		var i = 0;
		while (tri < nbSides - 1)
		{
			triangles[i] = 0;
			triangles[i + 1] = tri + 1;
			triangles[i + 2] = tri + 2;
			tri++;
			i += 3;
		}
		triangles[i] = 0;
		triangles[i + 1] = tri + 1;
		triangles[i + 2] = 1;
		tri++;
		i += 3;

		// Top cap
		//tri++;
		while (tri < nbSides * 2)
		{
			triangles[i] = tri + 2;
			triangles[i + 1] = tri + 1;
			triangles[i + 2] = nbVerticesCap;
			tri++;
			i += 3;
		}

		triangles[i] = nbVerticesCap + 1;
		triangles[i + 1] = tri + 1;
		triangles[i + 2] = nbVerticesCap;
		tri++;
		i += 3;
		tri++;

		// Sides
		while (tri <= nbTriangles)
		{
			triangles[i] = tri + 2;
			triangles[i + 1] = tri + 1;
			triangles[i + 2] = tri + 0;
			tri++;
			i += 3;

			triangles[i] = tri + 1;
			triangles[i + 1] = tri + 2;
			triangles[i + 2] = tri + 0;
			tri++;
			i += 3;
		}
		#endregion

		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		return mesh;
	}

	// Longitude |||
	// Latitude ---
	public static Mesh CreateSphere(in float radius = 1f, in int nbLong = 15, in int nbLat = 15)
	{
		return CreateSphere(Vector3.one * radius, nbLong, nbLat, "Sphere", Type.SPHERE);
	}

	public static Mesh CreateSphere(
		in Vector3 scale, in int nbLong = 15, in int nbLat = 15,
		in string meshName = "Ellipsoid", in Type meshType = Type.ELLIPSOID)
	{
		Mesh mesh;

		if (!MeshObjectCache.ContainsKey(meshType))
		{
			const float UnitRadius = 1f;
			mesh = new Mesh();
			mesh.name = "Sphere";

			#region Vertices
			var vertices = new Vector3[(nbLong + 1) * nbLat + 2];

			vertices[0] = Vector3.up * UnitRadius;
			for (var lat = 0; lat < nbLat; lat++)
			{
				var a1 = PI * (float)(lat + 1) / (nbLat + 1);
				var sin1 = Mathf.Sin(a1);
				var cos1 = Mathf.Cos(a1);

				for (var lon = 0; lon <= nbLong; lon++)
				{
					var a2 = PI2 * (float)(lon == nbLong ? 0 : lon) / nbLong;
					var sin2 = Mathf.Sin(a2);
					var cos2 = Mathf.Cos(a2);

					vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * UnitRadius;
				}
			}
			vertices[vertices.Length - 1] = Vector3.up * -UnitRadius;
			#endregion

			#region Normals
			var normals = new Vector3[vertices.Length];
			for (var n = 0; n < vertices.Length; n++)
			{
				normals[n] = vertices[n].normalized;
			}
			#endregion

			#region UVs
			var uvs = new Vector2[vertices.Length];
			uvs[0] = Vector2.up;
			uvs[uvs.Length - 1] = Vector2.zero;

			for (var lat = 0; lat < nbLat; lat++)
			{
				for (var lon = 0; lon <= nbLong; lon++)
				{
					uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
				}
			}
			#endregion

			#region Triangles
			var nbFaces = vertices.Length;
			var nbTriangles = nbFaces * 2;
			var nbIndexes = nbTriangles * 3;
			var triangles = new int[nbIndexes];

			// Top Cap
			var i = 0;
			for (var lon = 0; lon < nbLong; lon++)
			{
				triangles[i++] = lon + 2;
				triangles[i++] = lon + 1;
				triangles[i++] = 0;
			}

			// Middle
			for (var lat = 0; lat < nbLat - 1; lat++)
			{
				for (var lon = 0; lon < nbLong; lon++)
				{
					var current = lon + lat * (nbLong + 1) + 1;
					var next = current + nbLong + 1;

					triangles[i++] = current;
					triangles[i++] = current + 1;
					triangles[i++] = next + 1;

					triangles[i++] = current;
					triangles[i++] = next + 1;
					triangles[i++] = next;
				}
			}

			// Bottom Cap
			for (var lon = 0; lon < nbLong; lon++)
			{
				triangles[i++] = vertices.Length - 1;
				triangles[i++] = vertices.Length - (lon + 2) - 1;
				triangles[i++] = vertices.Length - (lon + 1) - 1;
			}
			#endregion

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			MeshObjectCache.Add(Type.SPHERE, mesh);
		}

		mesh = Object.Instantiate(MeshObjectCache[Type.SPHERE]);
		mesh.name = meshName;

		var meshVertices = mesh.vertices;
		for (var i = 0; i < mesh.vertexCount; i++)
		{
			var vertex = meshVertices[i];
			vertex.Scale(scale);
			meshVertices[i] = vertex;
		}
		mesh.vertices = meshVertices;

		return mesh;
	}

	public static Mesh CreatePlane(
		in float length = 1f, in float width = 1f, Vector3 normal = default(Vector3),
		in int resolutionX = 15, in int resolutionZ = 15)
	{
		Mesh mesh;
		var cacheKey = Type.PLANE + resolutionX * resolutionZ;

		if (!MeshObjectCache.ContainsKey(cacheKey))
		{
			if (normal.Equals(default(Vector3)))
			{
				normal = Vector3.up;
			}

			mesh = new Mesh();
			mesh.name = "Plane";

			var resX = (resolutionX < 2) ? 2 : resolutionX + 1;
			var resZ = (resolutionZ < 2) ? 2 : resolutionZ + 1;

			#region Vertices
			var vertices = new Vector3[resX * resZ];
			for (var z = 0; z < resZ; z++)
			{
				// [ -length / 2, length / 2 ]
				var zPos = ((float)z / (resZ - 1) - .5f);

				for (var x = 0; x < resX; x++)
				{
					// [ -width / 2, width / 2 ]
					var xPos = ((float)x / (resX - 1) - .5f);
					vertices[x + z * resX] = new Vector3(xPos, 0f, zPos);
				}
			}
			#endregion

			#region Normals
			var normals = new Vector3[vertices.Length];
			for (var n = 0; n < normals.Length; n++)
			{
				normals[n] = normal;
			}
			#endregion

			#region UVs
			var uvs = new Vector2[vertices.Length];
			for (var v = 0; v < resZ; v++)
			{
				for (var u = 0; u < resX; u++)
				{
					var U = (float)u / (resX - 1);
					var V = (float)v / (resZ - 1);
					uvs[u + v * resX] = new Vector2(V, U);
				}
			}
			#endregion

			#region Triangles
			var nbFaces = (resX - 1) * (resZ - 1);
			var triangles = new int[nbFaces * 6];
			var t = 0;
			for (var face = 0; face < nbFaces; face++)
			{
				// Retrieve lower left corner from face ind
				var i = face % (resX - 1) + (face / (resZ - 1) * resX);

				triangles[t++] = i + resX;
				triangles[t++] = i + 1;
				triangles[t++] = i;

				triangles[t++] = i + resX;
				triangles[t++] = i + resX + 1;
				triangles[t++] = i + 1;
			}
			#endregion

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.uv = uvs;
			mesh.triangles = triangles;

			MeshObjectCache.Add(cacheKey, mesh);
		}

		mesh = Object.Instantiate(MeshObjectCache[cacheKey]);

		var meshVertices = mesh.vertices;
		for (var i = 0; i < mesh.vertexCount; i++)
		{
			var vertex = meshVertices[i];
			vertex.Scale(new Vector3(width, 1, length));
			meshVertices[i] = vertex;
		}
		mesh.vertices = meshVertices;

		return mesh;
	}

	public static Mesh CreateCapsule(in float radius = 0.5f, in float length = 2f, int segments = 24)
	{
		Mesh mesh = new Mesh();
		mesh.name = "Capsule";

		// make segments an even number
		if (segments % 2 != 0)
		{
			segments++;
		}

		// extra vertex on the seam
		var points = segments + 1;

		// calculate points around a circle
		var pX = new float[points];
		var pZ = new float[points];
		var pY = new float[points];
		var pR = new float[points];

		var calcH = 0f;
		var calcV = 0f;

		for (var i = 0; i < points; i++)
		{
			pX[i] = Mathf.Sin(calcH * Mathf.Deg2Rad);
			pZ[i] = Mathf.Cos(calcH * Mathf.Deg2Rad);
			pY[i] = Mathf.Cos(calcV * Mathf.Deg2Rad);
			pR[i] = Mathf.Sin(calcV * Mathf.Deg2Rad);

			calcH += 360f / (float)segments;
			calcV += 180f / (float)segments;
		}

		// Vertices and UVs
		var vertices = new Vector3[points * (points + 1)];
		var uvs = new Vector2[vertices.Length];
		var ind = 0;

		// Y-offset is half the height minus the diameter
		var yOff = (length - (radius * 2f)) * 0.5f;
		if (yOff < 0)
		{
			yOff = 0;
		}

		// uv calculations
		var stepX = 1f / ((float)(points - 1));
		float uvX, uvY;

		// Top Hemisphere
		var top = Mathf.CeilToInt((float)points * 0.5f);

		for (var y = 0; y < top; y++)
		{
			for (var x = 0; x < points; x++)
			{
				vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
				vertices[ind].y = yOff + vertices[ind].y;

				uvX = 1f - (stepX * (float)x);
				uvY = (vertices[ind].y + (length * 0.5f)) / length;
				uvs[ind] = new Vector2(uvX, uvY);

				ind++;
			}
		}

		// Bottom Hemisphere
		var btm = Mathf.FloorToInt((float)points * 0.5f);

		for (var y = btm; y < points; y++)
		{
			for (var x = 0; x < points; x++)
			{
				vertices[ind] = new Vector3(pX[x] * pR[y], pY[y], pZ[x] * pR[y]) * radius;
				vertices[ind].y = -yOff + vertices[ind].y;

				uvX = 1f - (stepX * (float)x);
				uvY = (vertices[ind].y + (length * 0.5f)) / length;
				uvs[ind] = new Vector2(uvX, uvY);

				ind++;
			}
		}

		// Triangles
		var triangles = new int[(segments * (segments + 1) * 2 * 3)];
		var t = 0;
		for (var y = 0; y < segments + 1; y++)
		{
			for (var x = 0; x < segments; x++, t += 6)
			{
				triangles[t + 0] = ((y + 0) * (segments + 1)) + x + 0;
				triangles[t + 1] = ((y + 1) * (segments + 1)) + x + 0;
				triangles[t + 2] = ((y + 1) * (segments + 1)) + x + 1;

				triangles[t + 3] = ((y + 0) * (segments + 1)) + x + 1;
				triangles[t + 4] = ((y + 0) * (segments + 1)) + x + 0;
				triangles[t + 5] = ((y + 1) * (segments + 1)) + x + 1;
			}
		}

		var normals = new Vector3[vertices.Length];
		for (var n = 0; n < vertices.Length; n++)
		{
			normals[n] = vertices[n].normalized;
		}

		mesh.vertices = vertices;
		mesh.normals = normals;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		return mesh;
	}

	public static Mesh CreatePolylines(in SDF.Polylines polylines)
	{
		// Enables tesselation (before calculating constrained edges) when greater than zero.
		// It subdivides the triangles until each of them has an area smaller than this value.
		const float TesselationMaximumTriangleArea = 0.0f;

		// distance tolerance between 2 points.
		// This is used when creating a list of distinct points in the polylines.
		const float tolerance = 1e-4f;

		var height = polylines.Count == 0 ? 1f : (float)polylines[0].height;
		var halfHeight = height * 0.5f;

		// close all the loops
		foreach (var polyline in polylines)
		{
			// does the poly ends with the first point?
			var first = polyline.point[0];
			var last = polyline.point[polyline.point.Count - 1];
			var distance = (first.X - last.X) * (first.X - last.X) + (first.Y - last.Y) * (first.Y - last.Y);

			// within range
			if (distance > tolerance * tolerance)
			{
				polyline.point.Add(first);
				Debug.LogWarning("add the first point at the ends");
			}
		}

		var pointsToTriangulate = new List<Vector2>();
		if (polylines.Count > 0)
		{
			foreach (var point in polylines[0].point)
			{
				pointsToTriangulate.Add(SDF2Unity.Point(point));
			}
		}

		List<List<Vector2>> constrainedEdgePoints = null;
		if (polylines.Count > 1)
		{
			constrainedEdgePoints = new List<List<Vector2>>();

			for (int i = 1; i < polylines.Count; i++)
			{
				var points = new List<Vector2>();
				foreach (var point in polylines[i].point)
				{
					points.Add(SDF2Unity.Point(point));
				}
				constrainedEdgePoints.Add(points);
			}
		}

		var outputTriangles = new List<Triangle2D>();
		var triangulator = new DelaunayTriangulation();
		triangulator.Triangulate(pointsToTriangulate, TesselationMaximumTriangleArea, constrainedEdgePoints);
		triangulator.GetTrianglesDiscardingHoles(outputTriangles);

		var planeMesh = CreateMeshFromTriangles(outputTriangles);

		var extrudedMesh = new Mesh();
		extrudedMesh.name = "Polyline";

		var extrusion = new Matrix4x4[2]
		{
			Matrix4x4.TRS(Vector3.forward * -halfHeight, Quaternion.identity, Vector3.one),
			Matrix4x4.TRS(Vector3.forward * halfHeight, Quaternion.identity, Vector3.one)
		};
		MeshExtrusion.ExtrudeMesh(planeMesh, extrudedMesh, extrusion, false);

		return extrudedMesh;
	}

	private static Mesh CreateMeshFromTriangles(IReadOnlyList<Triangle2D> triangles)
	{
		var vertices = new List<Vector3>(triangles.Count * 3);
		var indices = new List<int>(triangles.Count * 3);

		for (var i = 0; i < triangles.Count; ++i)
		{
			vertices.Add(triangles[i].p0);
			vertices.Add(triangles[i].p1);
			vertices.Add(triangles[i].p2);
			indices.Add(i * 3 + 2); // Changes order
			indices.Add(i * 3 + 1);
			indices.Add(i * 3);
		}

		var uvs = new Vector2[vertices.Count];
		for (int i = 0; i < uvs.Length; i++)
		{
			uvs[i] = new Vector2(vertices[i].x, vertices[i].y);
		}

		var mesh = new Mesh();
		mesh.subMeshCount = 1;
		mesh.SetVertices(vertices);
		mesh.SetIndices(indices, MeshTopology.Triangles, 0);
		mesh.SetUVs(0, uvs);
		return mesh;
	}
}