---
name: add-mesh-import
description: "Import 3D mesh files into CLOiSim via the Assimp pipeline, including collision generation and procedural meshes. Use when: adding a new mesh file format, debugging mesh import issues, generating convex hull colliders via VHACD, creating procedural geometry, fixing texture loading or UV mapping problems."
---

# Add Mesh Import

Reference and procedure for importing 3D mesh files through CLOiSim's Assimp-based pipeline, generating collision geometry via VHACD, and creating procedural meshes.

## When to Use

- Importing a new 3D file format (OBJ, DAE, STL, FBX)
- Debugging mesh import failures (missing textures, wrong orientation, degenerate geometry)
- Generating convex hull colliders for complex meshes
- Creating procedural geometry (box, cylinder, sphere, heightmap)
- Fixing face winding, UV channels, or vertex color issues
- Understanding the mesh → material → texture loading pipeline

## Architecture

```
MeshLoader.CreateMeshObject(meshPath)
  │
  ├─ Assimp.Common: GetScene()
  │  ├─ File validation (extension check)
  │  ├─ Assimp post-processing flags
  │  └─ Per-format rotation correction
  │
  ├─ LoadEmbeddedTextures()  (FBX)
  │
  ├─ sceneMaterials.ToUnity()
  │  ├─ Color: Diffuse → BaseColor, Emissive → Emission
  │  ├─ Textures: Diffuse → _BaseMap, Normal → _BumpMap, etc.
  │  └─ Transparency: Opacity → alpha blending
  │
  ├─ sceneMeshes.ToUnity()
  │  ├─ Vertex setup (UInt16/UInt32 index format)
  │  ├─ UV channels (up to 8)
  │  ├─ Face winding reversal (right→left hand)
  │  └─ Normal/tangent calculation
  │
  └─ ToUnityMeshObject()
     ├─ Recursive node traversal
     ├─ MeshFilter + MeshRenderer per node
     └─ Material assignment by submesh index
```

## File Format Support

| Format | Extension | Rotation Correction | Notes |
|--------|-----------|-------------------|-------|
| Collada | `.dae` | Euler(0, -90, 0) | Common for ROS/Gazebo models |
| Wavefront | `.obj` | Euler(90, -90, 0) | No animation support |
| STL | `.stl` | Euler(90, -90, 0) | Mesh-only, no materials |
| FBX | `.fbx` | None (identity) | Supports embedded textures |

## Procedure

### 1. Loading a Mesh File

Entry point is `MeshLoader.CreateMeshObject()`:

```csharp
// Basic mesh loading
var meshObject = MeshLoader.CreateMeshObject(meshPath);

// With sub-mesh selection (specific mesh from multi-mesh file)
var meshObject = MeshLoader.CreateMeshObject(meshPath, subMeshName);
```

### 2. Assimp Scene Loading

`GetScene()` handles file validation, post-processing, and format-specific rotation:

```csharp
// Post-processing flags applied to all formats:
// - OptimizeMeshes
// - SplitLargeMeshes
// - MakeLeftHanded (coordinate system conversion)
// - Triangulate
// - SortByPrimitiveType
// - GenerateNormals (if missing)

// Per-format rotation correction:
// OBJ/STL: Euler(90, -90, 0) — compensates Z-up → Y-up
// DAE:     Euler(0, -90, 0)  — compensates axis orientation
// FBX:     Identity (no rotation)
```

### 3. Mesh Conversion to Unity

Key conversion details in `sceneMeshes.ToUnity()`:

```csharp
// Index format selection based on vertex count
mesh.indexFormat = vertexCount >= 65536
	? IndexFormat.UInt32 : IndexFormat.UInt16;

// Face winding reversal (right-hand → left-hand)
// Assimp indices [0, 1, 2] become [2, 1, 0]
triangles[triIdx + 0] = face.Indices[2];
triangles[triIdx + 1] = face.Indices[1];
triangles[triIdx + 2] = face.Indices[0];

// UV channels — up to 8 supported
for (var ch = 0; ch < mesh.TextureCoordinateChannelCount; ch++)
{
	// mesh.SetUVs(ch, uvChannel)
}

// Vertex colors — single channel only
if (mesh.VertexColorChannelCount > 0)
{
	// Uses VertexColorChannels[0]
}

// Degenerate mesh check — skip if bounds < 1e-4
if (bounds.size.magnitude < 1e-4f)
	continue; // Skip degenerate
```

### 4. Material Loading from Assimp

Materials are converted via `sceneMaterials.ToUnity()`:

```csharp
// Assimp → Unity material property mapping:
HasColorDiffuse    → SetBaseColor() (alpha forced to 1.0)
HasOpacity         → Alpha channel + SetTransparent()
HasColorEmissive   → SetEmission()
HasColorSpecular   → SetColor("_SpecColor")
HasShininess       → Smoothness = 1.0 - Shininess
HasReflectivity    → Smoothness = 1.0 - Reflectivity

// Texture mapping:
HasTextureDiffuse  → _BaseMap
HasTextureNormal   → _BumpMap + _NORMALMAP keyword
HasTextureSpecular → _SpecGlossMap + _SPECGLOSSMAP keyword
HasTextureEmissive → _EmissionMap + _EMISSION keyword
```

### 5. Texture Search Path Resolution

Textures are resolved through a multi-path search:

```csharp
// Search directories tried in order:
""              // Same directory as mesh file
"../"           // Parent directory
"../../"        // Grandparent directory
"textures/"     // textures/ subfolder
"../textures/"  // Sibling textures/ folder
"../materials/" // Sibling materials/ folder
"materials/"    // materials/ subfolder

// Path normalization:
// - Strip "model://" prefix
// - Convert \\ to /
// - Strip "//" Blender FBX prefix
// - Fallback to filename-only search (strip subdirectories)

// Format-specific loading:
// .tga → TextureUtil.LoadTGA(stream)
// Others → texture.LoadImage(bytes) with mipmaps
```

### 6. Embedded Texture Handling (FBX)

FBX files can embed textures directly:

```csharp
// Pre-cached during scene loading
LoadEmbeddedTextures(scene);
// Stored with key "*{index}" (Assimp convention)
// Retrieved before file-system search in TryLoadTexture()
```

### 7. VHACD Convex Decomposition

For collision geometry on complex meshes:

```csharp
// Apply VHACD to all mesh filters in a GameObject
VHACD.Apply(meshFilters);

// Workflow:
// 1. For each MeshFilter, decompose mesh into convex hulls
// 2. Results are cached by mesh identity (avoids redundant computation)
// 3. Skip degenerate meshes (planar bounds < 1e-4)
// 4. Add MeshCollider (convex=true) for each hull
// 5. Multiple MeshColliders per original mesh is normal
```

### 8. Procedural Mesh Creation

For SDF primitive geometries (`<box>`, `<cylinder>`, `<sphere>`, etc.):

```csharp
// Available generators in ProceduralMesh:
ProceduralMesh.CreateBox(size);
ProceduralMesh.CreateCylinder(radius, length);
ProceduralMesh.CreateSphere(radius);
ProceduralMesh.CreateEllipsoid(radii);
ProceduralMesh.CreatePlane(size);
ProceduralMesh.CreateCapsule(radius, length);
```

### 9. Heightmap Generation

For terrain from image files:

```csharp
// ProceduralHeightmap creates Unity Terrain from:
// - Heightmap image (grayscale)
// - SDF <heightmap> parameters (size, position, textures)
// Generates terrain data with proper scaling and texture layers
```

## Adding a New File Format

1. Add extension to `CheckFileSupport()` validation
2. Add rotation case in `GetRotationByFileExtension()`:
   ```csharp
   case ".myformat":
       return Quaternion.Euler(rx, ry, rz);
   ```
3. Verify Assimp supports the format (check `Assimp.AssimpContext.IsImportFormatSupported()`)
4. Test face winding — ensure triangles render correctly (may need to adjust post-processing flags)

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Mesh is rotated 90° | Wrong format rotation | Check `GetRotationByFileExtension()` for the format |
| Faces are inside-out | Winding order incorrect | Verify `MakeLeftHanded` flag or adjust triangle reversal |
| Missing textures | Search path doesn't find texture file | Add directory to search path list in `TryLoadTexture()` |
| Black/untextured model | Embedded textures not loaded | Check `LoadEmbeddedTextures()` runs before material setup |
| Mesh has holes | Degenerate faces filtered out | Lower the `1e-4` bounds threshold if needed |
| Collider doesn't match mesh | VHACD skipped degenerate submeshes | Check mesh bounds; may need finer decomposition params |
| Out of memory on large mesh | UInt16 index overflow | Verify `indexFormat` selection for meshes with 65536+ verts |
| TGA texture fails | Unsupported TGA variant | Check `TextureUtil.LoadTGA()` supports the specific TGA type |
