using UnityEngine;
using UnityEditor;
using PureDOTS.Authoring;
using System.IO;

public static class FixConfigAssets
{
    [MenuItem("Tools/Fix Space4X Config Assets")]
    public static void FixAssets()
    {
        const string configPath = "Assets/Space4X/Config";
        
        // Ensure directory exists
        if (!AssetDatabase.IsValidFolder(configPath))
        {
            string parentPath = "Assets/Space4X";
            if (!AssetDatabase.IsValidFolder(parentPath))
            {
                AssetDatabase.CreateFolder("Assets", "Space4X");
            }
            AssetDatabase.CreateFolder(parentPath, "Config");
        }

        // Delete existing assets if they exist (to force recreation)
        string[] assetPaths = {
            $"{configPath}/PureDotsResourceTypes.asset",
            $"{configPath}/ResourceRecipeCatalog.asset",
            $"{configPath}/PureDotsRuntimeConfig.asset"
        };

        foreach (var path in assetPaths)
        {
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        // Now run the bootstrapper
        Space4XConfigBootstrapper.EnsureAssets();

        Debug.Log("Config assets fixed and recreated!");
    }
}

