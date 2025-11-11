using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("swap_visual_effect_graph")]
    public static class SwapVisualEffectGraphTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var graphPath = @params["graph_path"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(graphPath))
                {
                    return Response.Error("graph_path is required");
                }

                var go = GameObject.Find(gameObjectName);
                if (go == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' not found");
                }

                var visualEffect = go.GetComponent<UnityEngine.VFX.VisualEffect>();
                if (visualEffect == null)
                {
                    return Response.Error($"GameObject '{gameObjectName}' does not have a VisualEffect component");
                }

                var graphAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.VFX.VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                {
                    return Response.Error($"VFX graph asset not found at path: {graphPath}");
                }

                var oldGraphPath = visualEffect.visualEffectAsset != null ? AssetDatabase.GetAssetPath(visualEffect.visualEffectAsset) : null;
                visualEffect.visualEffectAsset = graphAsset;
                EditorUtility.SetDirty(visualEffect);

                return Response.Success($"Visual effect graph swapped on {gameObjectName}", new
                {
                    gameObjectName,
                    oldGraphPath,
                    newGraphPath = graphPath
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to swap visual effect graph: {ex.Message}");
            }
        }
    }
}

