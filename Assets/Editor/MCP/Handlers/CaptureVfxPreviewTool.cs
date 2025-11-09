using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("capture_vfx_preview_window")]
    public static class CaptureVfxPreviewTool
    {
        public static object HandleCommand(JObject @params)
        {
            try
            {
                string graphPath = @params["graph_path"]?.ToString();
                string graphId = @params["graph_id"]?.ToString();
                bool autoPlay = @params["auto_play"]?.ToObject<bool?>() ?? true;
                string outputDir = @params["output_dir"]?.ToString() ?? "Data/MCP_Exports";
                string fileName = @params["file_name"]?.ToString() ?? $"vfx_preview_{DateTime.Now:yyyyMMdd_HHmmss}";

                if (string.IsNullOrEmpty(graphPath) && !string.IsNullOrEmpty(graphId))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                        return Response.Error($"Graph with id '{graphId}' not found");
                }

                if (!string.IsNullOrEmpty(graphPath))
                {
                    var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                    if (asset == null)
                        return Response.Error($"Unable to load VisualEffectAsset at path '{graphPath}'");

                    Selection.activeObject = asset;
                    AssetDatabase.OpenAsset(asset);
                    // Allow editor to instantiate window
                    EditorApplication.QueuePlayerLoopUpdate();
                    System.Threading.Thread.Sleep(100);
                }

                // Locate VFXViewWindow
                var windowType = Type.GetType("UnityEditor.VFX.VFXViewWindow, UnityEditor.VFXModule")
                                 ?? Type.GetType("UnityEditor.VFX.UI.VFXViewWindow, Unity.VisualEffectGraph.Editor")
                                 ?? AppDomain.CurrentDomain.GetAssemblies()
                                    .Select(a => a.GetType("UnityEditor.VFX.UI.VFXViewWindow", false))
                                    .FirstOrDefault(t => t != null);

                if (windowType == null)
                {
                    var matches = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a =>
                        {
                            try
                            {
                                return a.GetTypes();
                            }
                            catch (ReflectionTypeLoadException rtle)
                            {
                                return rtle.Types.Where(t => t != null);
                            }
                            catch
                            {
                                return Array.Empty<Type>();
                            }
                        })
                        .Where(t => t.FullName != null && t.FullName.IndexOf("VFXViewWindow", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Select(t => t.FullName + ", Assembly=" + t.Assembly.FullName)
                        .ToList();

                    var assemblyNames = string.Join(", ", AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.FullName)
                        .Where(n => n.IndexOf("VFX", StringComparison.OrdinalIgnoreCase) >= 0));

                    Debug.LogWarning($"[CaptureVfxPreview] Unable to locate VFXViewWindow. Assemblies scanned: {assemblyNames}. Matches: {string.Join(" | ", matches)}");
                    return Response.Error("Unable to locate VFXViewWindow type. Ensure Visual Effect Graph package is installed.");
                }

                var window = Resources.FindObjectsOfTypeAll(windowType)
                    .OfType<EditorWindow>()
                    .FirstOrDefault();
                Debug.Log($"[CaptureVfxPreview] found window count: {Resources.FindObjectsOfTypeAll(windowType).Length}");

                if (window == null)
                    return Response.Error("VFX Graph window not found. Please open the VFX Graph you want to capture.");

                window.Repaint();
                EditorApplication.QueuePlayerLoopUpdate();
                System.Threading.Thread.Sleep(50);

                // Access internal preview controller via reflection
                var viewProperty = windowType.GetProperty("view", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                object view = null;
                if (viewProperty != null)
                {
                    view = viewProperty.GetValue(window);
                    Debug.Log($"[CaptureVfxPreview] view property type: {view?.GetType().FullName ?? "null"}");
                }

                if (view == null)
                {
                    var viewField = windowType.GetField("m_View", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (viewField == null)
                    {
                        Debug.LogWarning("[CaptureVfxPreview] m_View field not found on VFXViewWindow");
                    }
                    view = viewField?.GetValue(window);
                }

                if (view == null)
                    return Response.Error("Failed to access VFX view.");
                Debug.Log($"[CaptureVfxPreview] view type: {view.GetType().FullName}");

                var viewType = view.GetType();
                var previewField = viewType.GetField("m_Preview", BindingFlags.Instance | BindingFlags.NonPublic);
                var preview = previewField?.GetValue(view);
                if (preview == null)
                    return Response.Error("Failed to access VFX preview controller.");

                if (autoPlay)
                {
                    EnsurePreviewPlaying(preview);
                    // brief delay to allow start
                    System.Threading.Thread.Sleep(50);
                }

                // Get the preview render texture
                var previewType = preview.GetType();
                var renderTextureProp = previewType.GetProperty("renderTexture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var renderTexture = renderTextureProp?.GetValue(preview) as RenderTexture;
                if (renderTexture == null)
                    return Response.Error("Preview render texture not available. Make sure the preview is visible.");

                previewType.GetMethod("Update", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(preview, null);

                var previousActive = RenderTexture.active;
                RenderTexture.active = renderTexture;

                var tex = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
                tex.Apply();

                RenderTexture.active = previousActive;

                var absoluteOutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));
                Directory.CreateDirectory(absoluteOutputDir);
                var absoluteFilePath = Path.Combine(absoluteOutputDir, fileName + ".png");
                File.WriteAllBytes(absoluteFilePath, tex.EncodeToPNG());

                UnityEngine.Object.DestroyImmediate(tex);

                return Response.Success("Captured VFX preview", new
                {
                    path = absoluteFilePath,
                    width = renderTexture.width,
                    height = renderTexture.height
                });
            }
            catch (Exception ex)
            {
                return Response.Error($"Failed to capture VFX preview: {ex.Message}");
            }
        }

        private static string ResolveGraphIdToPath(string graphId)
        {
            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(path);
                if (fileName.Equals(graphId, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        private static void EnsurePreviewPlaying(object preview)
        {
            if (preview == null) return;
            var previewType = preview.GetType();

            var playingProp = previewType.GetProperty("playing", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (playingProp != null)
            {
                try
                {
                    playingProp.SetValue(preview, true);
                }
                catch { }
            }

            var playMethod = previewType.GetMethod("Play", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (playMethod != null)
            {
                try
                {
                    playMethod.Invoke(preview, null);
                }
                catch { }
            }

            var startMethod = previewType.GetMethod("StartPlay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (startMethod != null)
            {
                try
                {
                    startMethod.Invoke(preview, null);
                }
                catch { }
            }
        }
    }
}
