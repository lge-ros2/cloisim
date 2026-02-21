using UnityEngine;
using UnityEditor;

public class RemoveMissingScripts
{
    [MenuItem("Tools/Clean Up Missing Scripts in Scene")]
    public static void CleanUpScene()
    {
        var gameObjects = GameObject.FindObjectsOfType<GameObject>(true);
        int totalRemoved = 0;
        foreach (var go in gameObjects)
        {
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            totalRemoved += removed;
        }
        Debug.Log($"Removed {totalRemoved} missing scripts from the active scene.");
    }
}
