using UnityEngine;
using UnityEditor;

public class CreateHDRPMaterial {
    public static void Create() {
        // Ensures the directory exists
        if (!AssetDatabase.IsValidFolder("Assets/Resources/Materials")) {
            AssetDatabase.CreateFolder("Assets/Resources", "Materials");
        }
        
        Material mat = new Material(Shader.Find("HDRP/Lit"));
        AssetDatabase.CreateAsset(mat, "Assets/Resources/Materials/HDRPBase.mat");
        AssetDatabase.SaveAssets();
        Debug.Log("[CreateHDRPMaterial] Successfully created Assets/Resources/Materials/HDRPBase.mat");
    }
}
