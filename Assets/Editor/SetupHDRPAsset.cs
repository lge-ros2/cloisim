using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Rendering;

public class SetupHDRPAsset {
    public static void Setup() {
        if (!AssetDatabase.IsValidFolder("Assets/Settings")) {
            AssetDatabase.CreateFolder("Assets", "Settings");
        }
        
        string path = "Assets/Settings/HDRenderPipelineAsset.asset";
        var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(path);
        if (asset == null) {
            asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            AssetDatabase.CreateAsset(asset, path);
        }
        
        GraphicsSettings.defaultRenderPipeline = asset;
        for (int i = 0; i < QualitySettings.names.Length; i++) {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = asset;
        }
        
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        Debug.Log("[SetupHDRPAsset] Successfully created and assigned HDRP Render Pipeline Asset.");
    }
}
