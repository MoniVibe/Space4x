#if false
using Newtonsoft.Json.Linq;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("exit_play_mode")]
    public static class ExitPlayModeTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    return Response.Error("Not in Play mode");
                }
                
                EditorApplication.isPlaying = false;
                
                return Response.Success("Exited Play mode", new
                {
                    isPlaying = EditorApplication.isPlaying
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to exit Play mode: {ex.Message}");
            }
        }
    }
}
#endif

