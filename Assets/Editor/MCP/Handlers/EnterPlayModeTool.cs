#if false
using Newtonsoft.Json.Linq;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("enter_play_mode")]
    public static class EnterPlayModeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                if (EditorApplication.isPlaying)
                {
                    return Response.Error("Already in Play mode");
                }
                
                EditorApplication.isPlaying = true;
                
                return Response.Success("Entered Play mode", new
                {
                    isPlaying = EditorApplication.isPlaying
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to enter Play mode: {ex.Message}");
            }
        }
    }
}
#endif

