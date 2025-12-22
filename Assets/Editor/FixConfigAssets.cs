using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using PureDOTS.Authoring;

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

        // Now run the bootstrapper if available in this project.
        var bootstrapperType = Type.GetType("Space4XConfigBootstrapper");
        if (bootstrapperType == null)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                bootstrapperType = assembly.GetType("Space4XConfigBootstrapper");
                if (bootstrapperType != null)
                {
                    break;
                }
            }
        }

        if (bootstrapperType != null)
        {
            var method = bootstrapperType.GetMethod("EnsureAssets", BindingFlags.Public | BindingFlags.Static);
            if (method != null)
            {
                method.Invoke(null, null);
            }
            else
            {
                Debug.LogWarning("[FixConfigAssets] Space4XConfigBootstrapper.EnsureAssets not found.");
            }
        }
        else
        {
            Debug.LogWarning("[FixConfigAssets] Space4XConfigBootstrapper type not found; assets recreated but not post-configured.");
        }

        Debug.Log("Config assets fixed and recreated!");
    }
}
