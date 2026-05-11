---
name: setup-material-texture
description: "Create and configure URP materials from SDF material definitions or Assimp imports. Use when: setting up base color, emission, specular, or normal maps, switching transparency modes, debugging texture loading, converting materials to SpeedTree shader, customizing URP material properties."
---

# Setup Material and Texture Pipeline

Reference for creating and configuring Universal Render Pipeline (URP) materials in CLOiSim, covering both SDF-defined materials and Assimp-imported materials.

## When to Use

- Creating a new URP material from SDF `<material>` definitions
- Debugging missing or incorrect textures on imported models
- Adding transparency or emission to materials
- Converting vegetation materials to SpeedTree shader
- Understanding the Assimp material property → Unity material mapping
- Customizing the texture search path resolution

## Architecture

Two material creation paths converge on the same URP property setters:

```
Path 1: SDF Material                Path 2: Assimp Material
  ├─ <material>                       ├─ Assimp.Material properties
  │  ├─ <diffuse>                     │  ├─ ColorDiffuse
  │  ├─ <emissive>                    │  ├─ ColorEmissive
  │  ├─ <specular>                    │  ├─ TextureDiffuse
  │  └─ <normal_map>                  │  └─ TextureNormal
  │                                   │
  └─ SDF2Unity.Material.*()           └─ sceneMaterials.ToUnity()
                    │                              │
                    ▼                              ▼
              CreateMaterial()  ← base material creation
                    │
                    ▼
              Set*() extension methods
              ├─ SetBaseColor()
              ├─ SetEmission()
              ├─ SetSpecular()
              ├─ SetNormalMap()
              ├─ SetTransparent() / SetOpaque()
              └─ ConvertToSpeedTree()
```

## Base Material Creation

All materials start from `SDF2Unity.CreateMaterial()`:

```csharp
public static Material CreateMaterial(in string materialName = "")
// Creates a URP "Custom/URP/Simple Lit" material with defaults:
//   Render Queue: Geometry
//   Cull Mode:    Back (front-face rendering)
//   ZWrite:       Enabled
//   BaseColor:    White
//   Smoothness:   0 (fully rough)
//   EnvironmentReflections: 0.5
//   GPU Instancing: ON (_INSTANCING_ON + _DOTS_INSTANCING_ON)
//   Receive Shadows: ON
```

## Property Setters

### Base Color

```csharp
public static void SetBaseColor(this Material target, Color color)
// Sets _BaseColor property
// Auto-switches transparency:
//   color.a < 1.0 → SetTransparent()
//   color.a == 1.0 → SetOpaque()
```

### Transparency

```csharp
// Transparent mode
public static void SetTransparent(this Material target)
// RenderType tag: "Transparent"
// _Surface: 1
// _SrcBlend: SrcAlpha
// _DstBlend: OneMinusSrcAlpha
// Render Queue: Transparent

// Opaque mode
public static void SetOpaque(this Material target)
// RenderType tag: "Opaque"
// _Surface: 0
// Render Queue: Geometry
```

### Emission

```csharp
public static void SetEmission(this Material target, Color color)
// _EmissionColor: color
// GlobalIllumination: None
// Enables _EMISSION shader keyword
```

### Specular

```csharp
public static void SetSpecular(this Material target, Color color)
// _SpecColor: color.rgb
// _Smoothness: color.a (smoothness packed in alpha)
// _SmoothnessSource: 0 (from specular alpha)
// Enables _SPECGLOSSMAP shader keyword
```

### Normal Map

```csharp
public static void SetNormalMap(this Material target, in string normalMapPath)
// Loads texture via MeshLoader.GetTexture()
// Sets _BumpMap texture
// Enables _NORMALMAP shader keyword
```

### SpeedTree Conversion (Vegetation)

```csharp
public static void ConvertToSpeedTree(this Material target)
// Swaps shader: "Custom/URP/Simple Lit" → "Universal Render Pipeline/Nature/SpeedTree8"
// Remaps: _BaseMap → _MainTex (with scale/offset)
// Sets _TwoSided: 0
// Disables EFFECT_BILLBOARD keyword
```

## Assimp Material Property Mapping

When importing meshes via Assimp, material properties are mapped as follows:

```csharp
// Color properties
HasColorDiffuse    → SetBaseColor(diffuseColor)
                     // Note: alpha forced to 1.0 (Blender FBX override)
HasOpacity         → alpha = opacity; SetTransparent() if < 1.0
HasColorEmissive   → SetEmission(emissiveColor)
HasColorSpecular   → SetColor("_SpecColor", specularColor)
HasShininess       → Smoothness = 1.0 - Clamp01(Shininess)
HasReflectivity    → Smoothness = 1.0 - Clamp01(Reflectivity)

// Texture properties
HasTextureDiffuse  → _BaseMap texture
HasTextureNormal   → _BumpMap + _NORMALMAP keyword
HasBumpScaling     → _BumpScale (normal map strength)
HasTextureSpecular → _SpecGlossMap + _SPECGLOSSMAP keyword
HasTextureEmissive → _EmissionMap + _EMISSION keyword
HasTextureOpacity  → logged only (not applied)
```

## Texture Loading Pipeline

### Search Path Resolution

Textures are resolved through a prioritized search:

```csharp
// 1. Check embedded textures first (FBX "*{index}" convention)
// 2. Normalize path:
//    - Strip "model://" prefix
//    - Convert \\ to /
//    - Strip "//" Blender FBX prefix
// 3. Search directories (relative to mesh file):
""                  // Same directory
"../"               // Parent
"../../"            // Grandparent
"textures/"         // textures/ subfolder
"../textures/"      // Sibling textures/
"../materials/"     // Sibling materials/
"materials/"        // materials/ subfolder
// 4. Fallback: filename-only (strip all subdirectories)
```

### Format-Specific Loading

```csharp
// TGA files — custom loader
TextureUtil.LoadTGA(fileStream)
// Returns Texture2D from TGA binary data

// All other formats (PNG, JPG, BMP, etc.)
texture.LoadImage(File.ReadAllBytes(path))
// Unity's built-in image decoder with mipmap support
```

### Post-Load Optimization

```csharp
// Applied to all loaded textures:
texture.filterMode = FilterMode.Trilinear;
texture.anisoLevel = 4;

// Compress if dimensions are multiples of 4
if (width % 4 == 0 && height % 4 == 0)
	texture.Compress(true);

// Free CPU-side copy, keep GPU memory
texture.Apply(false, true);

// Prevent premature unloading
texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
```

## URP Shader Property Reference

| Property | Type | Default | Purpose |
|----------|------|---------|---------|
| `_BaseColor` | Color | White | Albedo/diffuse color |
| `_BaseMap` | Texture2D | — | Albedo texture |
| `_BumpMap` | Texture2D | — | Normal map |
| `_BumpScale` | Float | 1.0 | Normal map strength |
| `_EmissionColor` | Color | Black | Emission color |
| `_EmissionMap` | Texture2D | — | Emission texture |
| `_SpecColor` | Color | — | Specular color |
| `_SpecGlossMap` | Texture2D | — | Specular/gloss map |
| `_Smoothness` | Float | 0 | Surface smoothness (0=rough) |
| `_SmoothnessSource` | Int | 0 | 0=specular alpha, 1=albedo alpha |
| `_Cull` | Int | 2 (Back) | Face culling mode |
| `_Surface` | Int | 0 | 0=opaque, 1=transparent |
| `_SrcBlend` | Int | 1 | Source blend factor |
| `_DstBlend` | Int | 0 | Destination blend factor |
| `_ZWrite` | Int | 1 | Depth write enable |

## Procedure: Adding a Custom Material Property

1. Identify the URP shader property name (check `Custom/URP/Simple Lit` shader source)
2. Create an extension method in `SDF2Unity.Material.cs`:
   ```csharp
   public static void SetMyProperty(this Material target, float value)
   {
       target.SetFloat("_MyProperty", value);
       // Enable shader keyword if needed:
       // target.EnableKeyword("_MY_FEATURE");
   }
   ```
3. Wire it into the Assimp material conversion or SDF material import path

## Common Issues

| Symptom | Cause | Fix |
|---------|-------|-----|
| Model is all white | Textures not found | Check texture search paths; verify file exists |
| Model is invisible | Alpha is 0 | Check `HasOpacity`; Blender FBX may export alpha=0 |
| Model is too shiny | Smoothness too high | Check `Shininess` / `Reflectivity` mapping (inverted) |
| Normal map looks flat | `_NORMALMAP` keyword not enabled | Ensure `EnableKeyword("_NORMALMAP")` is called |
| Transparent model has z-fighting | Wrong render queue | Verify `SetTransparent()` sets queue to Transparent |
| Vegetation renders single-sided | Not using SpeedTree shader | Call `ConvertToSpeedTree()` for tree/plant materials |
| Texture is blurry | Not compressed or wrong filter | Check `filterMode` and `anisoLevel` in post-load |
| TGA texture crashes | Unsupported TGA variant | Check `TextureUtil.LoadTGA()` format support |
