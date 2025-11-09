using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Headless VFX capture that uses EditorApplication updates to render VFX without Play Mode
    /// Uses EditorApplication.update() and QueuePlayerLoopUpdate() to simulate VFX in edit mode
    /// Can be executed via Unity batch mode: -batchmode -executeMethod PureDOTS.Editor.MCP.HeadlessVfxCapture.RunFromCommandLine
    /// </summary>
    public static class HeadlessVfxCapture
    {
        private const string RIG_NAME = "VFXCaptureRig";
        
        private static readonly Dictionary<string, Vector3> CAMERA_POSITIONS = new Dictionary<string, Vector3>
        {
            ["Front"] = new Vector3(0, 0, -5),
            ["Back"] = new Vector3(0, 0, 5),
            ["Left"] = new Vector3(-5, 0, 0),
            ["Right"] = new Vector3(5, 0, 0),
            ["Top"] = new Vector3(0, 5, 0),
            ["Bottom"] = new Vector3(0, -5, 0),
            ["FrontRight"] = new Vector3(3.5f, 0, -3.5f),
            ["BackLeft"] = new Vector3(-3.5f, 0, 3.5f)
        };

        /// <summary>
        /// Entry point for Unity batch mode execution
        /// Reads parameters from command line args or environment variables
        /// </summary>
        public static void RunFromCommandLine()
        {
            try
            {
                // Parse command line arguments
                var args = Environment.GetCommandLineArgs();
                string graphPath = null;
                string graphId = null;
                string outputDir = "Data/MCP_Exports/MultiAngle";
                int width = 512;
                int height = 512;
                float duration = 3f;
                int frameCount = 10;
                string backgroundColorStr = "black";

                // Parse arguments (format: -graphPath "path" -outputDir "dir" etc.)
                for (int i = 0; i < args.Length; i++)
                {
                    switch (args[i].ToLower())
                    {
                        case "-graphpath":
                            if (i + 1 < args.Length) graphPath = args[++i];
                            break;
                        case "-graphid":
                            if (i + 1 < args.Length) graphId = args[++i];
                            break;
                        case "-outputdir":
                            if (i + 1 < args.Length) outputDir = args[++i];
                            break;
                        case "-width":
                            if (i + 1 < args.Length) int.TryParse(args[++i], out width);
                            break;
                        case "-height":
                            if (i + 1 < args.Length) int.TryParse(args[++i], out height);
                            break;
                        case "-duration":
                            if (i + 1 < args.Length) float.TryParse(args[++i], out duration);
                            break;
                        case "-framecount":
                            if (i + 1 < args.Length) int.TryParse(args[++i], out frameCount);
                            break;
                        case "-backgroundcolor":
                            if (i + 1 < args.Length) backgroundColorStr = args[++i];
                            break;
                    }
                }

                // Try environment variables as fallback
                if (string.IsNullOrEmpty(graphPath) && string.IsNullOrEmpty(graphId))
                {
                    graphPath = Environment.GetEnvironmentVariable("VFX_GRAPH_PATH");
                    graphId = Environment.GetEnvironmentVariable("VFX_GRAPH_ID");
                }
                if (string.IsNullOrEmpty(outputDir))
                    outputDir = Environment.GetEnvironmentVariable("VFX_OUTPUT_DIR") ?? outputDir;

                // Resolve graph path
                if (string.IsNullOrEmpty(graphPath) && !string.IsNullOrEmpty(graphId))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                }

                if (string.IsNullOrEmpty(graphPath))
                {
                    Debug.LogError("[HeadlessVfxCapture] graph_path or graph_id is required");
                    EditorApplication.Exit(1);
                    return;
                }

                var graphAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                {
                    Debug.LogError($"[HeadlessVfxCapture] VFX graph not found at path: {graphPath}");
                    EditorApplication.Exit(1);
                    return;
                }

                var graphName = Path.GetFileNameWithoutExtension(graphPath);
                Color backgroundColor = backgroundColorStr.ToLower() == "white" ? Color.white : Color.black;

                Debug.Log($"[HeadlessVfxCapture] Starting headless capture: {graphName}, output: {outputDir}");

                // Run capture
                var result = CaptureHeadless(graphAsset, graphName, width, height, duration, frameCount, backgroundColor, outputDir);

                if (result.Success)
                {
                    Debug.Log($"[HeadlessVfxCapture] Capture completed successfully: {result.OutputDir}");
                    EditorApplication.Exit(0);
                }
                else
                {
                    Debug.LogError($"[HeadlessVfxCapture] Capture failed: {result.ErrorMessage}");
                    EditorApplication.Exit(1);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HeadlessVfxCapture] Fatal error: {ex.Message}\n{ex.StackTrace}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// Main capture logic using EditorApplication updates (no Play Mode required)
        /// </summary>
        public static CaptureResult CaptureHeadless(
            VisualEffectAsset graphAsset,
            string graphName,
            int width,
            int height,
            float duration,
            int frameCount,
            Color backgroundColor,
            string outputDir)
        {
            try
            {
                // Create clean scene
                var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                    UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                    UnityEditor.SceneManagement.NewSceneMode.Single);

                // Create VFX GameObject at origin
                var vfxGO = new GameObject("HeadlessVFX");
                vfxGO.transform.position = Vector3.zero;
                var visualEffect = vfxGO.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = graphAsset;
                visualEffect.enabled = true;

                // Create capture rig with cameras
                var rig = new GameObject(RIG_NAME);
                rig.transform.position = Vector3.zero;

                var cameras = new List<CameraCaptureInfo>();
                foreach (var kvp in CAMERA_POSITIONS)
                {
                    var cameraName = kvp.Key;
                    var position = kvp.Value;

                    var cameraGO = new GameObject($"CaptureCamera_{cameraName}");
                    cameraGO.transform.SetParent(rig.transform, false);
                    cameraGO.transform.localPosition = position;
                    cameraGO.transform.LookAt(Vector3.zero);

                    var camera = cameraGO.AddComponent<Camera>();
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = backgroundColor;
                    camera.orthographic = false;
                    camera.fieldOfView = 60f;
                    camera.nearClipPlane = 0.1f;
                    camera.farClipPlane = 100f;

                    var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                    renderTexture.Create();
                    camera.targetTexture = renderTexture;

                    cameras.Add(new CameraCaptureInfo
                    {
                        Camera = camera,
                        RenderTexture = renderTexture,
                        CameraName = cameraName
                    });
                }

                // Prepare output directory
                var absoluteOutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));
                Directory.CreateDirectory(absoluteOutputDir);

                // Play VFX
                visualEffect.Play();
                visualEffect.enabled = true;

                // Calculate frame times
                var frameTimes = Enumerable.Range(0, frameCount)
                    .Select(i => (float)i / Mathf.Max(1, frameCount - 1) * duration)
                    .ToArray();

                var frameDelta = duration / Mathf.Max(1, frameCount - 1);
                var frameTimeMs = frameDelta * 1000f; // Convert to milliseconds

                var captureResults = new List<object>();

                // Initial delay to let VFX start
                for (int i = 0; i < 5; i++)
                {
                    EditorApplication.update();
                    EditorApplication.QueuePlayerLoopUpdate();
                    System.Threading.Thread.Sleep(50);
                }

                // Capture frames using EditorApplication updates (like VfxPreviewRenderer)
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    // Wait for frame time (except first frame)
                    if (frameIdx > 0)
                    {
                        var waitIterations = Mathf.Max(1, (int)(frameTimeMs / 50));
                        for (int i = 0; i < waitIterations; i++)
                        {
                            EditorApplication.update();
                            EditorApplication.QueuePlayerLoopUpdate();
                            System.Threading.Thread.Sleep(50);
                        }
                    }

                    // Ensure VFX is still playing
                    if (!visualEffect.enabled)
                    {
                        visualEffect.enabled = true;
                        visualEffect.Play();
                    }

                    // Render all cameras for this frame
                    foreach (var cameraInfo in cameras)
                    {
                        cameraInfo.Camera.Render();

                        var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                        RenderTexture.active = cameraInfo.RenderTexture;
                        tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                        tex.Apply();
                        RenderTexture.active = null;

                        var fileName = $"{graphName}_{cameraInfo.CameraName}_frame{frameIdx:D3}.png";
                        var filePath = Path.Combine(absoluteOutputDir, fileName);
                        var bytes = tex.EncodeToPNG();
                        File.WriteAllBytes(filePath, bytes);
                        
                        Debug.Log($"[HeadlessVfxCapture] Saved frame: {filePath} ({bytes.Length} bytes)");

                        UnityEngine.Object.DestroyImmediate(tex);

                        captureResults.Add(new
                        {
                            cameraName = cameraInfo.CameraName,
                            frameIndex = frameIdx,
                            frameTime = frameTimes[frameIdx],
                            fileName = fileName,
                            path = filePath
                        });
                    }
                }

                // Generate manifest
                var manifestData = new Dictionary<string, object>
                {
                    ["graphName"] = graphName,
                    ["graphPath"] = AssetDatabase.GetAssetPath(graphAsset),
                    ["width"] = width,
                    ["height"] = height,
                    ["duration"] = duration,
                    ["frameCount"] = frameCount,
                    ["backgroundColor"] = backgroundColor == Color.white ? "white" : "black",
                    ["cameras"] = cameras.Select(c => new Dictionary<string, object>
                    {
                        ["name"] = c.CameraName,
                        ["position"] = new Dictionary<string, float>
                        {
                            ["x"] = c.Camera.transform.position.x,
                            ["y"] = c.Camera.transform.position.y,
                            ["z"] = c.Camera.transform.position.z
                        },
                        ["rotation"] = new Dictionary<string, float>
                        {
                            ["x"] = c.Camera.transform.rotation.eulerAngles.x,
                            ["y"] = c.Camera.transform.rotation.eulerAngles.y,
                            ["z"] = c.Camera.transform.rotation.eulerAngles.z
                        }
                    }).ToArray(),
                    ["frames"] = captureResults
                };

                var manifestPath = Path.Combine(absoluteOutputDir, $"{graphName}_manifest.json");
                var settings = new Newtonsoft.Json.JsonSerializerSettings
                {
                    Formatting = Newtonsoft.Json.Formatting.Indented,
                    ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore
                };
                File.WriteAllText(manifestPath, Newtonsoft.Json.JsonConvert.SerializeObject(manifestData, settings));

                // Extract and save parameters and asset bindings
                if (visualEffect != null && graphAsset != null)
                {
                    Debug.Log("[HeadlessVfxCapture] Extracting parameters and asset bindings...");
                    var (currentParams, assetBindings) = PureDOTS.Editor.MCP.Helpers.VfxParameterExtractor.ExtractParametersAndAssets(visualEffect);
                    PureDOTS.Editor.MCP.Helpers.VfxParameterExtractor.SaveParametersAndAssets(absoluteOutputDir, graphName, currentParams, assetBindings);
                    Debug.Log($"[HeadlessVfxCapture] Saved {currentParams.Count} parameters and {assetBindings.Count} asset bindings for {graphName}");
                }

                // Cleanup
                foreach (var cameraInfo in cameras)
                {
                    if (cameraInfo.Camera != null)
                        cameraInfo.Camera.targetTexture = null;
                    if (cameraInfo.RenderTexture != null)
                        UnityEngine.Object.DestroyImmediate(cameraInfo.RenderTexture);
                }

                return new CaptureResult
                {
                    Success = true,
                    OutputDir = absoluteOutputDir,
                    FrameCount = frameCount,
                    CameraCount = cameras.Count
                };
            }
            catch (Exception ex)
            {
                return new CaptureResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
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

        private class CameraCaptureInfo
        {
            public Camera Camera;
            public RenderTexture RenderTexture;
            public string CameraName;
        }

        public class CaptureResult
        {
            public bool Success;
            public string OutputDir;
            public int FrameCount;
            public int CameraCount;
            public string ErrorMessage;
        }
    }
}

