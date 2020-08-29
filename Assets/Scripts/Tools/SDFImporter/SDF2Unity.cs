/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;

public class SDF2Unity
{
	private static string commonShaderName = "Standard (Specular setup)";
	public static Shader commonShader = Shader.Find(commonShaderName);

	public static Vector3 GetPosition(in double x, in double y, in double z)
	{
		return new Vector3(-(float)y, (float)z, (float)x);
	}

	public static Vector3 GetPosition(in SDF.Vector3<double> value)
	{
		return GetPosition(value.X, value.Y, value.Z);
	}

	public static Quaternion GetRotation(in SDF.Quaternion<double> value)
	{
		var roll = Mathf.Rad2Deg * (float)value.Pitch;
		var pitch = Mathf.Rad2Deg * -(float)value.Yaw;
		var yaw = Mathf.Rad2Deg * -(float)value.Roll;
		return Quaternion.Euler(roll, pitch, yaw);
	}

	public static Vector3 GetScale(in SDF.Vector3<double> value)
	{
		return GetPosition(value);
	}

	public static Vector3 GetScale(in double radius)
	{
		return new Vector3((float)radius, (float)radius, (float)radius);
	}

	public static void LoadObjMesh(in GameObject targetObject, in string objPath, in string mtlPath)
	{
		var loadedObject = new Dummiesman.OBJLoader().Load(objPath, mtlPath);

		var meshRotation = new Vector3(90f, 90f, 0f);
		foreach (var meshFilter in loadedObject.GetComponentsInChildren<MeshFilter>())
		{
			var meshObject = meshFilter.gameObject;
			meshObject.hideFlags |= HideFlags.NotEditable;

			meshFilter.transform.localScale = -meshFilter.transform.localScale;
			meshFilter.transform.Rotate(meshRotation);

			var child = meshObject.transform;
			child.SetParent(targetObject.transform, false);
		}

		GameObject.Destroy(loadedObject);
	}

	public static void LoadStlMesh(in GameObject targetObject, in string objPath)
	{


		var multipleMesh = Parabox.Stl.Importer.Import(objPath, Parabox.Stl.CoordinateSpace.Right, Parabox.Stl.UpAxis.Z, true);

		for (int i = 0; i < multipleMesh.Length; i++)
		{
			multipleMesh[i].name = "Mesh-" + i;

			var meshObject = new GameObject(multipleMesh[i].name);
			meshObject.hideFlags |= HideFlags.NotEditable;

			var meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = multipleMesh[i];

			var meshRenderer = meshObject.AddComponent<MeshRenderer>();

			var newMaterial = new Material(commonShader);
			newMaterial.name = multipleMesh[i].name;
			meshRenderer.material = newMaterial;

			var child = meshObject.transform;
			child.SetParent(targetObject.transform, false);
		}
	}
	public static bool CheckTopModel(in GameObject targetObject)
	{
		return CheckTopModel(targetObject.transform);
	}

	public static bool CheckTopModel(in Transform targetTransform)
	{
		return targetTransform.parent.Equals(targetTransform.root);
	}
}