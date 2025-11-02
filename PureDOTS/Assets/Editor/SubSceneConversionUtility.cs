using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SubSceneConversionUtility
{
    public static void ConvertChildrenOfRoot(string rootName)
    {
        var root = GameObject.Find(rootName);
        if (root == null)
        {
            Debug.LogError($"SubSceneConversionUtility: Could not find root object '{rootName}' in the active scene.");
            return;
        }

        var children = root.transform.Cast<Transform>().Select(t => t.gameObject).ToArray();
        if (children.Length == 0)
        {
            Debug.LogWarning($"SubSceneConversionUtility: Root '{rootName}' has no child objects to convert.");
            return;
        }

        Selection.objects = children;
        EditorApplication.ExecuteMenuItem("Entities/Convert To Entity Scene");
    }
}
