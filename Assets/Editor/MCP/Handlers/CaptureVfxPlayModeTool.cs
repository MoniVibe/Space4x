using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.VFX;
using UnityEditor;
using PureDOTS.Editor.MCP.Helpers;
using MCPForUnity.Editor.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PureDOTS.Editor.MCP
{
    /// <summary>
    /// Captures VFX in Play Mode where VFX actually renders
    /// </summary>
    [McpForUnityTool("capture_vfx_playmode")]
    public static class CaptureVfxPlayModeTool
    {
        private const string RIG_NAME = "VFXCaptureRig";
        private const string CAPTURE_SCENE_PATH = "Assets/Scenes/VFXCaptureScene.unity";
        
        private static readonly Dictionary<string, Vector3> CAMERA_POSITIONS = new Dictionary<string, Vector3>
        {
            { "Front", new Vector3(0, 0, -5) },
            { "Back", new Vector3(0, 0, 5) },
            { "Left", new Vector3(-5, 0, 0) },
            { "Right", new Vector3(5, 0, 0) },
            { "Top", new Vector3(0, 5, 0) },
            { "Bottom", new Vector3(0, -5, 0) },
            { "FrontRight", new Vector3(3.5f, 0, -3.5f) },
            { "BackLeft", new Vector3(-3.5f, 0, 3.5f) }
        };

        public static object HandleCommand(JObject @params)
        {
            // Save current state
            var wasPlaying = EditorApplication.isPlaying;
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string originalScenePath = activeScene.path;
            
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

                var graphName = Path.GetFileNameWithoutExtension(graphPath);
                Color backgroundColor = backgroundColorStr.ToLower() == "white" ? Color.white : Color.black;

                Debug.Log($"[CaptureVfxPlayMode] Starting Play Mode capture: {graphName}");

                // Setup capture scene
                var captureScene = EnsureCleanCaptureScene();
                if (!captureScene.IsValid())
                    return Response.Error("Failed to create clean capture scene");
                
                UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(captureScene);

                // Enter Play Mode
                if (!EditorApplication.isPlaying)
                {
                    Debug.Log("[CaptureVfxPlayMode] Entering Play Mode...");
                    EditorApplication.isPlaying = true;
                    
                    // Wait for Play Mode to start (non-blocking check)
                    int waitCount = 0;
                    while (!EditorApplication.isPlaying && waitCount < 100)
                    {
                        System.Threading.Thread.Sleep(50);
                        waitCount++;
                    }
                    
                    if (!EditorApplication.isPlaying)
                        return Response.Error("Failed to enter Play Mode");
                }

                // Wait a bit for scene to load in Play Mode
                System.Threading.Thread.Sleep(500);

                // Create VFX instance in Play Mode
                GameObject vfxGO = new GameObject("PlayModeVFX");
                vfxGO.transform.position = Vector3.zero;
                var visualEffect = vfxGO.AddComponent<VisualEffect>();
                visualEffect.visualEffectAsset = graphAsset;
                visualEffect.enabled = true;
                visualEffect.Play();

                // Create capture rig
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
                Debug.Log($"[CaptureVfxPlayMode] Output directory: {absoluteOutputDir}");

                // Wait for VFX to start
                System.Threading.Thread.Sleep(500);

                // Calculate frame times
                var frameTimes = Enumerable.Range(0, frameCount)
                    .Select(i => (float)i / Mathf.Max(1, frameCount - 1) * duration)
                    .ToArray();

                var frameDelta = duration / Mathf.Max(1, frameCount - 1);
                var frameTimeMs = frameDelta * 1000f;

                var captureResults = new List<object>();

                // Capture frames in Play Mode
                for (int frameIdx = 0; frameIdx < frameCount; frameIdx++)
                {
                    // Wait for frame time
                    if (frameIdx > 0)
                    {
                        System.Threading.Thread.Sleep((int)frameTimeMs);
                    }

                    // Render all cameras
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
                        Debug.Log($"[CaptureVfxPlayMode] Saved frame: {filePath} ({bytes.Length} bytes)");

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

                // Cleanup RenderTextures
                foreach (var cameraInfo in cameras)
                {
                    if (cameraInfo.Camera != null)
                        cameraInfo.Camera.targetTexture = null;
                    if (cameraInfo.RenderTexture != null)
                        UnityEngine.Object.DestroyImmediate(cameraInfo.RenderTexture);
                }

                // Exit Play Mode
                if (EditorApplication.isPlaying)
                {
                    Debug.Log("[CaptureVfxPlayMode] Exiting Play Mode...");
                    EditorApplication.isPlaying = false;
                    
                    // Wait for Play Mode to exit
                    int waitCount = 0;
                    while (EditorApplication.isPlaying && waitCount < 100)
                    {
                        System.Threading.Thread.Sleep(50);
                        waitCount++;
                    }
                }

                // Restore original scene
                if (!string.IsNullOrEmpty(originalScenePath))
                {
                    var sceneToRestore = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(originalScenePath);
                    if (!sceneToRestore.IsValid())
                    {
                        sceneToRestore = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    }
                    if (sceneToRestore.IsValid())
                    {
                        UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(sceneToRestore);
                    }
                }

                return Response.Success($"Captured {frameCount} frames from {cameras.Count} cameras in Play Mode", new
                {
                    graphId = graphName,
                    frameCount = frameCount,
                    cameraCount = cameras.Count,
                    outputDir = absoluteOutputDir,
                    captures = captureResults
                });
            }
            catch (Exception ex)
            {
                // Ensure we exit Play Mode on error
                if (EditorApplication.isPlaying)
                {
                    EditorApplication.isPlaying = false;
                }
                
                // Restore original scene
                if (!string.IsNullOrEmpty(originalScenePath))
                {
                    try
                    {
                        var sceneToRestore = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(originalScenePath);
                        if (!sceneToRestore.IsValid())
                        {
                            sceneToRestore = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(originalScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                        }
                        if (sceneToRestore.IsValid())
                        {
                            UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(sceneToRestore);
                        }
                    }
                    catch { }
                }
                
                return Response.Error($"Failed to capture VFX in Play Mode: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static UnityEngine.SceneManagement.Scene EnsureCleanCaptureScene()
        {
            var existingScene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(CAPTURE_SCENE_PATH);
            if (existingScene.IsValid())
            {
                if (!existingScene.isLoaded)
                {
                    existingScene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CAPTURE_SCENE_PATH, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }
                else
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(existingScene);
                }
                
                // Clear all objects
                var rootObjects = existingScene.GetRootGameObjects();
                foreach (var obj in rootObjects)
                {
                    UnityEngine.Object.DestroyImmediate(obj);
                }
                return existingScene;
            }
            
            var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.DefaultGameObjects,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, CAPTURE_SCENE_PATH);
            return newScene;
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
    }
}

