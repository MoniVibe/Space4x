using UnityEditor;
using UnityEngine;
using System.IO;

public class ForceUpdate
{
    public static void Run()
    {
        string path = "Assets/Settings/PC_RPAsset.asset";
        string content = File.ReadAllText(path);
        
        // Change to 11
        string temp = content.Replace("k_AssetVersion: 12", "k_AssetVersion: 11")
                             .Replace("k_AssetPreviousVersion: 12", "k_AssetPreviousVersion: 11");
        File.WriteAllText(path, temp);
        AssetDatabase.ImportAsset(path);
        Debug.Log("Set version to 11");

        // Change back to 12
        File.WriteAllText(path, content);
        AssetDatabase.ImportAsset(path);
        Debug.Log("Set version back to 12");
    }
}
