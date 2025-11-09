using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("render_snippet")]
    public static class RenderSnippetTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                // Extract snippet parameters
                var snippetId = @params["snippet_id"]?.ToString();
                var snippetPath = @params["snippet_path"]?.ToString();
                
                // Resolve snippet path if needed
                if (!string.IsNullOrEmpty(snippetId) && string.IsNullOrEmpty(snippetPath))
                {
                    snippetPath = ResolveSnippetIdToPath(snippetId);
                    if (string.IsNullOrEmpty(snippetPath))
                    {
                        return Response.Error($"Snippet with id '{snippetId}' not found");
                    }
                }

                if (string.IsNullOrEmpty(snippetPath))
                {
                    return Response.Error("snippet_path or snippet_id is required");
                }

                // Reuse capture_vfx_multi_angle logic but with snippet path
                // Convert snippet params to graph params format
                var captureParams = new JObject
                {
                    ["graph_path"] = snippetPath,
                    ["graph_id"] = snippetId ?? System.IO.Path.GetFileNameWithoutExtension(snippetPath)
                };

                // Copy all other parameters from snippet request
                if (@params["params"] != null)
                {
                    captureParams["params"] = @params["params"];
                }
                if (@params["cameras"] != null)
                {
                    captureParams["cameras"] = @params["cameras"];
                }
                if (@params["camera_names"] != null)
                {
                    captureParams["camera_names"] = @params["camera_names"];
                }
                if (@params["frame_times"] != null)
                {
                    captureParams["frame_times"] = @params["frame_times"];
                }
                if (@params["frame_count"] != null)
                {
                    captureParams["frame_count"] = @params["frame_count"];
                }
                if (@params["duration"] != null)
                {
                    captureParams["duration"] = @params["duration"];
                }
                if (@params["pre_roll"] != null)
                {
                    captureParams["pre_roll"] = @params["pre_roll"];
                }
                if (@params["width"] != null)
                {
                    captureParams["width"] = @params["width"];
                }
                if (@params["height"] != null)
                {
                    captureParams["height"] = @params["height"];
                }
                if (@params["size"] != null)
                {
                    captureParams["size"] = @params["size"];
                }
                if (@params["background_color"] != null)
                {
                    captureParams["background_color"] = @params["background_color"];
                }
                if (@params["output_dir"] != null)
                {
                    captureParams["output_dir"] = @params["output_dir"];
                }
                if (@params["seed"] != null)
                {
                    captureParams["seed"] = @params["seed"];
                }

                // Call the existing capture tool
                var captureResult = CaptureVfxMultiAngleTool.HandleCommand(captureParams);
                
                // Modify response to indicate it's a snippet
                if (captureResult is JObject response && response["success"]?.ToObject<bool>() == true)
                {
                    var data = response["data"] as JObject;
                    if (data != null)
                    {
                        data["snippet_id"] = snippetId ?? System.IO.Path.GetFileNameWithoutExtension(snippetPath);
                        data["snippet_path"] = snippetPath;
                    }
                }

                return captureResult;
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to render snippet: {ex.Message}");
            }
        }

        private static string ResolveSnippetIdToPath(string snippetId)
        {
            // Try subgraphs first
            string[] subgraphGuids = AssetDatabase.FindAssets("t:VFXSubgraphOperator", new[] { "Assets" });
            foreach (string guid in subgraphGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(snippetId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            
            // Try regular graphs
            string[] graphGuids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (string guid in graphGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(snippetId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            
            return null;
        }
    }
}

