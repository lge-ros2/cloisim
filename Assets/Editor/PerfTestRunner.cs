using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.IO;

public static class PerfTestRunner
{
    public static void StartTest()
    {
        // Import TMP Essential Resources if not already present
        if (!File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset"))
        {
            // Find the ugui package path via PackageManager
            var pkgInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMPro.TMP_Text).Assembly);
            if (pkgInfo != null)
            {
                string tmpPackage = pkgInfo.resolvedPath + "/Package Resources/TMP Essential Resources.unitypackage";
                if (File.Exists(tmpPackage))
                {
                    Debug.Log("Importing TMP Essential Resources...");
                    AssetDatabase.ImportPackage(tmpPackage, false);
                    AssetDatabase.Refresh();
                }
            }
        }

        EditorSceneManager.OpenScene("Assets/Scenes/MainScene.unity");

        // The Main Camera is a child of the UI GameObject in MainScene.
        // We must reparent it to the scene root BEFORE any UI modifications,
        // so it survives even if anything goes wrong with UI.
        var mainCamera = UnityEngine.Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.SetParent(null);
        }

        // Disable RectMask2D components that trigger TMP NullReferenceExceptions
        // during Canvas.SendWillRenderCanvases -> ClipperRegistry.Cull.
        // This preserves buttons and other UI elements while avoiding the crash.
        var ui = GameObject.Find("UI");
        if (ui != null)
        {
            var rectMasks = ui.GetComponentsInChildren<RectMask2D>(true);
            foreach (var mask in rectMasks)
            {
                mask.enabled = false;
            }
        }

        // Attach HDRP camera data to ensure rendering works
        if (mainCamera != null)
        {
            var hdCamData = mainCamera.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            if (hdCamData == null)
            {
                hdCamData = mainCamera.gameObject.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData>();
            }
            hdCamData.clearColorMode = UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData.ClearColorMode.Sky;
        }

        Debug.Log("Forced PlayMode in Editor...");
        EditorApplication.isPlaying = true;
    }
}

