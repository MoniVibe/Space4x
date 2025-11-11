using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_unity_instances")]
    public static class ListUnityInstancesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Note: Unity MCP typically manages instances internally
                // This tool provides info about the current instance and any known instances
                var activeScene = EditorSceneManager.GetActiveScene();
                var projectRoot = System.IO.Directory.GetParent(Application.dataPath).FullName;
                var projectName = System.IO.Path.GetFileName(projectRoot);
                
                // Get current instance info
                var currentInstance = new
                {
                    projectName = projectName,
                    projectRoot = projectRoot,
                    activeScene = new
                    {
                        name = activeScene.name,
                        path = activeScene.path
                    },
                    unityVersion = Application.unityVersion
                };
                
                // Note: Actual instance enumeration would require access to MCP bridge internals
                // For now, return current instance info
                return Response.Success("Unity instances listed", new
                {
                    currentInstance = currentInstance,
                    note = "Multiple instance enumeration requires MCP bridge access. Currently showing current instance only."
                });
            }
            catch (System.Exception ex)
            {
                return Response.Error($"Failed to list Unity instances: {ex.Message}");
            }
        }
    }
}

