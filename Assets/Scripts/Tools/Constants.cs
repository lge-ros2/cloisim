/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

/// <summary>
/// Central definitions for the Unity custom tags used across the simulator.
/// Keeping these as named constants prevents silent runtime breakage when a
/// tag is renamed in ProjectSettings (CompareTag would otherwise fail).
/// </summary>
public static class TagNames
{
	public const string Model = "Model";
	public const string Props = "Props";
	public const string Road = "Road";
	public const string Actor = "Actor";
	public const string Link = "Link";
	public const string Sensor = "Sensor";
	public const string Visual = "Visual";
	public const string Collision = "Collision";
	public const string Geometry = "Geometry";
	public const string Light = "Light";
	public const string Marker = "Marker";
}

/// <summary>
/// Central definitions for the Unity layer names used with LayerMask/NameToLayer.
/// Kept as constants so layer renames in ProjectSettings stay consistent.
/// </summary>
public static class LayerNames
{
	public const string Default = "Default";
	public const string Plane = "Plane";
	public const string UI = "UI";
	public const string IgnoreRaycast = "Ignore Raycast";
	public const string TransparentFX = "TransparentFX";
	public const string Water = "Water";
	public const string Cloth = "Cloth";
	public const string Visualization = "Visualization";
}

/// <summary>
/// Central definitions for URP shader property names.
/// </summary>
public static class ShaderProps
{
	public const string BaseMap = "_BaseMap";
	public const string BaseColor = "_BaseColor";
	public const string Color = "_Color";
	public const string BumpMap = "_BumpMap";
	public const string EmissionMap = "_EmissionMap";
	public const string EmissionColor = "_EmissionColor";
	public const string MetallicGlossMap = "_MetallicGlossMap";
	public const string SpecGlossMap = "_SpecGlossMap";
	public const string OcclusionMap = "_OcclusionMap";
	public const string Smoothness = "_Smoothness";
	public const string SmoothnessTextureChannel = "_SmoothnessTextureChannel";
	public const string WorkflowMode = "_WorkflowMode";
}

/// <summary>
/// Central definitions for the simulation WebSocket service paths and default
/// port. Using the same constants for registration and teardown prevents a typo
/// in one path from silently breaking the /control or /markers API.
/// </summary>
public static class SimServicePaths
{
	public const string Control = "/control";
	public const string Markers = "/markers";
	public const int DefaultPort = 8080;
}

