using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class HeadlessBuilder
{
    /// <summary>
    /// Builds a standalone player for headless use (NOT a dedicated server).
    /// The dedicated server subtarget strips rendering, which breaks GPU sensors
    /// (camera, lidar via AsyncGPUReadback). Instead, we build a regular player
    /// and run it with "-batchmode -force-vulkan" for headless EGL/Vulkan rendering.
    /// </summary>
    public static void Build()
    {
        // Ensure Vulkan is the graphics API for headless Linux
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneLinux64, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneLinux64,
            new[] { GraphicsDeviceType.Vulkan });

        string[] scenes = { "Assets/Scenes/MainScene.unity" };

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = "/home/nav/workspace/cloisim/build/linux_headless/CLOiSim.x86_64";
        buildPlayerOptions.target = BuildTarget.StandaloneLinux64;
        buildPlayerOptions.subtarget = (int)StandaloneBuildSubtarget.Player;
        buildPlayerOptions.options = BuildOptions.StrictMode;

        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Headless Player Build finished (run with: -batchmode -force-vulkan)");
    }
}
