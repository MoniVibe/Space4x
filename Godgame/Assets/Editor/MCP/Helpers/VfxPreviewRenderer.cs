using UnityEngine;
using UnityEditor;
using UnityEngine.VFX;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace PureDOTS.Editor.MCP.Helpers
{
    public static class VfxPreviewRenderer
    {
        private static GameObject previewCamera;
        private static GameObject previewVfxObject;
        private static RenderTexture previewRenderTexture;
        private static SceneAsset previewScene;

        public static string RenderPreview(
            string graphPath,
            Dictionary<string, object> parameters,
            int width = 320,
            int height = 320,
            float durationSeconds = 0.5f,
            int fps = 16,
            int frameCount = -1)
        {
            try
            {
                var graphAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                {
                    throw new Exception($"VFX graph not found at path: {graphPath}");
                }

                // Calculate frame count
                if (frameCount < 0)
                {
                    frameCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * fps));
                }

                // Create unique output directory per graph and run
                var graphId = System.IO.Path.GetFileNameWithoutExtension(graphPath);
                var runId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var outputDir = Path.Combine("Temp", "VFXPreviews", graphId, runId);
                Directory.CreateDirectory(outputDir);
                
                // Get absolute path for return value
                var absoluteOutputDir = Path.GetFullPath(outputDir);

                // Setup preview scene
                SetupPreviewScene();

                // Create VFX GameObject
                if (previewVfxObject == null)
                {
                    previewVfxObject = new GameObject("PreviewVFX");
                    previewVfxObject.hideFlags = HideFlags.HideAndDontSave;
                }

                var vfx = previewVfxObject.GetComponent<VisualEffect>();
                if (vfx == null)
                {
                    vfx = previewVfxObject.AddComponent<VisualEffect>();
                }

                vfx.visualEffectAsset = graphAsset;

                // Apply parameters
                ApplyParameters(vfx, parameters);

                // Setup camera
                SetupPreviewCamera(width, height);

                // Create render texture
                if (previewRenderTexture != null)
                {
                    previewRenderTexture.Release();
                }
                previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                previewRenderTexture.Create();
                previewCamera.GetComponent<Camera>().targetTexture = previewRenderTexture;

                // Play the effect
                vfx.Play();

                // Render frames
                var framePaths = new List<string>();
                var frameTime = 1.0f / fps;

                for (int i = 0; i < frameCount; i++)
                {
                    // Advance time
                    float time = i * frameTime;
                    EditorApplication.update();
                    System.Threading.Thread.Sleep(Mathf.RoundToInt(frameTime * 1000));

                    // Render frame
                    previewCamera.GetComponent<Camera>().Render();

                    // Save frame
                    var frameFileName = $"frame_{i:D4}.png";
                    var framePath = Path.Combine(outputDir, frameFileName);
                    SaveRenderTexture(previewRenderTexture, framePath);
                    
                    // Store absolute path
                    var absoluteFramePath = Path.Combine(absoluteOutputDir, frameFileName);
                    framePaths.Add(absoluteFramePath);
                }

                // Stop effect
                vfx.Stop();

                // Return absolute path to first frame
                return framePaths.Count > 0 ? framePaths[0] : null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VfxPreviewRenderer] Failed to render preview: {ex.Message}");
                CleanupPreviewScene();
                throw;
            }
        }

        public static List<string> RenderPreviewFrames(
            string graphPath,
            Dictionary<string, object> parameters,
            int width = 320,
            int height = 320,
            float durationSeconds = 0.5f,
            int fps = 16,
            int frameCount = -1)
        {
            try
            {
                var graphAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(graphPath);
                if (graphAsset == null)
                {
                    throw new Exception($"VFX graph not found at path: {graphPath}");
                }

                if (frameCount < 0)
                {
                    frameCount = Mathf.Max(1, Mathf.RoundToInt(durationSeconds * fps));
                }

                // Create unique output directory per graph and run
                var graphId = System.IO.Path.GetFileNameWithoutExtension(graphPath);
                var runId = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var outputDir = Path.Combine("Temp", "VFXPreviews", graphId, runId);
                Directory.CreateDirectory(outputDir);
                
                // Get absolute path for return value
                var absoluteOutputDir = Path.GetFullPath(outputDir);

                SetupPreviewScene();

                if (previewVfxObject == null)
                {
                    previewVfxObject = new GameObject("PreviewVFX");
                    previewVfxObject.hideFlags = HideFlags.HideAndDontSave;
                }

                var vfx = previewVfxObject.GetComponent<VisualEffect>();
                if (vfx == null)
                {
                    vfx = previewVfxObject.AddComponent<VisualEffect>();
                }

                vfx.visualEffectAsset = graphAsset;
                ApplyParameters(vfx, parameters);

                SetupPreviewCamera(width, height);

                if (previewRenderTexture != null)
                {
                    previewRenderTexture.Release();
                }
                previewRenderTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
                previewRenderTexture.Create();
                previewCamera.GetComponent<Camera>().targetTexture = previewRenderTexture;

                vfx.Play();

                var framePaths = new List<string>();
                var frameTime = 1.0f / fps;

                for (int i = 0; i < frameCount; i++)
                {
                    float time = i * frameTime;
                    EditorApplication.update();
                    System.Threading.Thread.Sleep(Mathf.RoundToInt(frameTime * 1000));

                    previewCamera.GetComponent<Camera>().Render();

                    var frameFileName = $"frame_{i:D4}.png";
                    var framePath = Path.Combine(outputDir, frameFileName);
                    SaveRenderTexture(previewRenderTexture, framePath);
                    
                    // Store absolute path
                    var absoluteFramePath = Path.Combine(absoluteOutputDir, frameFileName);
                    framePaths.Add(absoluteFramePath);
                }

                vfx.Stop();

                return framePaths;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[VfxPreviewRenderer] Failed to render preview frames: {ex.Message}");
                CleanupPreviewScene();
                throw;
            }
        }

        private static void SetupPreviewScene()
        {
            // Use a hidden scene or the current scene
            // For simplicity, we'll use the current scene but hide objects
        }

        private static void SetupPreviewCamera(int width, int height)
        {
            if (previewCamera == null)
            {
                previewCamera = new GameObject("PreviewCamera");
                previewCamera.hideFlags = HideFlags.HideAndDontSave;
                var camera = previewCamera.AddComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Color.black;
                camera.orthographic = false;
                camera.fieldOfView = 60f;
                camera.nearClipPlane = 0.1f;
                camera.farClipPlane = 100f;
                previewCamera.transform.position = new Vector3(0, 0, -5);
                previewCamera.transform.LookAt(Vector3.zero);
            }
        }

        private static void ApplyParameters(VisualEffect vfx, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            foreach (var kvp in parameters)
            {
                try
                {
                    var paramName = kvp.Key;
                    var value = kvp.Value;

                    if (value is float f)
                    {
                        vfx.SetFloat(paramName, f);
                    }
                    else if (value is int i)
                    {
                        vfx.SetInt(paramName, i);
                    }
                    else if (value is bool b)
                    {
                        vfx.SetBool(paramName, b);
                    }
                    else if (value is Vector2 v2)
                    {
                        vfx.SetVector2(paramName, v2);
                    }
                    else if (value is Vector3 v3)
                    {
                        vfx.SetVector3(paramName, v3);
                    }
                    else if (value is Vector4 v4)
                    {
                        vfx.SetVector4(paramName, v4);
                    }
                    else if (value is Color color)
                    {
                        vfx.SetVector4(paramName, new Vector4(color.r, color.g, color.b, color.a));
                    }
                    else if (value is List<object> list && list.Count == 4)
                    {
                        // Assume RGBA color
                        var r = Convert.ToSingle(list[0]);
                        var g = Convert.ToSingle(list[1]);
                        var blue = Convert.ToSingle(list[2]);
                        var a = Convert.ToSingle(list[3]);
                        vfx.SetVector4(paramName, new Vector4(r, g, blue, a));
                    }
                    else if (value is Texture2D tex)
                    {
                        vfx.SetTexture(paramName, tex);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[VfxPreviewRenderer] Failed to set parameter '{kvp.Key}': {ex.Message}");
                }
            }
        }

        private static void SaveRenderTexture(RenderTexture rt, string path)
        {
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();

            var bytes = tex.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            UnityEngine.Object.DestroyImmediate(tex);
            RenderTexture.active = null;
        }

        public static void CleanupPreviewScene()
        {
            if (previewCamera != null)
            {
                UnityEngine.Object.DestroyImmediate(previewCamera);
                previewCamera = null;
            }

            if (previewVfxObject != null)
            {
                UnityEngine.Object.DestroyImmediate(previewVfxObject);
                previewVfxObject = null;
            }

            if (previewRenderTexture != null)
            {
                previewRenderTexture.Release();
                UnityEngine.Object.DestroyImmediate(previewRenderTexture);
                previewRenderTexture = null;
            }
        }
    }
}

