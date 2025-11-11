using Newtonsoft.Json.Linq;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("remove_scripting_define")]
    public static class RemoveScriptingDefineTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string define = @params["define"]?.ToString();
                if (string.IsNullOrEmpty(define))
                {
                    return Response.Error("define is required");
                }
                
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
                // Get current defines
                string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
#pragma warning restore CS0618
                var defineList = string.IsNullOrEmpty(defines)
                    ? new System.Collections.Generic.List<string>()
                    : defines.Split(';').Where(d => !string.IsNullOrEmpty(d)).ToList();
                
                // Check if exists
                if (!defineList.Contains(define))
                {
                    return Response.Success($"Scripting define '{define}' not found", new
                    {
                        targetGroup = targetGroup.ToString(),
                        define = define,
                        allDefines = defineList.ToArray()
                    });
                }
                
                // Remove define
                defineList.Remove(define);
#pragma warning disable CS0618
                PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", defineList));
#pragma warning restore CS0618
                
                return Response.Success($"Scripting define '{define}' removed", new
                {
                    targetGroup = targetGroup.ToString(),
                    define = define,
                    allDefines = defineList.ToArray()
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to remove scripting define: {ex.Message}");
            }
        }
    }
}

