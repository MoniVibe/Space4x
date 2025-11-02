using UnityEditor;
using UnityEngine;

public static class MenuDebugUtility
{
    public static void LogGameObjectMenu()
    {
        var items = Unsupported.GetSubmenus("GameObject");
        Debug.Log($"GameObject submenu count: {items.Length}");
        foreach (var item in items)
        {
            Debug.Log($"GameObject Menu Item: {item}");
        }
    }
}
