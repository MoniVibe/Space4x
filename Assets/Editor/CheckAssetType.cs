using UnityEngine;
using UnityEditor;

public class CheckAssetType
{
    [MenuItem("Tools/Space4X/Check Asset Type")]
    public static void Execute()
    {
        string path = "Assets/Data/Space4XRenderCatalog_v2.asset";
        var obj = AssetDatabase.LoadAssetAtPath<Object>(path);
        if (obj != null)
        {
            Debug.Log($"Asset at {path} is of type: {obj.GetType().FullName}");
        }
        else
        {
            Debug.LogError($"Asset at {path} could not be loaded as Object.");
        }
    }
}
