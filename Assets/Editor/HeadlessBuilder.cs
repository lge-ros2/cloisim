using UnityEditor;
using UnityEngine;

public class HeadlessBuilder
{
    public static void Build()
    {
        string[] scenes = { "Assets/Scenes/MainScene.unity" };
        
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions();
        buildPlayerOptions.scenes = scenes;
        buildPlayerOptions.locationPathName = "/home/nav/workspace/cloisim/build/linux_server/CLOiSimServer.x86_64";
        buildPlayerOptions.target = BuildTarget.StandaloneLinux64;
        buildPlayerOptions.subtarget = (int)StandaloneBuildSubtarget.Server;
        buildPlayerOptions.options = BuildOptions.EnableHeadlessMode | BuildOptions.StrictMode;

        BuildPipeline.BuildPlayer(buildPlayerOptions);
        Debug.Log("Dedicated Server Build finished");
    }
}
