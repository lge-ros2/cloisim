using System;
using UnityEngine;

/// <summary>https://wiki.unity3d.com/index.php/ProceduralPrimitives</summary>
public class ProceduralMesh
{
	public static Mesh CreateBox(in float length = 1f, in float width = 1f, in float height = 1f)
	{
		Mesh mesh = new Mesh();
		mesh.Clear();
		mesh.name = "Box";

		#region Vertices
		var p0 = new Vector3(-length * .5f, -width * .5f, height * .5f);
		var p1 = new Vector3(length * .5f, -width * .5f, height * .5f);
		var p2 = new Vector3(length * .5f, -width * .5f, -height * .5f);
		var p3 = new Vector3(-length * .5f, -width * .5f, -height * .5f);

		var p4 = new Vector3(-length * .5f, width * .5f, height * .5f);
		var p5 = new Vector3(length * .5f, width * .5f, height * .5f);
		var p6 = new Vector3(length * .5f, width * .5f, -height * .5f);
		var p7 = new Vector3(-length * .5f, width * .5f, -height * .5f);

		Vector3[] vertices = new Vector3[]
		{
			p0, p1, p2, p3, // Bottom
			p7, p4, p0, p3, // Left
			p4, p5, p1, p0, // Front
			p6, p7, p3, p2, // Back
			p5, p6, p2, p1, // Right
			p7, p6, p5, p4 // Top
		};
		#endregion

		#region Normales
		var up = Vector3.up;
		var down = Vector3.down;
		var front = Vector3.forward;
		var back = Vector3.back;
		var left = Vector3.left;
		var right = Vector3.right;

		Vector3[] normales = new Vector3[]
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
		Vector2 _00 = new Vector2(0f, 0f);
		Vector2 _10 = new Vector2(1f, 0f);
		Vector2 _01 = new Vector2(0f, 1f);
		Vector2 _11 = new Vector2(1f, 1f);

		Vector2[] uvs = new Vector2[]
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
		int[] triangles = new int[]
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
		mesh.normals = normales;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		mesh.RecalculateBounds();
		mesh.Optimize();

		return mesh;
	}

	public static Mesh CreateCylinder(in float radius = 1f, in float height = 1f, in int nbSides = 36)
	{
		return CreateCone(radius, radius, height, nbSides);
	}

	public static Mesh CreateCone(in float topRadius = .01f, in float bottomRadius = 0.5f, in float height = 1f, in int nbSides = 18)
	{
		Mesh mesh = new Mesh();
		mesh.Clear();
		mesh.name = (topRadius.Equals(bottomRadius)) ? "Cylinder" : "Cone";

		float heightHalf = height / 2;
		const int nbHeightSeg = 1; // Not implemented yet

		int nbVerticesCap = nbSides + 1;

		#region Vertices
		// bottom + top + sides
		Vector3[] vertices = new Vector3[nbVerticesCap + nbVerticesCap + nbSides * nbHeightSeg * 2 + 2];
		int vert = 0;
		float _2pi = Mathf.PI * 2f;

		// Bottom cap
		vertices[vert++] = new Vector3(0f, -heightHalf, 0f);
		while (vert <= nbSides)
		{
			float rad = (float)vert / nbSides * _2pi;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * bottomRadius, -heightHalf, Mathf.Sin(rad) * bottomRadius);
			vert++;
		}

		// Top cap
		vertices[vert++] = new Vector3(0f, heightHalf, 0f);
		while (vert <= nbSides * 2 + 1)
		{
			float rad = (float)(vert - nbSides - 1) / nbSides * _2pi;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, heightHalf, Mathf.Sin(rad) * topRadius);
			vert++;
		}

		// Sides
		int v = 0;
		while (vert <= vertices.Length - 4)
		{
			float rad = (float)v / nbSides * _2pi;
			vertices[vert] = new Vector3(Mathf.Cos(rad) * topRadius, heightHalf, Mathf.Sin(rad) * topRadius);
			vertices[vert + 1] = new Vector3(Mathf.Cos(rad) * bottomRadius, -heightHalf, Mathf.Sin(rad) * bottomRadius);
			vert += 2;
			v++;
		}
		vertices[vert] = vertices[nbSides * 2 + 2];
		vertices[vert + 1] = vertices[nbSides * 2 + 3];
		#endregion

		#region Normales

		// bottom + top + sides
		Vector3[] normales = new Vector3[vertices.Length];
		vert = 0;

		// Bottom cap
		while (vert <= nbSides)
		{
			normales[vert++] = Vector3.down;
		}

		// Top cap
		while (vert <= nbSides * 2 + 1)
		{
			normales[vert++] = Vector3.up;
		}

		// Sides
		v = 0;
		while (vert <= vertices.Length - 4)
		{
			float rad = (float)v / nbSides * _2pi;
			float cos = Mathf.Cos(rad);
			float sin = Mathf.Sin(rad);

			normales[vert] = new Vector3(cos, 0f, sin);
			normales[vert + 1] = normales[vert];

			vert += 2;
			v++;
		}
		normales[vert] = normales[nbSides * 2 + 2];
		normales[vert + 1] = normales[nbSides * 2 + 3];
		#endregion

		#region UVs
		Vector2[] uvs = new Vector2[vertices.Length];

		// Bottom cap
		int u = 0;
		uvs[u++] = new Vector2(0.5f, 0.5f);
		while (u <= nbSides)
		{
			float rad = (float)u / nbSides * _2pi;
			uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			u++;
		}

		// Top cap
		uvs[u++] = new Vector2(0.5f, 0.5f);
		while (u <= nbSides * 2 + 1)
		{
			float rad = (float)u / nbSides * _2pi;
			uvs[u] = new Vector2(Mathf.Cos(rad) * .5f + .5f, Mathf.Sin(rad) * .5f + .5f);
			u++;
		}

		// Sides
		int u_sides = 0;
		while (u <= uvs.Length - 4)
		{
			float t = (float)u_sides / nbSides;
			uvs[u] = new Vector3(t, 1f);
			uvs[u + 1] = new Vector3(t, 0f);
			u += 2;
			u_sides++;
		}
		uvs[u] = new Vector2(1f, 1f);
		uvs[u + 1] = new Vector2(1f, 0f);
		#endregion

		#region Triangles
		int nbTriangles = nbSides + nbSides + nbSides * 2;
		int[] triangles = new int[nbTriangles * 3 + 3];

		// Bottom cap
		int tri = 0;
		int i = 0;
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
		mesh.normals = normales;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		mesh.RecalculateTangents();
		mesh.RecalculateBounds();
		mesh.RecalculateNormals();
		mesh.Optimize();

		return mesh;
	}


	// Longitude |||
	// Latitude ---
	public static Mesh CreateSphere(in float radius = 1f, int nbLong = 24, int nbLat = 16)
	{
		Mesh mesh = new Mesh();
		mesh.Clear();
		mesh.name = "Sphere";

		#region Vertices
		Vector3[] vertices = new Vector3[(nbLong + 1) * nbLat + 2];
		float _pi = Mathf.PI;
		float _2pi = _pi * 2f;

		vertices[0] = Vector3.up * radius;
		for (int lat = 0; lat < nbLat; lat++)
		{
			float a1 = _pi * (float)(lat + 1) / (nbLat + 1);
			float sin1 = Mathf.Sin(a1);
			float cos1 = Mathf.Cos(a1);

			for (int lon = 0; lon <= nbLong; lon++)
			{
				float a2 = _2pi * (float)(lon == nbLong ? 0 : lon) / nbLong;
				float sin2 = Mathf.Sin(a2);
				float cos2 = Mathf.Cos(a2);

				vertices[lon + lat * (nbLong + 1) + 1] = new Vector3(sin1 * cos2, cos1, sin1 * sin2) * radius;
			}
		}
		vertices[vertices.Length - 1] = Vector3.up * -radius;
		#endregion

		#region Normales
		Vector3[] normales = new Vector3[vertices.Length];
		for (int n = 0; n < vertices.Length; n++)
			normales[n] = vertices[n].normalized;
		#endregion

		#region UVs
		Vector2[] uvs = new Vector2[vertices.Length];
		uvs[0] = Vector2.up;
		uvs[uvs.Length - 1] = Vector2.zero;
		for (int lat = 0; lat < nbLat; lat++)
			for (int lon = 0; lon <= nbLong; lon++)
				uvs[lon + lat * (nbLong + 1) + 1] = new Vector2((float)lon / nbLong, 1f - (float)(lat + 1) / (nbLat + 1));
		#endregion

		#region Triangles
		int nbFaces = vertices.Length;
		int nbTriangles = nbFaces * 2;
		int nbIndexes = nbTriangles * 3;
		int[] triangles = new int[nbIndexes];

		//Top Cap
		int i = 0;
		for (int lon = 0; lon < nbLong; lon++)
		{
			triangles[i++] = lon + 2;
			triangles[i++] = lon + 1;
			triangles[i++] = 0;
		}

		//Middle
		for (int lat = 0; lat < nbLat - 1; lat++)
		{
			for (int lon = 0; lon < nbLong; lon++)
			{
				int current = lon + lat * (nbLong + 1) + 1;
				int next = current + nbLong + 1;

				triangles[i++] = current;
				triangles[i++] = current + 1;
				triangles[i++] = next + 1;

				triangles[i++] = current;
				triangles[i++] = next + 1;
				triangles[i++] = next;
			}
		}

		//Bottom Cap
		for (int lon = 0; lon < nbLong; lon++)
		{
			triangles[i++] = vertices.Length - 1;
			triangles[i++] = vertices.Length - (lon + 2) - 1;
			triangles[i++] = vertices.Length - (lon + 1) - 1;
		}
		#endregion

		mesh.vertices = vertices;
		mesh.normals = normales;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		mesh.RecalculateBounds();
		mesh.Optimize();

		return mesh;
	}

	// 2 minimum
	public static Mesh CreatePlane(in float length = 1f, in float width = 1f, Vector3 normal = default(Vector3), in int resX = 2, in int resZ = 2)
	{
		if (normal.Equals(default(Vector3)))
		{
			normal = Vector3.up;
		}

		Mesh mesh = new Mesh();
		mesh.Clear();
		mesh.name = "Plane";

		#region Vertices
		Vector3[] vertices = new Vector3[resX * resZ];
		for (int z = 0; z < resZ; z++)
		{
			// [ -length / 2, length / 2 ]
			float zPos = ((float)z / (resZ - 1) - .5f) * length;
			for (int x = 0; x < resX; x++)
			{
				// [ -width / 2, width / 2 ]
				float xPos = ((float)x / (resX - 1) - .5f) * width;
				vertices[x + z * resX] = new Vector3(xPos, 0f, zPos);
			}
		}
		#endregion

		#region Normales
		Vector3[] normales = new Vector3[vertices.Length];
		for (int n = 0; n < normales.Length; n++)
		{
			normales[n] = normal;
		}
		#endregion

		#region UVs
		Vector2[] uvs = new Vector2[vertices.Length];
		for (int v = 0; v < resZ; v++)
		{
			for (int u = 0; u < resX; u++)
			{
				uvs[u + v * resX] = new Vector2((float)u / (resX - 1), (float)v / (resZ - 1));
			}
		}
		#endregion

		#region Triangles
		int nbFaces = (resX - 1) * (resZ - 1);
		int[] triangles = new int[nbFaces * 6];
		int t = 0;
		for (int face = 0; face < nbFaces; face++)
		{
			// Retrieve lower left corner from face ind
			int i = face % (resX - 1) + (face / (resZ - 1) * resX);

			triangles[t++] = i + resX;
			triangles[t++] = i + 1;
			triangles[t++] = i;

			triangles[t++] = i + resX;
			triangles[t++] = i + resX + 1;
			triangles[t++] = i + 1;
		}
		#endregion

		mesh.vertices = vertices;
		mesh.normals = normales;
		mesh.uv = uvs;
		mesh.triangles = triangles;

		mesh.RecalculateBounds();
		mesh.Optimize();

		return mesh;
	}
}