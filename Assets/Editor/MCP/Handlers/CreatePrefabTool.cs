using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_prefab")]
    public static class CreatePrefabTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string gameObjectName = @params["gameobject_name"]?.ToString();
                string prefabPath = @params["prefab_path"]?.ToString();
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                string searchMethod = @params["search_method"]?.ToString() ?? "by_name";
                
                if (string.IsNullOrEmpty(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }
                
                if (string.IsNullOrEmpty(prefabPath))
                {
                    return Response.Error("prefab_path is required");
                }
                
                // Ensure path has .prefab extension
                if (!prefabPath.EndsWith(".prefab", System.StringComparison.OrdinalIgnoreCase))
                {
                    prefabPath += ".prefab";
                }
                
                // Check if prefab already exists
                if (File.Exists(prefabPath) && !replaceExisting)
                {
                    return Response.Error($"Prefab already exists at {prefabPath}. Set replace_existing=true to overwrite.");
                }
                
                // Find GameObject
                GameObject sourceGO = null;
                
                if (searchMethod == "by_id" && int.TryParse(gameObjectName, out int instanceID))
                {
                    sourceGO = EditorUtility.InstanceIDToObject(instanceID) as GameObject;
                }
                else if (searchMethod == "by_name")
                {
                    sourceGO = GameObject.Find(gameObjectName);
                    if (sourceGO == null)
                    {
                        var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                        sourceGO = allObjects.FirstOrDefault(go => go.name == gameObjectName && go.scene.IsValid());
                    }
                }
                
                if (sourceGO == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(prefabPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create prefab
                GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(sourceGO, prefabPath);
                
                if (prefabAsset == null)
                {
                    return Response.Error($"Failed to create prefab at {prefabPath}");
                }
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Prefab created successfully", new
                {
                    gameObject = gameObjectName,
                    prefabPath = prefabPath,
                    prefabAssetName = prefabAsset.name
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create prefab: {ex.Message}");
            }
        }
    }
}

