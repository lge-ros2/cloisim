/*
 * Copyright (c) 2020 LG Electronics Inc.
 *
 * SPDX-License-Identifier: MIT
 */
using UnityEngine;

public class SDF2Unity
{
	public static Vector3 GetPosition(double x, double y, double z)
	{
		var pos = new Vector3();
		pos.x = (float)x;
		pos.y = (float)z;
		pos.z = (float)y;

		return pos;
	}

	public static Vector3 GetPosition(SDF.Vector3<double> value)
	{
		var pos = new Vector3();
		pos.x = (float)(value.X);
		pos.y = (float)(value.Z);
		pos.z = (float)(value.Y);

		return pos;
	}

	public static Quaternion GetRotation(SDF.Quaternion<double> value)
	{
		var roll = Mathf.Rad2Deg * (float)(value.Roll);
		var pitch = Mathf.Rad2Deg * (float)(value.Yaw);
		var yaw = Mathf.Rad2Deg * (float)(value.Pitch);

		return Quaternion.Euler(-roll, -pitch, -yaw);
	}

	public static Vector3 GetScale(SDF.Vector3<double> value)
	{
		return GetPosition(value);
	}

	public static Vector3 GetScale(double radius)
	{
		var pos = new Vector3();
		pos.x = (float)(radius);// * 2.0f;
		pos.y = pos.x;
		pos.z = pos.y;
		return pos;
	}

	public static void LoadObjMesh(in GameObject targetObject, in string objPath, in string mtlPath)
	{
		var loadedObject = new Dummiesman.OBJLoader().Load(objPath, mtlPath);

		var meshRotation = new Vector3(90f, 180f, 0f);
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
		const string commonShader = "Standard (Specular setup)";

		var multipleMesh = Parabox.Stl.Importer.Import(objPath, Parabox.Stl.CoordinateSpace.Right, Parabox.Stl.UpAxis.Z, true);

		var meshRotation = new Vector3(0.0f, 90.0f, 0.0f);
		for (int i = 0; i < multipleMesh.Length; i++)
		{
			multipleMesh[i].name = "Mesh-" + i;

			var meshObject = new GameObject(multipleMesh[i].name);
			meshObject.hideFlags |= HideFlags.NotEditable;

			var meshFilter = meshObject.AddComponent<MeshFilter>();
			meshFilter.mesh = multipleMesh[i];
			meshFilter.transform.Rotate(meshRotation);

			var meshRenderer = meshObject.AddComponent<MeshRenderer>();

			var newMaterial = new Material(Shader.Find(commonShader));
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