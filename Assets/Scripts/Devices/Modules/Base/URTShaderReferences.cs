/*
 * Copyright (c) 2026 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */

using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// A ScriptableObject to hold direct references to URT compute shaders,
/// ensuring they are included in player builds.
/// </summary>
[Preserve]
[CreateAssetMenu(fileName = "URTShaderReferences", menuName = "URT/Shader References")]
public class URTShaderReferences : ScriptableObject
{
	public ComputeShader geometryPoolKernels;
	public ComputeShader copyBuffer;
	public ComputeShader copyPositions;
	public ComputeShader bitHistogram;
	public ComputeShader blockReducePart;
	public ComputeShader blockScan;
	public ComputeShader buildHlbvh;
	public ComputeShader restructureBvh;
	public ComputeShader scatter;
}