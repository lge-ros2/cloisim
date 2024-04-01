#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Splines;
#endif

using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Splines;
using Interpolators = UnityEngine.Splines.Interpolators;

namespace Unity.Splines
{
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	[RequireComponent(typeof(SplineContainer))]
	[RequireComponent(typeof(MeshRenderer), typeof(MeshCollider), typeof(MeshFilter))]
	public class LoftRoadGenerator : MonoBehaviour
	{
		[SerializeField]
		List<SplineData<float>> m_Widths = new List<SplineData<float>>();

		public List<SplineData<float>> Widths
		{
			get
			{
				foreach (var width in m_Widths)
				{
					if (width.DefaultValue == 0)
						width.DefaultValue = 1f;
				}

				return m_Widths;
			}
		}

		[SerializeField]
		SplineContainer m_Spline;

		public SplineContainer Container
		{
			get
			{
				if (m_Spline == null)
					m_Spline = GetComponent<SplineContainer>();

				return m_Spline;
			}
			set => m_Spline = value;
		}

		[SerializeField]
		int m_SegmentsPerMeter = 1;

		[SerializeField]
		Mesh m_Mesh;

		[SerializeField]
		Material m_Material;

		[SerializeField]
		float m_TextureScale = 1;

		// public IReadOnlyList<Spline> splines => LoftSplines;

		public IReadOnlyList<Spline> LoftSplines
		{
			get
			{
				if (m_Spline == null)
					m_Spline = GetComponent<SplineContainer>();

				if (m_Spline == null)
				{
					Debug.LogError("Cannot loft road mesh because Spline reference is null");
					return null;
				}

				return m_Spline.Splines;
			}
		}

		public Material Material
		{
			get => m_Material;
			set
			{
				m_Material = value;
				GetComponent<MeshRenderer>().sharedMaterial = m_Material;
			}
		}

		public int SegmentsPerMeter => Mathf.Min(10, Mathf.Max(1, m_SegmentsPerMeter));

		private List<Vector3> m_Positions = new List<Vector3>();
		private List<Vector3> m_Normals = new List<Vector3>();
		private List<Vector2> m_Textures = new List<Vector2>();
		private List<int> m_Indices = new List<int>();


		public void OnEnable()
		{
			m_Mesh = new Mesh();
			m_Mesh.name = "GeneratedRoadMesh";

			var meshCollider = gameObject.GetComponent<MeshCollider>();
			if (meshCollider == null)
			{
				Debug.LogError("Cannot load mesh collider because  it is null");
				return;
			}

			meshCollider.sharedMesh = m_Mesh;

			if (m_Spline == null)
				m_Spline = GetComponent<SplineContainer>();

			LoftAllRoads();

#if UNITY_EDITOR
			EditorSplineUtility.AfterSplineWasModified += OnAfterSplineWasModified;
			EditorSplineUtility.RegisterSplineDataChanged<float>(OnAfterSplineDataWasModified);
			Undo.undoRedoPerformed += LoftAllRoads;
#endif

			SplineContainer.SplineAdded += OnSplineContainerAdded;
			SplineContainer.SplineRemoved += OnSplineContainerRemoved;
			SplineContainer.SplineReordered += OnSplineContainerReordered;
			Spline.Changed += OnSplineChanged;
		}

		public void OnDisable()
		{
#if UNITY_EDITOR
			EditorSplineUtility.AfterSplineWasModified -= OnAfterSplineWasModified;
			EditorSplineUtility.UnregisterSplineDataChanged<float>(OnAfterSplineDataWasModified);
			Undo.undoRedoPerformed -= LoftAllRoads;
#endif

			if (m_Mesh != null)
#if  UNITY_EDITOR
				DestroyImmediate(m_Mesh);
#else
				Destroy(m_Mesh);
#endif

			SplineContainer.SplineAdded -= OnSplineContainerAdded;
			SplineContainer.SplineRemoved -= OnSplineContainerRemoved;
			SplineContainer.SplineReordered -= OnSplineContainerReordered;
			Spline.Changed -= OnSplineChanged;
		}

		void OnSplineContainerAdded(SplineContainer container, int index)
		{
			if (container != m_Spline)
				return;

			if (m_Widths.Count < LoftSplines.Count)
			{
				var delta = LoftSplines.Count - m_Widths.Count;
				for (var i = 0; i < delta; i++)
				{
#if  UNITY_EDITOR
					Undo.RecordObject(this, "Modifying Widths SplineData");
#endif
					m_Widths.Add(new SplineData<float>() { DefaultValue = 1f });
				}
			}

			LoftAllRoads();
		}

		void OnSplineContainerRemoved(SplineContainer container, int index)
		{
			if (container != m_Spline)
				return;

			if (index < m_Widths.Count)
			{
#if UNITY_EDITOR
				Undo.RecordObject(this, "Modifying Widths SplineData");
#endif
				m_Widths.RemoveAt(index);
			}

			LoftAllRoads();
		}

		void OnSplineContainerReordered(SplineContainer container, int previousIndex, int newIndex)
		{
			if (container != m_Spline)
				return;

			LoftAllRoads();
		}

		void OnAfterSplineWasModified(Spline s)
		{
			if (LoftSplines == null)
				return;

			foreach (var spline in LoftSplines)
			{
				if (s == spline)
				{
					LoftAllRoads();
					break;
				}
			}
		}

		void OnSplineChanged(Spline spline, int knotIndex, SplineModification modification)
		{
			OnAfterSplineWasModified(spline);
		}

		void OnAfterSplineDataWasModified(SplineData<float> splineData)
		{
			foreach (var width in m_Widths)
			{
				if (splineData == width)
				{
					LoftAllRoads();
					break;
				}
			}
		}

		public void LoftAllRoads()
		{
			m_Mesh.Clear();
			m_Positions.Clear();
			m_Normals.Clear();
			m_Textures.Clear();
			m_Indices.Clear();

			for (var i = 0; i < LoftSplines.Count; i++)
				Loft(LoftSplines[i], i);

			m_Mesh.SetVertices(m_Positions);
			m_Mesh.SetNormals(m_Normals);
			m_Mesh.SetUVs(0, m_Textures);
			m_Mesh.subMeshCount = 1;
			m_Mesh.SetIndices(m_Indices, MeshTopology.Triangles, 0);
			m_Mesh.UploadMeshData(false);

			m_Mesh.Optimize();

			GetComponent<MeshFilter>().sharedMesh = m_Mesh;
		}

		private void Loft(Spline spline, in int widthDataIndex)
		{
			if (spline == null || spline.Count < 2)
				return;

			m_Mesh.Clear();

			var length = spline.GetLength();

			if (length <= 0.001f)
				return;

			var segmentsPerLength = SegmentsPerMeter * length;
			var segments = Mathf.CeilToInt(segmentsPerLength);
			var segmentStepT = (1f / SegmentsPerMeter) / length;
			var steps = segments + 1;
			var vertexCount = steps * 2;
			var triangleCount = segments * 6;
			var prevVertexCount = m_Positions.Count;

			m_Positions.Capacity += vertexCount;
			m_Normals.Capacity += vertexCount;
			m_Textures.Capacity += vertexCount;
			m_Indices.Capacity += triangleCount;

			var t = 0f;
			for (int i = 0; i < steps; i++)
			{
				SplineUtility.Evaluate(spline, t, out var pos, out var dir, out var up);

				// If dir evaluates to zero (linear or broken zero length tangents?)
				// then attempt to advance forward by a small amount and build direction to that point
				if (math.length(dir) == 0)
				{
					var nextPos = spline.GetPointAtLinearDistance(t, 0.01f, out _);
					dir = math.normalizesafe(nextPos - pos);

					if (math.length(dir) == 0)
					{
						nextPos = spline.GetPointAtLinearDistance(t, -0.01f, out _);
						dir = -math.normalizesafe(nextPos - pos);
					}

					if (math.length(dir) == 0)
						dir = new float3(0, 0, 1);
				}

				var scale = transform.lossyScale;
				var tangent = math.normalizesafe(math.cross(up, dir)) * new float3(1f / scale.x, 1f / scale.y, 1f / scale.z);

				var w = 1f;
				if (widthDataIndex < m_Widths.Count)
				{
					w = m_Widths[widthDataIndex].DefaultValue;
					if (m_Widths[widthDataIndex] != null && m_Widths[widthDataIndex].Count > 0)
					{
						w = m_Widths[widthDataIndex].Evaluate(spline, t, PathIndexUnit.Normalized, new Interpolators.LerpFloat());
						w = math.clamp(w, .001f, 10000f);
					}
				}

				m_Positions.Add(pos - (tangent * w));
				m_Positions.Add(pos + (tangent * w));
				m_Normals.Add(up);
				m_Normals.Add(up);
				m_Textures.Add(new Vector2(0f, t * m_TextureScale));
				m_Textures.Add(new Vector2(1f, t * m_TextureScale));

				t = math.min(1f, t + segmentStepT);
			}

			for (int i = 0, n = prevVertexCount; i < triangleCount; i += 6, n += 2)
			{
				m_Indices.Add((n + 2) % (prevVertexCount + vertexCount));
				m_Indices.Add((n + 1) % (prevVertexCount + vertexCount));
				m_Indices.Add((n + 0) % (prevVertexCount + vertexCount));
				m_Indices.Add((n + 2) % (prevVertexCount + vertexCount));
				m_Indices.Add((n + 3) % (prevVertexCount + vertexCount));
				m_Indices.Add((n + 1) % (prevVertexCount + vertexCount));
			}
		}
	}
}
