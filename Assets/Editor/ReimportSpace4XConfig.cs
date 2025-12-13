using UnityEditor;
using UnityEngine;

public class ReimportSpace4XConfig
{
    public static void Execute()
    {
        string path = "Assets/Scenes/Space4XConfig.unity";
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"Re-imported {path}");
    }
}
