using UnityEditor;
using UnityEngine;

public class GUIBuilder
{
    public static void Build()
    {
        string[] scenes = { "Assets/Scenes/MainScene.unity" };
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = "/home/nav/workspace/cloisim/build/linux_x86_64/CLOiSim.x86_64";
        buildPlayerOptions.target = BuildTarget.StandaloneLinux64;
        buildPlayerOptions.subtarget = (int)StandaloneBuildSubtarget.Player;
        buildPlayerOptions.options = BuildOptions.StrictMode;

        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Standalone GUI Build finished");
    }
}
