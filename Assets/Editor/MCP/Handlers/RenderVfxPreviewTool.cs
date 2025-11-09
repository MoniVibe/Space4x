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
    [McpForUnityTool("render_vfx_preview")]
    public static class RenderVfxPreviewTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                var graphPath = @params["graph_path"]?.ToString();
                var graphId = @params["graph_id"]?.ToString();
                var parametersToken = @params["params"];
                int size = @params["size"]?.ToObject<int>() ?? 320;
                float seconds = @params["seconds"]?.ToObject<float>() ?? 0.5f;
                int fps = @params["fps"]?.ToObject<int>() ?? 16;
                int frames = @params["frames"]?.ToObject<int>() ?? 1;

                // Resolve graph path if graph_id provided
                if (!string.IsNullOrEmpty(graphId) && string.IsNullOrEmpty(graphPath))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                    {
                        return Response.Error($"Graph with id '{graphId}' not found");
                    }
                }

                if (string.IsNullOrEmpty(graphPath))
                {
                    return Response.Error("graph_path or graph_id is required");
                }

                // Parse parameters
                var parameters = new Dictionary<string, object>();
                if (parametersToken != null && parametersToken.Type == JTokenType.Object)
                {
                    var paramObj = parametersToken as JObject;
                    foreach (var prop in paramObj.Properties())
                    {
                        var value = prop.Value;
                        if (value.Type == JTokenType.Float || value.Type == JTokenType.Integer)
                        {
                            parameters[prop.Name] = value.ToObject<float>();
                        }
                        else if (value.Type == JTokenType.Boolean)
                        {
                            parameters[prop.Name] = value.ToObject<bool>();
                        }
                        else if (value.Type == JTokenType.Array)
                        {
                            var arr = value.ToObject<float[]>();
                            if (arr.Length == 2)
                            {
                                parameters[prop.Name] = new Vector2(arr[0], arr[1]);
                            }
                            else if (arr.Length == 3)
                            {
                                parameters[prop.Name] = new Vector3(arr[0], arr[1], arr[2]);
                            }
                            else if (arr.Length == 4)
                            {
                                parameters[prop.Name] = new Vector4(arr[0], arr[1], arr[2], arr[3]);
                            }
                            else
                            {
                                parameters[prop.Name] = arr.ToList();
                            }
                        }
                        else if (value.Type == JTokenType.String)
                        {
                            var str = value.ToString();
                            // Try to load as texture if it's a path
                            if (str.StartsWith("Assets/"))
                            {
                                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(str);
                                if (tex != null)
                                {
                                    parameters[prop.Name] = tex;
                                }
                                else
                                {
                                    parameters[prop.Name] = str;
                                }
                            }
                            else
                            {
                                parameters[prop.Name] = str;
                            }
                        }
                        else
                        {
                            parameters[prop.Name] = value.ToObject<object>();
                        }
                    }
                }

                // Render preview
                if (frames == 1)
                {
                    var framePath = VfxPreviewRenderer.RenderPreview(
                        graphPath,
                        parameters,
                        size,
                        size,
                        seconds,
                        fps,
                        1);

                    if (string.IsNullOrEmpty(framePath))
                    {
                        return Response.Error("Failed to render preview frame");
                    }

                    // Ensure absolute path is returned
                    var absoluteFramePath = System.IO.Path.IsPathRooted(framePath) 
                        ? framePath 
                        : System.IO.Path.GetFullPath(framePath);
                    
                    return Response.Success("Preview rendered", new
                    {
                        graphPath,
                        framePath = absoluteFramePath,
                        frames = new[] { absoluteFramePath }
                    });
                }
                else
                {
                    var framePaths = VfxPreviewRenderer.RenderPreviewFrames(
                        graphPath,
                        parameters,
                        size,
                        size,
                        seconds,
                        fps,
                        frames);

                    if (framePaths == null || framePaths.Count == 0)
                    {
                        return Response.Error("Failed to render preview frames");
                    }

                    // Ensure all paths are absolute
                    var absoluteFramePaths = framePaths.Select(fp => 
                        System.IO.Path.IsPathRooted(fp) 
                            ? fp 
                            : System.IO.Path.GetFullPath(fp)
                    ).ToArray();
                    
                    return Response.Success($"Rendered {framePaths.Count} frames", new
                    {
                        graphPath,
                        frames = absoluteFramePaths,
                        frameCount = absoluteFramePaths.Length
                    });
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to render preview: {ex.Message}");
            }
        }

        private static string ResolveGraphIdToPath(string graphId)
        {
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(graphId, StringComparison.OrdinalIgnoreCase))
                {
                    return path;
                }
            }
            return null;
        }
    }
}

