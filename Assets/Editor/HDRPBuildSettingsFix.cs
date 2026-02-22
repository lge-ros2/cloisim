using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class HDRPBuildSettingsFix
{
    [MenuItem("Build/Fix Linux Graphics API")]
    public static void FixLinuxGraphicsAPI()
    {
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64, new GraphicsDeviceType[] { GraphicsDeviceType.Vulkan });
        Debug.Log("Set Linux Standalone Graphics API exclusively to Vulkan. Auto Graphic APIs disabled.");
        AssetDatabase.SaveAssets(); // Ensure it saves!
    }
}
