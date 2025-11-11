using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("play_effect")]
    public static class PlayEffectTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
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

                visualEffect.Play();
                EditorUtility.SetDirty(visualEffect);

                return Response.Success($"Visual effect played on {gameObjectName}", new
                {
                    gameObjectName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to play effect: {ex.Message}");
            }
        }
    }
}

