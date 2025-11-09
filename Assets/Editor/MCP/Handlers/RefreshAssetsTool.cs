using Newtonsoft.Json.Linq;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("refresh_assets")]
    public static class RefreshAssetsTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                AssetDatabase.Refresh();
                AssetDatabase.SaveAssets();
                
                return Response.Success("Assets refreshed and saved", new
                {
                    timestamp = DateTime.UtcNow.ToString("o")
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to refresh assets: {ex.Message}");
            }
        }
    }
}

