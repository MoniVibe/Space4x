using Newtonsoft.Json.Linq;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("list_scripting_defines")]
    public static class ListScriptingDefinesTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                BuildTargetGroup targetGroup = BuildTargetGroup.Standalone;
                if (@params["target_group"] != null)
                {
                    string targetGroupStr = @params["target_group"].ToString();
                    if (Enum.TryParse<BuildTargetGroup>(targetGroupStr, out BuildTargetGroup parsed))
                    {
                        targetGroup = parsed;
                    }
                }
                
#pragma warning disable CS0618
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#pragma warning restore CS0618
                string[] defineArray = string.IsNullOrEmpty(defines) 
                    ? new string[0] 
                    : defines.Split(';').Where(d => !string.IsNullOrEmpty(d)).ToArray();
                
                return Response.Success($"Scripting defines retrieved for {targetGroup}", new
                {
                    targetGroup = targetGroup.ToString(),
                    defines = defineArray,
                    defineCount = defineArray.Length
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to list scripting defines: {ex.Message}");
            }
        }
    }
}

