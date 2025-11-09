using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// MCP tool wrapper for headless VFX capture
    /// Can be called directly from MCP or will launch Unity batch mode
    /// </summary>
    [McpForUnityTool("capture_vfx_headless")]
    public static class CaptureVfxHeadlessTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphId = @params["graph_id"]?.ToString();
                string graphPath = @params["graph_path"]?.ToString();
                int width = @params["width"]?.ToObject<int?>() ?? 512;
                int height = @params["height"]?.ToObject<int?>() ?? 512;
                float duration = @params["duration"]?.ToObject<float?>() ?? 3f;
                int frameCount = @params["frame_count"]?.ToObject<int?>() ?? 10;
                string backgroundColorStr = @params["background_color"]?.ToString() ?? "black";
                string outputDir = @params["output_dir"]?.ToString() ?? "Data/MCP_Exports/MultiAngle";

                // Resolve graph path
                if (string.IsNullOrEmpty(graphPath) && !string.IsNullOrEmpty(graphId))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                        return Response.Error($"Graph with id '{graphId}' not found");
                }

                if (string.IsNullOrEmpty(graphPath))
                    return Response.Error("graph_path or graph_id is required");

                var graphAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                    return Response.Error($"VFX graph not found at path: {graphPath}");

                var graphName = System.IO.Path.GetFileNameWithoutExtension(graphPath);
                Color backgroundColor = backgroundColorStr.ToLower() == "white" ? Color.white : Color.black;

                Debug.Log($"[CaptureVfxHeadless] Starting headless capture: {graphName}");

                // Run headless capture directly (no batch mode needed if Unity is already running)
                var result = HeadlessVfxCapture.CaptureHeadless(
                    graphAsset,
                    graphName,
                    width,
                    height,
                    duration,
                    frameCount,
                    backgroundColor,
                    outputDir
                );

                if (result.Success)
                {
                    return Response.Success($"Captured {result.FrameCount} frames from {result.CameraCount} cameras", new
                    {
                        graphId = graphName,
                        frameCount = result.FrameCount,
                        cameraCount = result.CameraCount,
                        outputDir = result.OutputDir
                    });
                }
                else
                {
                    return Response.Error($"Headless capture failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to capture VFX headless: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static string ResolveGraphIdToPath(string graphId)
        {
            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(graphId, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }
    }
}

