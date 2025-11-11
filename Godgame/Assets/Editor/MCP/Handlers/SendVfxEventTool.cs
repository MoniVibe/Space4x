using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("send_vfx_event")]
    public static class SendVfxEventTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var gameObjectName = @params["gameobject_name"]?.ToString();
                var eventName = @params["event_name"]?.ToString();

                if (string.IsNullOrWhiteSpace(gameObjectName))
                {
                    return Response.Error("gameobject_name is required");
                }

                if (string.IsNullOrWhiteSpace(eventName))
                {
                    return Response.Error("event_name is required");
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

                // Send event using the event name string directly
                // VisualEffect.SendEvent can accept either an event ID or event name string
                visualEffect.SendEvent(eventName);
                EditorUtility.SetDirty(visualEffect);

                return Response.Success($"VFX event '{eventName}' sent to {gameObjectName}", new
                {
                    gameObjectName,
                    eventName
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to send VFX event: {ex.Message}");
            }
        }
    }
}

