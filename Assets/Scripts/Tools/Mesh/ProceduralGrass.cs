using UnityEngine;
using UnityEngine.Rendering;

public class ProceduralGrass : MonoBehaviour
{
	[Header("Rendering Properties")]

	[Tooltip("Compute shader for generating transformation matrices.")]
	public ComputeShader _computeShader = null;

	[Tooltip("Mesh for target terrain.")]
	public Mesh _terrainMesh;
	[Tooltip("Mesh for individual grass blades.")]
	public Mesh _grassMesh;
	[Tooltip("Material for rendering each grass blade.")]
	public Material _material;

	[Space(10)]

	[Header("Lighting and Shadows")]

	[Tooltip("Should the procedural grass cast shadows?")]
	public ShadowCastingMode _castShadows = ShadowCastingMode.On;
	[Tooltip("Should the procedural grass receive shadows from other objects?")]
	public bool _receiveShadows = true;

	[Space(10)]

	[Header("Grass Blade Properties")]

	[Range(0.0f, 0.1f)]
	[Tooltip("Minimum width multiplier.")]
	public float minBladeWidth = 0.001f;
	[Range(0.0f, 0.1f)]
	[Tooltip("Maximum width multiplier.")]
	public float maxBladeWidth = 0.005f;

	[Range(0.0f, 1.0f)]
	[Tooltip("Minimum height multiplier.")]
	public float minBladeHeight = 0.02f;
	[Range(0.0f, 1.0f)]
	[Tooltip("Maximum height multiplier.")]
	public float maxBladeHeight = 0.05f;

	[Range(-1.0f, 1.0f)]
	[Tooltip("Minimum random offset in the x- and z-directions.")]
	public float minOffset = -0.05f;
	[Range(-1.0f, 1.0f)]
	[Tooltip("Maximum random offset in the x- and z-directions.")]
	public float maxOffset = 0.05f;

	private GraphicsBuffer _terrainTriangleBuffer;
	private GraphicsBuffer _terrainVertexBuffer;

	private GraphicsBuffer _transformMatrixBuffer;

	private GraphicsBuffer _grassTriangleBuffer;
	private GraphicsBuffer _grassVertexBuffer;
	private GraphicsBuffer _grassUVBuffer;

	private Bounds _bounds;
	private MaterialPropertyBlock _materialProperties;

	private int _kernel;
	private uint _threadGroupSize;
	private int _terrainTriangleCount = 0;

	private bool _running = false;

	public void SetTerrain(in Rect terrainSize)
	{
		Debug.Log(terrainSize.height + "," + terrainSize.width);
		_terrainMesh = ProceduralMesh.CreatePlane(
			terrainSize.height, terrainSize.width, Vector3.up,
			100, 100);

		Debug.Log(_terrainMesh.bounds.size);
	}

	public void SetTerrain(in Transform terrain)
	{
		_terrainMesh = terrain?.GetComponentInChildren<MeshFilter>().sharedMesh;
	}

	public void SetTerrain(in GameObject terrain)
	{
		SetTerrain(terrain.transform);
	}

	private void Awake()
	{
		_computeShader = Resources.Load<ComputeShader>("Shader/ProceduralGrass");

		_material = Resources.Load<Material>("Materials/ProceduralGrass");

		_grassMesh = Resources.Load<Mesh>("Meshes/GrassBlade");

		_materialProperties = new MaterialPropertyBlock();
	}

	private void PrepareTerrain()
	{
		// Terrain data for the compute shader.
		var terrainVertices = _terrainMesh.vertices;
		_terrainVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainVertices.Length, sizeof(float) * 3);
		_terrainVertexBuffer.SetData(terrainVertices);

		int[] terrainTriangles = _terrainMesh.triangles;
		_terrainTriangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, terrainTriangles.Length, sizeof(int));
		_terrainTriangleBuffer.SetData(terrainTriangles);

		_terrainTriangleCount = terrainTriangles.Length / 3;

		_computeShader.SetBuffer(_kernel, "_TerrainPositions", _terrainVertexBuffer);
		_computeShader.SetBuffer(_kernel, "_TerrainTriangles", _terrainTriangleBuffer);

		_bounds = _terrainMesh.bounds;
		_bounds.center += transform.position;
		_bounds.Expand(maxBladeHeight);

		// Set up buffer for the grass blade transformation matrices.
		_transformMatrixBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, _terrainTriangleCount, sizeof(float) * 16);
		_computeShader.SetBuffer(_kernel, "_TransformMatrices", _transformMatrixBuffer);

		// Bind buffers to a MaterialPropertyBlock which will get used for the draw call.
		_materialProperties.SetBuffer("_TransformMatrices", _transformMatrixBuffer);
	}

	private void PrepareGrass()
	{
		// Grass data for RenderPrimitives.
		var grassVertices = _grassMesh.vertices;
		_grassVertexBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassVertices.Length, sizeof(float) * 3);
		_grassVertexBuffer.SetData(grassVertices);
		_materialProperties.SetBuffer("_Positions", _grassVertexBuffer);

		var grassTriangles = _grassMesh.triangles;
		_grassTriangleBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassTriangles.Length, sizeof(int));
		_grassTriangleBuffer.SetData(grassTriangles);

		var grassUVs = _grassMesh.uv;
		if (grassUVs.Length > 0)
		{
			_grassUVBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, grassUVs.Length, sizeof(float) * 2);
			_grassUVBuffer.SetData(grassUVs);
			_materialProperties.SetBuffer("_UVs", _grassUVBuffer);
		}
	}

	public void Prepare()
	{
		_kernel = _computeShader.FindKernel("CalculateBladePositions");

		PrepareTerrain();
		PrepareGrass();
	}

	public void Execute()
	{
		SetProperties();

		// Run the compute shader's _kernel function.
		_computeShader.GetKernelThreadGroupSizes(_kernel, out _threadGroupSize, out var _y, out var _z);
		var threadGroups = Mathf.CeilToInt(_terrainTriangleCount / _threadGroupSize);
		_computeShader.Dispatch(_kernel, threadGroups, 1, 1);

		_running = true;
	}

	private void SetProperties()
	{
		// Bind variables to the compute shader.
		_computeShader.SetMatrix("_TerrainObjectToWorld", transform.localToWorldMatrix);
		_computeShader.SetInt("_TerrainTriangleCount", _terrainTriangleCount);
		_computeShader.SetFloat("_MinBladeWidth", minBladeWidth);
		_computeShader.SetFloat("_MaxBladeWidth", maxBladeWidth);
		_computeShader.SetFloat("_MinBladeHeight", minBladeHeight);
		_computeShader.SetFloat("_MaxBladeHeight", maxBladeHeight);
		_computeShader.SetFloat("_MinOffset", minOffset);
		_computeShader.SetFloat("_MaxOffset", maxOffset);
	}

	// Run a single draw call to render all the grass blade meshes each frame.
	private void LateUpdate()
	{
		if (_running)
		{
			Graphics.DrawProcedural(
				_material,
				_bounds,
				MeshTopology.Triangles,
				_grassTriangleBuffer,
				_grassTriangleBuffer.count,
				instanceCount: _terrainTriangleCount*10,
				properties: _materialProperties,
				castShadows: _castShadows,
				receiveShadows: _receiveShadows);
		}
	}

	private void OnDestroy()
	{
		_terrainTriangleBuffer.Dispose();
		_terrainVertexBuffer.Dispose();
		_transformMatrixBuffer.Dispose();

		_grassTriangleBuffer.Dispose();
		_grassVertexBuffer.Dispose();
		_grassUVBuffer.Dispose();
	}

	private void OnValidate()
	{
		if (_running)
		{
			Execute();
		}
	}
}