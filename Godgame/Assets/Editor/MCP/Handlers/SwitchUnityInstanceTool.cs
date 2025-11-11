using Newtonsoft.Json.Linq;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("switch_unity_instance")]
    public static class SwitchUnityInstanceTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string instance = @params["instance"]?.ToString();
                
                if (string.IsNullOrEmpty(instance))
                {
                    return Response.Error("instance parameter is required");
                }
                
                // Note: Actual instance switching requires MCP bridge API access
                // This is a placeholder that acknowledges the request
                // The MCP bridge should handle actual switching via set_active_instance
                
                return Response.Success($"Instance switch requested: {instance}", new
                {
                    requestedInstance = instance,
                    note = "Instance switching is handled by MCP bridge. Verify switch by calling identify_unity_instance."
                });
            }
            catch (System.Exception ex)
            {
                return Response.Error($"Failed to switch Unity instance: {ex.Message}");
            }
        }
    }
}

