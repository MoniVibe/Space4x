using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using PureDOTS.Editor.MCP.Helpers;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VFX;

namespace PureDOTS.Editor.MCP
{
    [McpForUnityTool("capture_vfx_multi_angle")]
    public static class CaptureVfxMultiAngleTool
    {
        private const string CaptureScenePath = "Assets/Scenes/VFXCaptureScene.unity";
        private const string VfxInstanceName = "VFXInstance";

        private static readonly Color DefaultBackground = new(0.5f, 0.5f, 0.5f, 1f);
        private static readonly string[] DefaultCameraOrder = { "front", "left", "right", "back", "top" };

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
                float duration = Mathf.Max(0.1f, @params["duration"]?.ToObject<float?>() ?? 0.8f);
                float preRollSeconds = Mathf.Max(0f, @params["pre_roll"]?.ToObject<float?>() ?? 0.2f);
                var frameTimes = ParseFrameTimes(@params["frame_times"] as JArray, duration, @params["frame_count"]?.ToObject<int?>() ?? 0);
                var cameraNamesFilter = ParseCameraNames(@params["camera_names"]);
                var backgroundColor = ParseColor(@params["background_color"]) ?? DefaultBackground;
                var parameterToken = (@params["parameters"] ?? @params["params"]) as JObject;
                string outputDir = @params["output_dir"]?.ToString() ?? "Data/MCP_Exports/MultiAngle";

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

                var graphName = Path.GetFileNameWithoutExtension(graphPath) ?? "VFX";

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

                var vfxInstance = FindVfxInstance(captureScene);
                if (vfxInstance == null)
                    return Response.Error($"VFX instance '{VfxInstanceName}' not found in capture scene.");

                var visualEffect = vfxInstance.GetComponent<VisualEffect>();
                if (visualEffect == null)
                    return Response.Error($"VFX instance '{VfxInstanceName}' does not have a VisualEffect component.");

                visualEffect.visualEffectAsset = graphAsset;
                visualEffect.enabled = true;

                var cameras = UnityEngine.Object
                    .FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.gameObject.scene == captureScene)
                    .ToList();

                if (cameras.Count == 0)
                    return Response.Error("No cameras found in capture scene.");

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

                if (parameterToken != null)
                {
                    ApplyParameters(visualEffect, parameterToken);
                }

                var captureResults = CaptureFrames(cameraInfos, graphName, frameTimes, outputDir, visualEffect, preRollSeconds, wasPlaying);

                if (!wasPlaying && EditorApplication.isPlaying)
                {
                    ExitPlayModeWithWait();
                }

                RestoreActiveScene(activeScenePath);

                return Response.Success($"Captured {frameTimes.Count} frames from {cameraInfos.Count} cameras", new
                {
                    graphId = graphName,
                    cameras = cameraInfos.Select(c => c.Name).ToArray(),
                    frameCount = frameTimes.Count,
                    duration,
                    frameTimes = frameTimes.ToArray(),
                    preRoll = preRollSeconds,
                    captures = captureResults,
                    outputDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", outputDir))
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
            if (visualEffect != null)
            {
                visualEffect.enabled = true;
                visualEffect.Play();
            }

            SleepWithUpdates(preRollSeconds);

            float lastTimestamp = 0f;
            for (int frameIndex = 0; frameIndex < frameTimes.Count; frameIndex++)
            {
                float targetTime = Mathf.Max(0f, frameTimes[frameIndex]);
                float waitSeconds = Mathf.Max(0f, targetTime - lastTimestamp);
                if (waitSeconds > 0f)
                {
                    SleepWithUpdates(waitSeconds);
                }
                lastTimestamp = targetTime;

                foreach (var camera in cameras)
                {
                    var entry = CaptureFrame(camera, graphName, frameIndex, targetTime, absoluteOutputDir);
                    if (entry != null)
                    {
                        results[camera.Name].Add(entry);
                    }
                }
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

        private static object? CaptureFrame(CameraCaptureInfo info, string graphName, int frameIndex, float frameTime, string outputDir)
        {
            if (info.RenderTexture == null)
                return null;

            var previousActive = RenderTexture.active;
            RenderTexture.active = info.RenderTexture;

            try
            {
                info.ReadbackTexture.ReadPixels(new Rect(0, 0, info.RenderTexture.width, info.RenderTexture.height), 0, 0);
                info.ReadbackTexture.Apply(false);

                var fileName = $"{graphName}_{info.Name}_frame{frameIndex:D3}.png";
                var filePath = Path.Combine(outputDir, fileName);
                File.WriteAllBytes(filePath, info.ReadbackTexture.EncodeToPNG());

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

            float[] baseFractions = requestedCount switch
            {
                <= 0 or 3 => new[] { 0.25f, 0.5f, 0.75f },
                4 => new[] { 0.2f, 0.4f, 0.6f, 0.8f },
                5 => new[] { 0.2f, 0.35f, 0.5f, 0.65f, 0.8f },
                _ => Enumerable.Range(1, requestedCount).Select(i => (float)i / (requestedCount + 1)).ToArray()
            };

            return baseFractions
                .Select(f => Mathf.Clamp(f * duration, 0f, duration))
                .ToList();
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
            var infos = new List<CameraCaptureInfo>();
            foreach (var camera in cameras)
            {
                var renderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
                {
                    name = $"CaptureRT_{camera.gameObject.name}"
                };
                renderTexture.Create();

                var readback = new Texture2D(width, height, TextureFormat.RGBA32, false, false)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };

                var info = new CameraCaptureInfo
                {
                    Camera = camera,
                    RenderTexture = renderTexture,
                    ReadbackTexture = readback,
                    OriginalBackground = camera.backgroundColor,
                    OriginalClearFlags = camera.clearFlags,
                    OriginalRenderTexture = camera.targetTexture,
                    Name = NormalizeCameraName(camera.gameObject.name),
                };

                camera.targetTexture = renderTexture;
                camera.forceIntoRenderTexture = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;

                infos.Add(info);
            }

            return infos;
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
                    info.Camera.forceIntoRenderTexture = false;
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

            const float distance = 3f;
            foreach (var (label, direction) in directions)
            {
                var camera = new GameObject($"CaptureCamera_{label}").AddComponent<Camera>();
                camera.transform.SetParent(cameraRig.transform, false);
                camera.transform.position = direction.normalized * distance;
                camera.transform.LookAt(Vector3.zero, Vector3.up);
                camera.fieldOfView = 45f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
            }

            var vfxGO = new GameObject(VfxInstanceName);
            vfxGO.transform.position = Vector3.zero;
            vfxGO.AddComponent<VisualEffect>();

            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveScene(newScene, CaptureScenePath))
                return default;

            AssetDatabase.Refresh();
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

        private class CameraCaptureInfo
        {
            public Camera Camera = null!;
            public RenderTexture RenderTexture = null!;
            public Texture2D ReadbackTexture = null!;
            public CameraClearFlags OriginalClearFlags;
            public Color OriginalBackground;
            public RenderTexture? OriginalRenderTexture;
            public string Name = string.Empty;
        }

        #endregion
    }
}

