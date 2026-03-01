using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

public class MigrationAndSetup
{
    [MenuItem("Tools/Migrate to HDRP")]
    public static void MigrateToHDRP()
    {
        Debug.Log("Starting HDRP Migration...");

        string settingsFolder = "Assets/Settings";
        if (!AssetDatabase.IsValidFolder(settingsFolder))
            AssetDatabase.CreateFolder("Assets", "Settings");

        string hdrpAssetPath = "Assets/Settings/HDRenderPipelineAsset.asset";
        var hdrpAsset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(hdrpAssetPath);
        
        if (hdrpAsset == null)
        {
            hdrpAsset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
            AssetDatabase.CreateAsset(hdrpAsset, hdrpAssetPath);
        }

        GraphicsSettings.defaultRenderPipeline = hdrpAsset;
        GraphicsSettings.defaultRenderPipeline = hdrpAsset;
        QualitySettings.renderPipeline = hdrpAsset;
        Debug.Log("HDRP Render Pipeline Asset assigned.");

        // Ensure Vulkan is the preferred API for Linux Headless
        var linuxGraphicsAPIs = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneLinux64);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new[] { GraphicsDeviceType.Vulkan });
        Debug.Log("Set Linux Standalone Graphics API to Vulkan.");

        AssetDatabase.SaveAssets();

        Debug.Log("Migration Setup Complete.");
    }
}
