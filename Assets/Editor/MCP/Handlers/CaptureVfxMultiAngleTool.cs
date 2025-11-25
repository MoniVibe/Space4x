using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PureDOTS.Editor.MCP.Helpers;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using VFXPlayground.Capture;

#nullable enable

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("capture_vfx_multi_angle")]
    public static class CaptureVfxMultiAngleTool
    {
        private const string CaptureScenePath = "Assets/Scenes/VFXCaptureScene.unity";
        private const string VfxInstanceName = "VFXInstance";

        private static readonly Color DefaultBackground = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly string[] DefaultCameraOrder = { "front", "left", "right", "back", "top", "diagonal" };
        private const float DefaultCameraDistance = 5f;

        [MenuItem("VFX/Setup Capture Scene")]
        public static void SetupCaptureSceneMenuItem()
        {
            var scene = LoadCaptureScene();
            if (!scene.IsValid())
            {
                Debug.Log($"[CaptureVfxMultiAngle] Creating new capture scene at {CaptureScenePath}");
                scene = CreateCaptureScene();
            }
            else
            {
                // Recreate the scene to ensure it's properly set up
                Debug.Log($"[CaptureVfxMultiAngle] Recreating capture scene at {CaptureScenePath}");
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                scene = CreateCaptureScene();
            }

            if (scene.IsValid())
            {
                EditorSceneManager.SetActiveScene(scene);
                Debug.Log($"[CaptureVfxMultiAngle] Capture scene ready at {CaptureScenePath}");
            }
        }
        
        public static object HandleCommand(JObject @params)
        {
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            string activeScenePath = activeScene.path;
            bool wasPlaying = EditorApplication.isPlaying;

            try
            {
                string graphId = @params["graph_id"]?.ToString();
                string graphPath = @params["graph_path"]?.ToString();
                int width = @params["width"]?.ToObject<int?>() ?? 512;
                int height = @params["height"]?.ToObject<int?>() ?? 512;
                // Default duration to 8s baseline for all captures
                float duration = Mathf.Max(0.1f, @params["duration"]?.ToObject<float?>() ?? 8f);
                float preRollSeconds = Mathf.Max(0f, @params["pre_roll"]?.ToObject<float?>() ?? 0.2f);
                // Default to 6 frames when frame_count is not specified (all captures use baseline duration)
                int? requestedFrameCount = @params["frame_count"]?.ToObject<int?>();
                if (requestedFrameCount == null || requestedFrameCount == 0)
                {
                    requestedFrameCount = 6; // Default to 6 frames for baseline captures
                }
                var frameTimes = ParseFrameTimes(@params["frame_times"] as JArray, duration, requestedFrameCount ?? 6);
                var cameraNamesFilter = ParseCameraNames(@params["camera_names"]);
                var backgroundColor = ParseColor(@params["background_color"]) ?? DefaultBackground;
                var parameterToken = (@params["parameters"] ?? @params["params"]) as JObject;
                string baseOutputDir = @params["output_dir"]?.ToString() ?? "Data/MCP_Exports";

                // Auto-detect VFX from active scene if not provided
                VisualEffectAsset detectedGraphAsset = null;
                if (string.IsNullOrEmpty(graphPath) && string.IsNullOrEmpty(graphId))
                {
                    var activeVfx = FindActiveVisualEffect();
                    if (activeVfx != null && activeVfx.visualEffectAsset != null)
                    {
                        detectedGraphAsset = activeVfx.visualEffectAsset;
                        graphPath = AssetDatabase.GetAssetPath(detectedGraphAsset);
                        if (!string.IsNullOrEmpty(graphPath))
                        {
                            Debug.Log($"[CaptureVfxMultiAngle] Auto-detected VFX from active scene: {Path.GetFileNameWithoutExtension(graphPath)}");
                        }
                    }
                }

                if (string.IsNullOrEmpty(graphPath) && !string.IsNullOrEmpty(graphId))
                {
                    graphPath = ResolveGraphIdToPath(graphId);
                    if (string.IsNullOrEmpty(graphPath))
                        return Response.Error($"Graph with id '{graphId}' not found");
                }

                if (string.IsNullOrEmpty(graphPath))
                    return Response.Error("graph_path or graph_id is required, or no active VFX found in scene");

                VisualEffectAsset graphAsset = detectedGraphAsset ?? AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                    return Response.Error($"VFX graph not found at path: {graphPath}");

                var graphName = Path.GetFileNameWithoutExtension(graphPath) ?? "VFX";
                
                // Create output directory named after the VFX graph
                string outputDir = Path.Combine(baseOutputDir, graphName).Replace('\\', '/');

                var captureScene = LoadCaptureScene();
                if (!captureScene.IsValid())
                {
                    Debug.Log("[CaptureVfxMultiAngle] Capture scene missing—creating default scene.");
                    captureScene = CreateCaptureScene();
                    if (!captureScene.IsValid())
                        return Response.Error($"Failed to create capture scene at {CaptureScenePath}");
                }

                UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(captureScene);
                Thread.Sleep(200);

                // Clean up scene: disable all unrelated VFX and particle systems
                CleanupSceneForCapture(captureScene, graphAsset);

                var vfxInstance = FindVfxInstance(captureScene);
                if (vfxInstance == null)
                    return Response.Error($"VFX instance '{VfxInstanceName}' not found in capture scene.");

                var visualEffect = vfxInstance.GetComponent<VisualEffect>();
                if (visualEffect == null)
                    return Response.Error($"VFX instance '{VfxInstanceName}' does not have a VisualEffect component.");

                // Set the VFX asset but don't play yet (will play after entering play mode)
                visualEffect.visualEffectAsset = graphAsset;
                visualEffect.enabled = true;
                // Don't play yet - will play after entering play mode to ensure proper rendering

                var cameras = UnityEngine.Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.gameObject.scene == captureScene)
                    .ToList();

                if (cameras.Count == 0)
                {
                    Debug.LogError($"[CaptureVfxMultiAngle] No cameras found in capture scene '{captureScene.name}'. Scene path: {CaptureScenePath}");
                    return Response.Error("No cameras found in capture scene.");
                }
                
                Debug.Log($"[CaptureVfxMultiAngle] Found {cameras.Count} cameras: {string.Join(", ", cameras.Select(c => c.gameObject.name))}");

                if (cameraNamesFilter != null && cameraNamesFilter.Count > 0)
                {
                    cameras = cameras
                        .Where(c => cameraNamesFilter.Contains(NormalizeCameraName(c.gameObject.name)))
                        .ToList();
                }

                if (cameras.Count == 0)
                    return Response.Error("Requested cameras were not found in the capture scene.");

                // Sort cameras to maintain deterministic ordering
                cameras = cameras
                    .OrderBy(c => Array.IndexOf(DefaultCameraOrder, NormalizeCameraName(c.gameObject.name)))
                    .ThenBy(c => c.gameObject.name, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var cameraInfos = PrepareCameras(cameras, width, height, backgroundColor);
                AdjustCameraDistances(cameraInfos, visualEffect);
                
                // Add debug component for real-time camera visualization and adjustment
                var debugComponent = captureScene.GetRootGameObjects()
                    .SelectMany(go => go.GetComponentsInChildren<VfxCaptureCameraDebug>())
                    .FirstOrDefault();
                
                if (debugComponent == null)
                {
                    try
                    {
                        var debugGO = new GameObject("VfxCaptureCameraDebug");
                        debugComponent = debugGO.AddComponent<VfxCaptureCameraDebug>();
                        if (debugComponent != null)
                        {
                            debugComponent.showCameraPreviews = true;
                            debugComponent.showGizmos = true;
                            Debug.Log("[CaptureVfxMultiAngle] Added VfxCaptureCameraDebug component for real-time camera adjustment");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        // VfxCaptureCameraDebug is an editor script and can't be attached to runtime GameObjects
                        // This is fine - debug visualization is optional
                        Debug.LogWarning($"[CaptureVfxMultiAngle] Could not add VfxCaptureCameraDebug component (this is normal): {ex.Message}");
                        debugComponent = null;
                    }
                }

                if (parameterToken != null)
                {
                    ApplyParameters(visualEffect, parameterToken);
                }

                var captureResults = CaptureFrames(cameraInfos, graphName, frameTimes, outputDir, visualEffect, graphAsset, graphPath, preRollSeconds, wasPlaying);

                // Parameters and descriptor are now saved inside CaptureFrames before exiting play mode
                var absoluteOutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));
                
                // Load saved parameters for response (they're already saved to disk)
                var currentParams = new Dictionary<string, object>();
                var paramsPath = Path.Combine(absoluteOutputDir, $"{graphName}_params.json");
                if (File.Exists(paramsPath))
                {
                    try
                    {
                        var paramsJson = File.ReadAllText(paramsPath);
                        currentParams = JsonConvert.DeserializeObject<Dictionary<string, object>>(paramsJson) ?? currentParams;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to load saved parameters: {ex.Message}");
                    }
                }

                if (!wasPlaying && EditorApplication.isPlaying)
                {
                    ExitPlayModeWithWait();
                }

                RestoreActiveScene(activeScenePath);

                // Check if descriptor was saved
                var descriptorPath = Path.Combine(absoluteOutputDir, $"{graphName}_descriptor.json");
                var descriptorSaved = File.Exists(descriptorPath);
                
                return Response.Success($"Captured {frameTimes.Count} frames from {cameraInfos.Count} cameras", new
                {
                    graphId = graphName,
                    cameras = cameraInfos.Select(c => c.Name).ToArray(),
                    frameCount = frameTimes.Count,
                    duration,
                    frameTimes = frameTimes.ToArray(),
                    preRoll = preRollSeconds,
                    captures = captureResults,
                    parameters = currentParams,
                    parametersPath = paramsPath,
                    descriptorPath = descriptorSaved ? descriptorPath : null,
                    outputDir = outputDir,  // Relative path: Data/MCP_Exports/{graphName}
                    absoluteOutputDir = absoluteOutputDir  // Absolute path for file operations
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureVfxMultiAngle] Error: {ex.Message}\n{ex.StackTrace}");

               if (!wasPlaying && EditorApplication.isPlaying)
                {
                    try { EditorApplication.isPlaying = false; } catch { }
                }

                RestoreActiveScene(activeScenePath);
                return Response.Error($"Failed to capture multi-angle VFX: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private static List<object> CaptureFrames(
            List<CameraCaptureInfo> cameras,
            string graphName,
            IReadOnlyList<float> frameTimes,
            string outputDir,
            VisualEffect visualEffect,
            VisualEffectAsset graphAsset,
            string graphPath,
            float preRollSeconds,
            bool wasPlaying)
        {
            var absoluteOutputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir));
            Directory.CreateDirectory(absoluteOutputDir);

            var results = new Dictionary<string, List<object>>();
            foreach (var camera in cameras)
            {
                results[camera.Name] = new List<object>();
            }

            bool enteredPlayMode = EnsurePlayMode();
            
            // Ensure VFX is properly set up and playing after entering play mode
            if (visualEffect != null)
            {
                // Re-assign asset and ensure it's enabled (play mode might reset state)
                if (visualEffect.visualEffectAsset == null && graphAsset != null)
                {
                    Debug.LogWarning("[CaptureVfxMultiAngle] VFX asset was null, reassigning from graphAsset");
                    visualEffect.visualEffectAsset = graphAsset;
                }
                
                // Try to reload from the visual effect's gameObject scene if still null
                if (visualEffect.visualEffectAsset == null)
                {
                    var vfxScene = visualEffect.gameObject.scene;
                    var vfxInstance = FindVfxInstance(vfxScene);
                    if (vfxInstance != null)
                    {
                        var vfxComponent = vfxInstance.GetComponent<VisualEffect>();
                        if (vfxComponent != null && vfxComponent.visualEffectAsset != null)
                        {
                            Debug.Log("[CaptureVfxMultiAngle] Reloaded VFX asset from scene instance");
                            visualEffect.visualEffectAsset = vfxComponent.visualEffectAsset;
                        }
                    }
                }
                
                if (visualEffect.visualEffectAsset == null)
                {
                    Debug.LogError("[CaptureVfxMultiAngle] VFX asset is still null after attempts to reload!");
                }
                else
                {
                    Debug.Log($"[CaptureVfxMultiAngle] VFX asset: {visualEffect.visualEffectAsset.name}");
                }
                
                visualEffect.enabled = true;
                visualEffect.Reinit(); // Reinitialize to ensure proper state
                visualEffect.Play();
                
                // Wait a moment for VFX to start spawning particles so bounds can be calculated
                SleepWithUpdates(0.5f);
                
                // Verify VFX is actually playing
                if (!visualEffect.enabled)
                {
                    Debug.LogWarning("[CaptureVfxMultiAngle] VFX was disabled after initialization, re-enabling...");
                    visualEffect.enabled = true;
                    visualEffect.Play();
                    SleepWithUpdates(0.2f);
                }
                
                // Re-adjust camera distances now that VFX is actually rendering
                AdjustCameraDistances(cameras, visualEffect);
            }
            else
            {
                Debug.LogError("[CaptureVfxMultiAngle] VisualEffect is null in CaptureFrames!");
            }

            float preRoll = Mathf.Max(preRollSeconds, 0.15f);
            Debug.Log($"[CaptureVfxMultiAngle] Pre-roll {preRoll:F2}s before capturing");
            SleepWithUpdates(preRoll);

            double captureStart = EditorApplication.timeSinceStartup;

            for (int frameIndex = 0; frameIndex < frameTimes.Count; frameIndex++)
            {
                float frameTime = frameTimes[frameIndex];
                WaitUntilTime(captureStart + frameTime);

                foreach (var info in cameras)
                {
                    var entry = CaptureFrameFromCamera(info, graphName, frameIndex, frameTime, absoluteOutputDir);
                    if (entry != null)
                    {
                        results[info.Name].Add(entry);
                        Debug.Log($"[CaptureVfxMultiAngle] Captured frame {frameIndex} at {frameTime:F2}s for camera {info.Name}");
                    }
                }
            }

            if (frameTimes.Count == 0)
            {
                Debug.LogWarning("[CaptureVfxMultiAngle] No frame times specified; nothing captured");
            }

            var screenshotOutputDir = absoluteOutputDir;

                // Extract and save parameters and descriptor BEFORE exiting play mode
                // (VFX state might be reset when exiting play mode)
                // Use the exact same directory where screenshots are saved
                if (visualEffect != null && graphAsset != null && !string.IsNullOrEmpty(graphPath))
                {
                    Debug.Log("[CaptureVfxMultiAngle] Extracting parameters and graph descriptor before exiting play mode...");
                    
                    // Capture current parameter values while still in play mode
                    var currentParams = ExtractParameters(visualEffect);
                    
                    // Extract asset bindings separately for logging
                    var assetBindings = new Dictionary<string, object>();
                    if (currentParams.TryGetValue("_asset_bindings", out var bindingsObj))
                    {
                        if (bindingsObj is Dictionary<string, object> bindingsDict)
                        {
                            assetBindings = bindingsDict;
                        }
                        else if (bindingsObj is Dictionary<string, string> bindingsStr)
                        {
                            foreach (var kvp in bindingsStr)
                            {
                                assetBindings[kvp.Key] = kvp.Value;
                            }
                        }
                        currentParams.Remove("_asset_bindings"); // Remove from params, save separately
                    }
                    
                    // Get graph descriptor (doesn't require play mode, but do it here for consistency)
                    var graphDescriptor = GetGraphDescriptor(graphAsset, graphPath);

                    // Save parameters, asset bindings, and descriptor to the exact same directory where screenshots are saved
                    SaveParametersAndDescriptor(screenshotOutputDir, graphName, currentParams, graphDescriptor, assetBindings);
                    
                    Debug.Log($"[CaptureVfxMultiAngle] Saved {currentParams.Count} parameters, {assetBindings.Count} asset bindings, and graph descriptor for {graphName} to {screenshotOutputDir}");
                }

            if (enteredPlayMode && !wasPlaying)
            {
                ExitPlayModeWithWait();
            }

            var response = new List<object>();
            foreach (var camera in cameras)
            {
                response.Add(new
                {
                    cameraName = camera.Name,
                    frames = results[camera.Name]
                });
            }

            CleanupCameraResources(cameras);

            return response;
        }

        private static object? CaptureFrameFromRenderTexture(CameraCaptureInfo info, string graphName, int frameIndex, float frameTime, string outputDir)
        {
            if (info.RenderTexture == null || info.Camera == null)
                return null;

            // At this point, the render pipeline has completed rendering to the RenderTexture
            // Copy from RenderTexture to Texture2D for CPU readback
            var previousActive = RenderTexture.active;
            RenderTexture.active = info.RenderTexture;

            try
            {
                // Read pixels from the active RenderTexture
                info.ReadbackTexture.ReadPixels(new Rect(0, 0, info.RenderTexture.width, info.RenderTexture.height), 0, 0);
                info.ReadbackTexture.Apply(false);

                var fileName = $"{graphName}_{info.Name}_frame{frameIndex:D3}.png";
                var filePath = Path.Combine(outputDir, fileName);
                var pngData = info.ReadbackTexture.EncodeToPNG();
                
                if (pngData == null || pngData.Length == 0)
                {
                    Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to encode PNG for {fileName} - texture may be empty");
                    return null;
                }
                
                File.WriteAllBytes(filePath, pngData);
                Debug.Log($"[CaptureVfxMultiAngle] Captured frame {frameIndex} from camera {info.Name} to {fileName} ({pngData.Length} bytes)");

                return new
                {
                    frameIndex,
                    frameTime,
                    fileName,
                    path = filePath
                };
            }
            finally
            {
                RenderTexture.active = previousActive;
            }
        }

        private static object? CaptureFrameFromCamera(CameraCaptureInfo info, string graphName, int frameIndex, float frameTime, string outputDir)
        {
            if (info.Camera == null || info.RenderTexture == null)
                return null;

            var camera = info.Camera;
            var previousTarget = camera.targetTexture;
            var previousForce = camera.forceIntoRenderTexture;
            var previousClearFlags = camera.clearFlags;
            var previousBackground = camera.backgroundColor;
            var previousRect = camera.rect;

            try
            {
                // Clear the RenderTexture with the configured background color first
                RenderTexture.active = info.RenderTexture;
                GL.Clear(true, true, info.ConfiguredBackground);
                RenderTexture.active = null;
                
                // Configure camera for RenderTexture capture
                camera.targetTexture = info.RenderTexture;
                camera.forceIntoRenderTexture = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = info.ConfiguredBackground; // Use the configured background color
                camera.rect = new Rect(0f, 0f, 1f, 1f); // Render full frame for capture
                camera.aspect = (float)info.RenderTexture.width / info.RenderTexture.height;
                camera.Render();

                return CaptureFrameFromRenderTexture(info, graphName, frameIndex, frameTime, outputDir);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                camera.forceIntoRenderTexture = previousForce;
                camera.clearFlags = previousClearFlags;
                camera.backgroundColor = previousBackground;
                camera.rect = previousRect;
                camera.aspect = info.OriginalAspect;
            }
        }

        private static void WaitUntilTime(double targetTime)
        {
            const int maxSleepMs = 20;
            while (EditorApplication.timeSinceStartup < targetTime)
            {
                double remaining = targetTime - EditorApplication.timeSinceStartup;
                int sleep = remaining > 0.05 ? maxSleepMs : 5;
                Thread.Sleep(sleep);
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        #region Helpers

        private static List<float> ParseFrameTimes(JArray? frameTimesArray, float duration, int requestedCount)
        {
            if (frameTimesArray != null && frameTimesArray.Count > 0)
            {
                return frameTimesArray
                    .Select(t => Mathf.Clamp(t.Value<float>(), 0f, duration))
                    .Distinct()
                    .OrderBy(v => v)
                    .ToList();
            }

            // Default to 6 frames with hardcoded timings for baseline captures
            // All captures use the same baseline duration, so always use 6 frames unless explicitly overridden
            if (requestedCount == 6 || requestedCount == 0)
            {
                var defaultTimes = new List<float>
                {
                    0.3f,
                    1.3f,
                    2.3f,
                    3.5f,
                    5.0f,
                    6.0f
                };

                Debug.Log($"[CaptureVfxMultiAngle] Using default 6 frames: {string.Join(", ", defaultTimes.Select(t => $"{t:F2}s"))}");
                return defaultTimes;
            }

            // Use requested count if explicitly specified
            int effectiveCount = requestedCount > 0 ? requestedCount : 6;
            Debug.Log($"[CaptureVfxMultiAngle] ParseFrameTimes: requestedCount={requestedCount}, duration={duration}, effectiveCount={effectiveCount}");

            float[] baseFractions = effectiveCount switch
            {
                3 => new[] { 0.25f, 0.5f, 0.75f },
                4 => new[] { 0.2f, 0.4f, 0.6f, 0.8f },
                5 => new[] { 0.15f, 0.35f, 0.55f, 0.75f, 0.9f },
                6 => new[] { 0.12f, 0.3f, 0.48f, 0.66f, 0.84f, 0.96f },
                _ => Enumerable.Range(1, effectiveCount).Select(i => (float)i / (effectiveCount + 1)).ToArray()
            };

            var frameTimesList = baseFractions
                .Select(f => Mathf.Clamp(f * duration, 0f, duration))
                .ToList();
            
            Debug.Log($"[CaptureVfxMultiAngle] Generated {frameTimesList.Count} frame times: {string.Join(", ", frameTimesList.Select(t => $"{t:F2}s"))}");
            
            return frameTimesList;
        }

        private static HashSet<string>? ParseCameraNames(JToken? token)
        {
            if (token == null)
                return null;

            var names = token.Type switch
            {
                JTokenType.Array => token.Values<string>(),
                JTokenType.String => new[] { token.Value<string>() },
                _ => Enumerable.Empty<string>()
            };

            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var name in names)
            {
                if (!string.IsNullOrWhiteSpace(name))
                    set.Add(NormalizeCameraName(name));
            }

            return set.Count > 0 ? set : null;
        }

        private static Color? ParseColor(JToken? token)
        {
            if (token == null)
                return null;

            if (token is JArray arr)
            {
                var values = arr.Select(v => v.Value<float>()).ToArray();
                if (values.Length >= 3)
                {
                    return new Color(
                        Mathf.Clamp01(values[0]),
                        Mathf.Clamp01(values[1]),
                        Mathf.Clamp01(values[2]),
                        values.Length > 3 ? Mathf.Clamp01(values[3]) : 1f);
                }
            }
            else if (token is JObject obj)
            {
                float r = obj["r"]?.Value<float>() ?? 0.5f;
                float g = obj["g"]?.Value<float>() ?? 0.5f;
                float b = obj["b"]?.Value<float>() ?? 0.5f;
                float a = obj["a"]?.Value<float>() ?? 1f;
                return new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), Mathf.Clamp01(a));
            }

            return null;
        }

        private static string NormalizeCameraName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return name;

            return name
                .Replace("CaptureCamera_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("Camera_", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
                .ToLowerInvariant();
        }

        private static bool EnsurePlayMode()
        {
            if (EditorApplication.isPlaying)
                return false;

            Debug.Log("[CaptureVfxMultiAngle] Entering Play Mode for VFX rendering...");
            EditorApplication.isPlaying = true;

            int waitCount = 0;
            while (!EditorApplication.isPlaying && waitCount < 200)
            {
                Thread.Sleep(50);
                EditorApplication.QueuePlayerLoopUpdate();
                waitCount++;
            }

            if (EditorApplication.isPlaying)
            {
                Debug.Log("[CaptureVfxMultiAngle] Successfully entered Play Mode");
                Thread.Sleep(200); // small settle time
                return true;
            }

            Debug.LogWarning("[CaptureVfxMultiAngle] Failed to enter Play Mode, continuing in edit mode");
            return false;
        }

        private static void ExitPlayModeWithWait()
        {
            if (!EditorApplication.isPlaying)
                return;

            EditorApplication.isPlaying = false;
            int waitCount = 0;
            while (EditorApplication.isPlaying && waitCount < 100)
            {
                Thread.Sleep(50);
                waitCount++;
            }
        }

        private static void SleepWithUpdates(float seconds)
        {
            if (seconds <= 0f)
                return;

            int totalMs = Mathf.CeilToInt(seconds * 1000f);
            int elapsed = 0;
            const int slice = 15;

            while (elapsed < totalMs)
            {
                int wait = Mathf.Min(slice, totalMs - elapsed);
                Thread.Sleep(wait);
                EditorApplication.QueuePlayerLoopUpdate();
                elapsed += wait;
            }
        }

        private static List<CameraCaptureInfo> PrepareCameras(IEnumerable<Camera> cameras, int width, int height, Color background)
        {
            var cameraList = cameras.ToList();
            var infos = new List<CameraCaptureInfo>();
            
            // Calculate grid layout for viewports
            int cameraCount = cameraList.Count;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(cameraCount));
            int rows = Mathf.CeilToInt((float)cameraCount / cols);
            
            for (int i = 0; i < cameraList.Count; i++)
            {
                var camera = cameraList[i];
                
                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"CaptureRT_{camera.gameObject.name}"
                };
                renderTexture.Create();

                var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                // Calculate viewport rect for this camera (split-screen layout)
                int col = i % cols;
                int row = i / cols;
                float viewportWidth = 1f / cols;
                float viewportHeight = 1f / rows;
                float viewportX = col * viewportWidth;
                float viewportY = 1f - (row + 1) * viewportHeight; // Unity uses bottom-left origin
                
                var info = new CameraCaptureInfo
                {
                    Camera = camera,
                    RenderTexture = renderTexture,
                    ReadbackTexture = readback,
                    OriginalBackground = camera.backgroundColor,
                    OriginalClearFlags = camera.clearFlags,
                    OriginalRenderTexture = camera.targetTexture,
                    OriginalRect = camera.rect,
                    OriginalTag = camera.tag,
                    OriginalForceIntoRenderTexture = camera.forceIntoRenderTexture,
                    OriginalAspect = camera.aspect,
                    Name = NormalizeCameraName(camera.gameObject.name),
                    ConfiguredBackground = background, // Store the configured background color
                };
                
                // Set viewport rect so camera renders to its portion of the screen
                camera.rect = new Rect(viewportX, viewportY, viewportWidth, viewportHeight);
                
                // Keep targetTexture null so cameras continue rendering to the Game view
                camera.targetTexture = info.OriginalRenderTexture;
                camera.forceIntoRenderTexture = false; // Allow rendering to viewport
                camera.targetDisplay = 0; // Ensure camera outputs to Display 1 (Game view)
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
                camera.enabled = true; // Ensure camera is enabled
                if (!camera.gameObject.activeInHierarchy)
                {
                    camera.gameObject.SetActive(true);
                }
                
                // Ensure at least one camera is tagged as MainCamera (some effects expect it)
                if (i == 0)
                {
                    camera.tag = "MainCamera";
                }
                
                Debug.Log($"[CaptureVfxMultiAngle] Camera '{camera.gameObject.name}' viewport: ({viewportX:F2}, {viewportY:F2}, {viewportWidth:F2}, {viewportHeight:F2})");

                infos.Add(info);
            }

            return infos;
        }

        private static void AdjustCameraDistances(List<CameraCaptureInfo> cameras, VisualEffect visualEffect)
        {
            if (cameras == null || cameras.Count == 0 || visualEffect == null)
                return;

            // Use bounds center if available, otherwise use transform position
            var rendererBounds = GetRendererBounds(visualEffect.gameObject);
            var focusPosition = rendererBounds.HasValue && rendererBounds.Value.extents != Vector3.zero
                ? rendererBounds.Value.center
                : visualEffect.transform.position;
            float minY = rendererBounds.HasValue ? rendererBounds.Value.min.y : focusPosition.y;
            float maxY = rendererBounds.HasValue ? rendererBounds.Value.max.y : focusPosition.y;
            float boundsHeight = Mathf.Max(maxY - minY, 0.1f);
            
            var (desiredDistance, isWideFlat) = CalculateDesiredCameraDistance(visualEffect);
            
            // Cap maximum distance to prevent cameras from going too far
            const float maxDistance = 50f;
            desiredDistance = Mathf.Min(desiredDistance, maxDistance);

            // Standard camera directions based on camera name
            var cameraDirections = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase)
            {
                { "front", Vector3.back },
                { "back", Vector3.forward },
                { "left", Vector3.left },
                { "right", Vector3.right },
                { "top", Vector3.up },
                { "bottom", Vector3.down },
                { "diagonal", new Vector3(-1f, 1f, -1f).normalized } // Diagonal: forward-right-up direction
            };

            foreach (var info in cameras)
            {
                if (info?.Camera == null)
                    continue;

                var cameraName = NormalizeCameraName(info.Camera.gameObject.name);
                
                // Get standard direction for this camera, fallback to current direction if not found
                Vector3 direction;
                if (cameraDirections.TryGetValue(cameraName, out var standardDir))
                {
                    direction = standardDir;
                }
                else
                {
                    // Fallback: calculate from current position
                    direction = (info.Camera.transform.position - focusPosition);
                    if (direction.sqrMagnitude < 1e-4f)
                    {
                        direction = -info.Camera.transform.forward;
                        if (direction.sqrMagnitude < 1e-4f)
                            direction = Vector3.back;
                    }
                    direction.Normalize();
                }
                
                // For wide/flat VFX, adjust camera positioning and FOV
                float cameraDistance = desiredDistance;
                float cameraFOV = info.Camera.fieldOfView;
                
                if (isWideFlat)
                {
                    Debug.Log($"[CaptureVfxMultiAngle] Wide/flat VFX detected, adjusting camera '{cameraName}'");
                    
                    // For top/bottom cameras viewing flat effects, increase FOV to capture more width
                    if (cameraName == "top" || cameraName == "bottom")
                    {
                        cameraFOV = Mathf.Max(cameraFOV, 90f); // Wider FOV for top/bottom views
                        Debug.Log($"[CaptureVfxMultiAngle] Top/bottom camera FOV set to {cameraFOV}°");
                    }
                    // For side cameras, move further back but not too far (reduced multiplier)
                    else if (cameraName == "front" || cameraName == "back" || cameraName == "left" || cameraName == "right")
                    {
                        cameraDistance = Mathf.Min(desiredDistance * 1.8f, maxDistance); // Reduced from 2.5f to 1.8f
                        Debug.Log($"[CaptureVfxMultiAngle] Side camera distance set to {cameraDistance}m (base: {desiredDistance}m)");
                    }
                }
                
                // Ensure minimum distance to prevent cameras from being too close
                cameraDistance = Mathf.Max(cameraDistance, 3f);
                
                info.Camera.transform.position = focusPosition + direction * cameraDistance;

                // Determine look target anchored toward the base of the effect for ground-level cameras
                Vector3 lookTarget = focusPosition;
                Vector3 upVector = Vector3.up; // Default up vector

                if (rendererBounds.HasValue)
                {
                    var bounds = rendererBounds.Value;
                    switch (cameraName)
                    {
                        case "front":
                        case "back":
                        case "left":
                        case "right":
                        {
                            // Anchor roughly one third up from the base so the flame/particles sit on the ground line
                            float anchorRatio = 0.35f;
                            lookTarget = new Vector3(bounds.center.x, Mathf.Lerp(bounds.min.y, bounds.max.y, anchorRatio), bounds.center.z);
                            break;
                        }
                        case "top":
                        case "bottom":
                        {
                            lookTarget = bounds.center;
                            upVector = Vector3.forward;
                            break;
                        }
                    }
                }

                Vector3 lookDirection = (lookTarget - info.Camera.transform.position).normalized;
                if (lookDirection.sqrMagnitude < 0.01f)
                {
                    // Fallback if we somehow ended up inside the bounds
                    lookDirection = (focusPosition - info.Camera.transform.position).normalized;
                }

                info.Camera.transform.rotation = Quaternion.LookRotation(lookDirection, upVector);
                info.Camera.fieldOfView = cameraFOV;
                info.Camera.enabled = true; // Ensure camera is enabled

                // Skip auto-adjustment for diagonal camera - preserve its exact position
                if (cameraName == "diagonal")
                {
                    Debug.Log($"[CaptureVfxMultiAngle] Preserving diagonal camera position: {info.Camera.transform.position}");
                    continue;
                }
                
                if (rendererBounds.HasValue && (cameraName == "front" || cameraName == "back" || cameraName == "left" || cameraName == "right"))
                {
                    AnchorBoundsToBottom(info.Camera, rendererBounds.Value, lookTarget);
                }

                Debug.Log($"[CaptureVfxMultiAngle] Camera '{cameraName}' positioned at {info.Camera.transform.position}, distance: {cameraDistance:F2}m, FOV: {cameraFOV}°, looking at {lookTarget}");
            }
        }

        private static void AnchorBoundsToBottom(Camera camera, Bounds bounds, Vector3 lookTarget)
        {
            if (camera == null)
                return;

            const float desiredBottom = 0.08f; // target viewport Y for base of the effect
            const float tolerance = 0.02f;
            const int maxIterations = 6;

            float height = Mathf.Max(bounds.size.y, 0.1f);
            Vector3 bottomPoint = new(bounds.center.x, bounds.min.y, bounds.center.z);
            Vector3 topPoint = new(bounds.center.x, bounds.max.y, bounds.center.z);

            for (int i = 0; i < maxIterations; i++)
            {
                var bottomViewport = camera.WorldToViewportPoint(bottomPoint);
                if (float.IsNaN(bottomViewport.y) || float.IsInfinity(bottomViewport.y))
                    break;

                bool adjusted = false;

                if (bottomViewport.y > desiredBottom + tolerance)
                {
                    camera.transform.position += Vector3.up * (height * 0.08f);
                    adjusted = true;
                }
                else if (bottomViewport.y < desiredBottom - tolerance)
                {
                    camera.transform.position -= Vector3.up * (height * 0.08f);
                    adjusted = true;
                }

                var topViewport = camera.WorldToViewportPoint(topPoint);
                if (topViewport.y > 0.98f)
                {
                    camera.transform.position += -camera.transform.forward * (height * 0.08f);
                    adjusted = true;
                }

                if (!adjusted)
                    break;

                // Re-aim the camera after adjustments
                Vector3 lookDir = (lookTarget - camera.transform.position).normalized;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    camera.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }
            }
        }

        private static (float distance, bool isWideFlat) CalculateDesiredCameraDistance(VisualEffect visualEffect)
        {
            const float padding = 2f;
            float distance = DefaultCameraDistance;
            bool isWideFlat = false;

            if (visualEffect == null || visualEffect.gameObject == null)
                return (distance, isWideFlat);

            // Try to get bounds from renderers (VFX particles are rendered through renderers)
            var rendererBounds = GetRendererBounds(visualEffect.gameObject);
            if (rendererBounds.HasValue && rendererBounds.Value.extents != Vector3.zero)
            {
                var extents = rendererBounds.Value.extents;
                float boundsSize = extents.magnitude;
                
                // Detect wide/flat VFX: if width or depth is much larger than height (lower threshold for better detection)
                float maxHorizontal = Mathf.Max(extents.x, extents.z);
                float height = extents.y;
                
                Debug.Log($"[CaptureVfxMultiAngle] VFX bounds - X: {extents.x:F2}m, Y: {extents.y:F2}m, Z: {extents.z:F2}m, maxHorizontal: {maxHorizontal:F2}m");
                
                if (maxHorizontal > 0.1f && height > 0.01f)
                {
                    float aspectRatio = maxHorizontal / height;
                    isWideFlat = aspectRatio > 2.0f; // Lower threshold: wide/flat if horizontal is 2x+ larger than vertical (was 3.0f)
                    if (isWideFlat)
                    {
                        Debug.Log($"[CaptureVfxMultiAngle] Wide/flat VFX detected: aspect ratio {aspectRatio:F2} (horizontal {maxHorizontal:F2}m vs height {height:F2}m)");
                    }
                }
                // Also check if height is very small relative to horizontal (very flat effects)
                else if (maxHorizontal > 0.5f && height < 0.2f)
                {
                    isWideFlat = true;
                    Debug.Log($"[CaptureVfxMultiAngle] Very flat VFX detected: horizontal {maxHorizontal:F2}m, height {height:F2}m");
                }
                
                if (boundsSize > 0.1f) // Only use if bounds are meaningful
                {
                    // For wide/flat effects, use the larger horizontal dimension
                    // Note: Additional multiplier will be applied in AdjustCameraDistances for side cameras
                    if (isWideFlat)
                    {
                        distance = Mathf.Max(distance, maxHorizontal * 1.5f + padding); // Reduced from 2.5f since we apply 1.8x in AdjustCameraDistances
                        Debug.Log($"[CaptureVfxMultiAngle] Wide/flat distance calculation: {distance:F2}m (from maxHorizontal {maxHorizontal:F2}m)");
                    }
                    else
                    {
                        distance = Mathf.Max(distance, boundsSize + padding);
                        Debug.Log($"[CaptureVfxMultiAngle] Normal distance calculation: {distance:F2}m (from boundsSize {boundsSize:F2}m)");
                    }
                }
            }

            // Fallback: use transform scale as a hint
            if (distance == DefaultCameraDistance)
            {
                float scaleMagnitude = visualEffect.transform.lossyScale.magnitude;
                if (!float.IsNaN(scaleMagnitude) && scaleMagnitude > 0.1f && scaleMagnitude < 100f)
                {
                    distance = Mathf.Max(distance, scaleMagnitude * 2f + padding);
                }
            }

            return (distance, isWideFlat);
        }

        private static Bounds? GetRendererBounds(GameObject root)
        {
            if (root == null)
                return null;

            var renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers == null || renderers.Length == 0)
                return null;

            var combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                combined.Encapsulate(renderers[i].bounds);
            }

            return combined;
        }

        private static void CleanupCameraResources(IEnumerable<CameraCaptureInfo> cameras)
        {
            foreach (var info in cameras)
            {
                if (info.Camera != null)
                {
                    info.Camera.targetTexture = info.OriginalRenderTexture;
                    info.Camera.clearFlags = info.OriginalClearFlags;
                    info.Camera.backgroundColor = info.OriginalBackground;
                    info.Camera.rect = info.OriginalRect;
                    info.Camera.forceIntoRenderTexture = info.OriginalForceIntoRenderTexture;
                    info.Camera.aspect = info.OriginalAspect;
                    if (!string.IsNullOrEmpty(info.OriginalTag))
                        info.Camera.tag = info.OriginalTag;
                }

                if (info.RenderTexture != null)
                {
                    info.RenderTexture.Release();
                    UnityEngine.Object.DestroyImmediate(info.RenderTexture);
                }

                if (info.ReadbackTexture != null)
                {
                    UnityEngine.Object.DestroyImmediate(info.ReadbackTexture);
                }
            }
        }

        private static void ApplyParameters(VisualEffect visualEffect, JObject parameters)
        {
            foreach (var property in parameters)
            {
                var name = property.Key;
                var token = property.Value;
                if (token == null || token.Type == JTokenType.Null)
                    continue;

                try
                {
                    switch (token.Type)
                    {
                        case JTokenType.Boolean:
                            if (visualEffect.HasBool(name))
                                visualEffect.SetBool(name, token.Value<bool>());
                            break;
                        case JTokenType.Integer:
                        case JTokenType.Float:
                            ApplyNumeric(visualEffect, name, token.Value<float>());
                            break;
                        case JTokenType.Array:
                            ApplyArray(visualEffect, name, (JArray)token);
                            break;
                        case JTokenType.Object:
                            ApplyObject(visualEffect, name, (JObject)token);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to apply parameter '{name}': {ex.Message}");
                }
            }

            visualEffect.Reinit();
        }

        private static void ApplyNumeric(VisualEffect visualEffect, string name, float value)
        {
            if (visualEffect.HasFloat(name))
                visualEffect.SetFloat(name, value);
            else if (visualEffect.HasInt(name))
                visualEffect.SetInt(name, Mathf.RoundToInt(value));
        }

        private static void ApplyArray(VisualEffect visualEffect, string name, JArray array)
        {
            var floats = array.Select(v => v.Value<float>()).ToArray();
            switch (floats.Length)
            {
                case 2 when visualEffect.HasVector2(name):
                    visualEffect.SetVector2(name, new Vector2(floats[0], floats[1]));
                    break;
                case 3:
                    if (visualEffect.HasVector3(name))
                        visualEffect.SetVector3(name, new Vector3(floats[0], floats[1], floats[2]));
                    else if (visualEffect.HasVector4(name))
                        visualEffect.SetVector4(name, new Vector4(floats[0], floats[1], floats[2], 0f));
                    break;
                case 4 when visualEffect.HasVector4(name):
                    visualEffect.SetVector4(name, new Vector4(floats[0], floats[1], floats[2], floats[3]));
                    break;
            }
        }

        private static void ApplyObject(VisualEffect visualEffect, string name, JObject obj)
        {
            if (obj["r"] != null || obj["g"] != null || obj["b"] != null)
            {
                float r = obj["r"]?.Value<float>() ?? 0f;
                float g = obj["g"]?.Value<float>() ?? 0f;
                float b = obj["b"]?.Value<float>() ?? 0f;
                float a = obj["a"]?.Value<float>() ?? 1f;
                if (visualEffect.HasVector4(name))
                    visualEffect.SetVector4(name, new Vector4(r, g, b, a));
                else if (visualEffect.HasVector3(name))
                    visualEffect.SetVector3(name, new Vector3(r, g, b));
            }
        }

        private static UnityEngine.SceneManagement.Scene LoadCaptureScene()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(CaptureScenePath);
            if (scene.IsValid())
            {
                if (!scene.isLoaded)
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CaptureScenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }
                else
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(scene);
                }
            }
            return scene;
        }

        private static UnityEngine.SceneManagement.Scene CreateCaptureScene()
        {
            var newScene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);

            var cameraRig = new GameObject("CameraRig");
            cameraRig.transform.position = Vector3.zero;

            var directions = new Dictionary<string, Vector3>
            {
                { "Front", Vector3.back },
                { "Back", Vector3.forward },
                { "Left", Vector3.left },
                { "Right", Vector3.right },
                { "Top", Vector3.up }
            };

            const float distance = DefaultCameraDistance;
            const float fov = 60f;
            foreach (var (label, direction) in directions)
            {
                var camera = new GameObject($"CaptureCamera_{label}").AddComponent<Camera>();
                camera.transform.SetParent(cameraRig.transform, false);
                camera.transform.position = direction.normalized * distance;
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = DefaultBackground;
                camera.enabled = false; // Disabled by default, enabled during capture
            }

            // Create directional light for proper VFX rendering
            var lightGO = new GameObject("Directional Light");
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = Color.white;
            light.intensity = 1f;
            light.shadows = LightShadows.Soft;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var vfxGO = new GameObject(VfxInstanceName);
            vfxGO.transform.position = Vector3.zero;
            vfxGO.AddComponent<VisualEffect>();

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, CaptureScenePath))
                return default;

            AssetDatabase.Refresh();
            Debug.Log($"[CaptureVfxMultiAngle] Created capture scene at {CaptureScenePath}");
            return newScene;
        }

        private static GameObject? FindVfxInstance(UnityEngine.SceneManagement.Scene scene)
        {
            var instance = GameObject.Find(VfxInstanceName);
            if (instance != null)
                return instance;

            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindInChildren(root.transform, VfxInstanceName);
                if (found != null)
                    return found.gameObject;
            }
            return null;
        }

        private static Transform? FindInChildren(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name)
                    return child;
                var result = FindInChildren(child, name);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static void RestoreActiveScene(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;

            try
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneByPath(path);
                if (!scene.IsValid())
                {
                    scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                }
                if (scene.IsValid())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.SetActiveScene(scene);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to restore scene: {ex.Message}");
            }
        }

        private static string? ResolveGraphIdToPath(string graphId)
        {
            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path).Equals(graphId, StringComparison.OrdinalIgnoreCase))
                    return path;
            }
            return null;
        }

        private static VisualEffect? FindActiveVisualEffect()
        {
            // First try to find in the active scene
            var activeScene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid())
            {
                var vfxInScene = UnityEngine.Object
                    .FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                    .Where(v => v != null && v.gameObject.scene == activeScene && v.enabled && v.visualEffectAsset != null)
                    .FirstOrDefault();
                
                if (vfxInScene != null)
                    return vfxInScene;
            }

            // Fallback: check all loaded scenes
            for (int i = 0; i < UnityEditor.SceneManagement.EditorSceneManager.sceneCount; i++)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;

                var vfxInScene = UnityEngine.Object
                    .FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                    .Where(v => v != null && v.gameObject.scene == scene && v.enabled && v.visualEffectAsset != null)
                    .FirstOrDefault();
                
                if (vfxInScene != null)
                    return vfxInScene;
            }

            return null;
        }

        private static void CleanupSceneForCapture(UnityEngine.SceneManagement.Scene scene, VisualEffectAsset targetGraph)
        {
            // Disable all VisualEffect components except the one we'll use for capture
            var allVfx = UnityEngine.Object
                .FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                .Where(v => v != null && v.gameObject.scene == scene)
                .ToList();

            foreach (var vfx in allVfx)
            {
                // Keep the VFXInstance enabled, disable all others
                if (vfx.gameObject.name != VfxInstanceName)
                {
                    vfx.enabled = false;
                    Debug.Log($"[CaptureVfxMultiAngle] Disabled unrelated VFX: {vfx.gameObject.name}");
                }
            }

            // Disable ParticleSystem components that might interfere (like bonfire)
            var particleSystems = UnityEngine.Object
                .FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Where(ps => ps != null && ps.gameObject.scene == scene)
                .ToList();

            foreach (var ps in particleSystems)
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = ps.main;
                main.playOnAwake = false;
                Debug.Log($"[CaptureVfxMultiAngle] Disabled ParticleSystem: {ps.gameObject.name}");
            }

            // Disable additional lights that aren't part of the capture setup
            var allLights = UnityEngine.Object
                .FindObjectsByType<Light>(FindObjectsSortMode.None)
                .Where(l => l != null && l.gameObject.scene == scene)
                .ToList();

            // Keep directional lights (likely part of capture setup), disable point/spot lights
            foreach (var light in allLights)
            {
                if (light.type != LightType.Directional)
                {
                    light.enabled = false;
                    Debug.Log($"[CaptureVfxMultiAngle] Disabled non-directional light: {light.gameObject.name}");
                }
            }
        }

        private class CameraCaptureInfo
        {
            public Camera Camera = null!;
            public RenderTexture RenderTexture = null!;
            public Texture2D ReadbackTexture = null!;
            public CameraClearFlags OriginalClearFlags;
            public Color OriginalBackground;
            public Color ConfiguredBackground; // The background color configured for capture
            public RenderTexture? OriginalRenderTexture;
            public Rect OriginalRect;
            public string OriginalTag = string.Empty;
            public bool OriginalForceIntoRenderTexture;
            public float OriginalAspect;
            public string Name = string.Empty;
        }

        private static Dictionary<string, object> ExtractParameters(VisualEffect visualEffect)
        {
            var paramsDict = new Dictionary<string, object>();
            var assetBindings = new Dictionary<string, string>(); // Track asset bindings separately
            if (visualEffect == null || visualEffect.visualEffectAsset == null)
                return paramsDict;

            // Get exposed parameters from the graph descriptor
            try
            {
                var graphPath = AssetDatabase.GetAssetPath(visualEffect.visualEffectAsset);
                var descParams = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["lod"] = 1 // Just need parameter list
                };
                var descResponse = DescribeVfxGraphTool.HandleCommand(descParams);
                
                if (descResponse is JObject responseObj && responseObj["success"]?.ToObject<bool>() == true)
                {
                    var data = responseObj["data"] as JObject;
                    if (data == null)
                        return paramsDict;
                        
                    var exposedParams = data["exposed_params"] as JArray;
                    if (exposedParams != null)
                    {
                        foreach (var paramToken in exposedParams)
                        {
                            if (paramToken is JObject paramObj)
                            {
                                var name = paramObj["k"]?.ToString();
                                var type = paramObj["t"]?.ToString();
                                
                                if (string.IsNullOrEmpty(name))
                                    continue;

                                try
                                {
                                    bool valueExtracted = false;
                                    
                                    switch (type)
                                    {
                                        case "Float":
                                            if (visualEffect.HasFloat(name))
                                            {
                                                paramsDict[name] = visualEffect.GetFloat(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                // Use default value from descriptor
                                                paramsDict[name] = paramObj["def"].ToObject<float>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Int":
                                            if (visualEffect.HasInt(name))
                                            {
                                                paramsDict[name] = visualEffect.GetInt(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                paramsDict[name] = paramObj["def"].ToObject<int>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Vector2":
                                            if (visualEffect.HasVector2(name))
                                            {
                                                var v2 = visualEffect.GetVector2(name);
                                                paramsDict[name] = new[] { v2.x, v2.y };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 2)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Vector3":
                                            if (visualEffect.HasVector3(name))
                                            {
                                                var v3 = visualEffect.GetVector3(name);
                                                paramsDict[name] = new[] { v3.x, v3.y, v3.z };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 3)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>(), defArray[2].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Vector4":
                                        case "Color":
                                            if (visualEffect.HasVector4(name))
                                            {
                                                var v4 = visualEffect.GetVector4(name);
                                                paramsDict[name] = new[] { v4.x, v4.y, v4.z, v4.w };
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                var defArray = paramObj["def"] as JArray;
                                                if (defArray != null && defArray.Count >= 4)
                                                {
                                                    paramsDict[name] = new[] { defArray[0].ToObject<float>(), defArray[1].ToObject<float>(), defArray[2].ToObject<float>(), defArray[3].ToObject<float>() };
                                                    valueExtracted = true;
                                                }
                                            }
                                            break;
                                        case "Bool":
                                            if (visualEffect.HasBool(name))
                                            {
                                                paramsDict[name] = visualEffect.GetBool(name);
                                                valueExtracted = true;
                                            }
                                            else if (paramObj["def"] != null)
                                            {
                                                paramsDict[name] = paramObj["def"].ToObject<bool>();
                                                valueExtracted = true;
                                            }
                                            break;
                                        case "Texture2D":
                                        case "Texture":
                                            if (visualEffect.HasTexture(name))
                                            {
                                                var tex = visualEffect.GetTexture(name);
                                                if (tex != null)
                                                {
                                                    var texPath = AssetDatabase.GetAssetPath(tex);
                                                    if (!string.IsNullOrEmpty(texPath))
                                                    {
                                                        paramsDict[name] = texPath;
                                                        assetBindings[$"texture_{name}"] = texPath;
                                                        valueExtracted = true;
                                                    }
                                                }
                                            }
                                            break;
                                        case "Mesh":
                                            if (visualEffect.HasMesh(name))
                                            {
                                                var mesh = visualEffect.GetMesh(name);
                                                if (mesh != null)
                                                {
                                                    var meshPath = AssetDatabase.GetAssetPath(mesh);
                                                    if (!string.IsNullOrEmpty(meshPath))
                                                    {
                                                        paramsDict[name] = meshPath;
                                                        assetBindings[$"mesh_{name}"] = meshPath;
                                                        valueExtracted = true;
                                                    }
                                                }
                                            }
                                            break;
                                    }
                                    
                                    if (!valueExtracted)
                                    {
                                        Debug.LogWarning($"[CaptureVfxMultiAngle] Could not extract parameter '{name}' (type: {type}) - no value set and no default found");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to extract parameter '{name}': {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to get parameter list: {ex.Message}");
            }

            // Add asset bindings as a separate dictionary entry for easy access
            if (assetBindings.Count > 0)
            {
                paramsDict["_asset_bindings"] = assetBindings;
            }

            return paramsDict;
        }

        private static object? GetGraphDescriptor(VisualEffectAsset graphAsset, string graphPath)
        {
            try
            {
                var descParams = new JObject
                {
                    ["graph_path"] = graphPath,
                    ["lod"] = 2 // Full detail including structure
                };
                return DescribeVfxGraphTool.HandleCommand(descParams);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureVfxMultiAngle] Failed to get graph descriptor: {ex.Message}");
                return null;
            }
        }

        private static void SaveParametersAndDescriptor(string outputDir, string graphName, Dictionary<string, object> parameters, object? descriptor, Dictionary<string, object> assetBindings)
        {
            try
            {
                Directory.CreateDirectory(outputDir);

                // Save parameters as JSON
                var paramsJson = JsonConvert.SerializeObject(parameters, Formatting.Indented);
                var paramsPath = Path.Combine(outputDir, $"{graphName}_params.json");
                File.WriteAllText(paramsPath, paramsJson);
                Debug.Log($"[CaptureVfxMultiAngle] Saved parameters to {paramsPath}");

                // Save asset bindings as JSON (textures, meshes, materials)
                if (assetBindings.Count > 0)
                {
                    var assetsJson = JsonConvert.SerializeObject(assetBindings, Formatting.Indented);
                    var assetsPath = Path.Combine(outputDir, $"{graphName}_assets.json");
                    File.WriteAllText(assetsPath, assetsJson);
                    Debug.Log($"[CaptureVfxMultiAngle] Saved {assetBindings.Count} asset bindings to {assetsPath}");
                }

                // Save descriptor as JSON
                if (descriptor != null)
                {
                    string descriptorJson;
                    if (descriptor is JObject responseObj)
                    {
                        // Check if it's a Response object with success/data structure
                        if (responseObj["success"]?.ToObject<bool>() == true && responseObj["data"] != null)
                        {
                            var data = responseObj["data"];
                            if (data is JObject dataObj)
                            {
                                descriptorJson = dataObj.ToString(Formatting.Indented);
                            }
                            else
                            {
                                descriptorJson = data?.ToString(Formatting.Indented) ?? JsonConvert.SerializeObject(data, Formatting.Indented);
                            }
                        }
                        else
                        {
                            // It's a plain JObject, serialize it directly
                            descriptorJson = responseObj.ToString(Formatting.Indented);
                        }
                    }
                    else
                    {
                        descriptorJson = JsonConvert.SerializeObject(descriptor, Formatting.Indented);
                    }

                    var descPath = Path.Combine(outputDir, $"{graphName}_descriptor.json");
                    File.WriteAllText(descPath, descriptorJson);
                    Debug.Log($"[CaptureVfxMultiAngle] Saved graph descriptor to {descPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureVfxMultiAngle] Failed to save parameters/descriptor: {ex.Message}");
            }
        }

        #endregion
    }
}

