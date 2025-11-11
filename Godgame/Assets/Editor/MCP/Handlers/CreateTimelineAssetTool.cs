#if false
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using UnityEngine.Timeline;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.IO;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("create_timeline_asset")]
    public static class CreateTimelineAssetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string timelinePath = @params["timeline_path"]?.ToString();
                bool replaceExisting = @params["replace_existing"]?.ToObject<bool>() ?? false;
                
                if (string.IsNullOrEmpty(timelinePath))
                {
                    return Response.Error("timeline_path is required");
                }
                
                // Ensure path has .playable extension
                if (!timelinePath.EndsWith(".playable", System.StringComparison.OrdinalIgnoreCase))
                {
                    timelinePath += ".playable";
                }
                
                // Check if timeline already exists
                if (File.Exists(timelinePath) && !replaceExisting)
                {
                    return Response.Error($"Timeline asset already exists at {timelinePath}. Set replace_existing=true to overwrite.");
                }
                
                // Create timeline asset
                TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = Path.GetFileNameWithoutExtension(timelinePath);
                
                // Ensure directory exists
                string directory = Path.GetDirectoryName(timelinePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Save timeline asset
                AssetDatabase.CreateAsset(timeline, timelinePath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                return Response.Success($"Timeline asset created successfully", new
                {
                    timelinePath = timelinePath,
                    timelineName = timeline.name,
                    trackCount = timeline.outputTrackCount
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to create timeline asset: {ex.Message}");
            }
        }
    }
}
#endif
