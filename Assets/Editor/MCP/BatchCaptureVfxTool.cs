using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using PureDOTS.Editor.MCP;
using PureDOTS.Editor.MCP.Helpers;
using VFXPlayground.Capture;

namespace VFXPlayground.Editor.MCP
{
    /// <summary>
    /// Batch capture tool for automatically capturing all VFX graphs in the project.
    /// </summary>
    public static class BatchCaptureVfxTool
    {
        private const string CaptureRootRelative = "Data/CameraCaptures";

        private static bool _isBatchRunning;
        private static readonly List<VisualEffectAsset> _batchGraphs = new();
        private static int _currentIndex;
        private static int _successCount;
        private static int _skipCount;
        private static int _failCount;
        private static bool _waitingForCapture;
        private static string _currentGraphName = string.Empty;
        private static string _currentGraphPath = string.Empty;
        private static string _currentFolderName = string.Empty;

        [MenuItem("VFX/Batch Capture All VFX")]
        public static void BatchCaptureAllVfx()
        {
            if (_isBatchRunning)
            {
                Debug.LogWarning("[BatchCaptureVfx] A batch capture is already running.");
                return;
            }

            // Find all VFX graphs in the project
            var vfxGraphs = FindAllVfxGraphs();
            
            if (vfxGraphs.Count == 0)
            {
                Debug.LogWarning("[BatchCaptureVfx] No VFX graphs found in the project.");
                EditorUtility.DisplayDialog("No VFX Found", "No VFX graphs found in the project.", "OK");
                return;
            }
            
            Debug.Log($"[BatchCaptureVfx] Found {vfxGraphs.Count} VFX graphs to capture:");
            foreach (var graph in vfxGraphs)
            {
                Debug.Log($"  - {graph.name} ({AssetDatabase.GetAssetPath(graph)})");
            }
            
            // Confirm with user
            bool proceed = EditorUtility.DisplayDialog(
                "Batch Capture VFX",
                $"Found {vfxGraphs.Count} VFX graphs. This will capture each one.\n\nProceed?",
                "Yes",
                "Cancel"
            );
            
            if (!proceed)
            {
                return;
            }

            _batchGraphs.Clear();
            _batchGraphs.AddRange(vfxGraphs);
            _currentIndex = -1;
            _successCount = _skipCount = _failCount = 0;
            _waitingForCapture = false;
            _currentGraphName = string.Empty;
            _isBatchRunning = true;

            EditorApplication.update += BatchCaptureUpdate;

            Debug.Log($"[BatchCaptureVfx] Starting batch capture of {_batchGraphs.Count} graphs.");
            AdvanceToNextGraph();
        }
        
        private static List<VisualEffectAsset> FindAllVfxGraphs()
        {
            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            var graphs = new List<VisualEffectAsset>();
            
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                if (asset != null)
                {
                    graphs.Add(asset);
                }
            }
            
            return graphs.OrderBy(g => g.name).ToList();
        }

        private static void BatchCaptureUpdate()
        {
            if (!_isBatchRunning || !_waitingForCapture)
                return;

            if (VFXCaptureUtility.CaptureInProgress)
            {
                SceneView.RepaintAll();
                EditorApplication.QueuePlayerLoopUpdate();
                return;
            }

            var captureDir = VFXCaptureUtility.LastCaptureDirectory;
            bool success = !string.IsNullOrEmpty(captureDir)
                           && Directory.Exists(captureDir)
                           && Directory.GetFiles(captureDir, "*.png", SearchOption.AllDirectories).Length > 0;

            if (success)
            {
                Debug.Log($"[BatchCaptureVfx] ✓ Captured {_currentGraphName} to {captureDir}");
                _successCount++;
            }
            else
            {
                Debug.LogWarning($"[BatchCaptureVfx] ✗ Capture produced no frames for {_currentGraphName}");
                _failCount++;
            }

            _waitingForCapture = false;
            AdvanceToNextGraph();
        }

        private static void AdvanceToNextGraph()
        {
            while (true)
            {
                _currentIndex++;

                if (_currentIndex >= _batchGraphs.Count)
                {
                    FinishBatch();
                    return;
                }

                var graph = _batchGraphs[_currentIndex];
                _currentGraphName = graph.name;
                _currentGraphPath = AssetDatabase.GetAssetPath(graph);
                _currentFolderName = VFXCaptureUtility.SanitizeFolderName(_currentGraphName);

                Debug.Log($"[BatchCaptureVfx] [{_currentIndex + 1}/{_batchGraphs.Count}] Capturing {_currentGraphName}...");

                string captureRootAbsolute = GetAbsoluteCaptureRoot();
                Directory.CreateDirectory(captureRootAbsolute);
                string graphOutputDir = Path.Combine(captureRootAbsolute, _currentFolderName);
                string descriptorPath = Path.Combine(graphOutputDir, $"{_currentGraphName}_descriptor.json");

                if (File.Exists(descriptorPath))
                {
                    Debug.Log($"[BatchCaptureVfx] Skipping {_currentGraphName} - already captured (found descriptor)");
                    _skipCount++;
                    continue;
                }

                // Ensure capture scene is loaded and active before each capture
                if (!EnsureCaptureSceneActive())
                {
                    Debug.LogError($"[BatchCaptureVfx] Failed to load capture scene for {_currentGraphName}");
                    _failCount++;
                    continue;
                }

                if (!LoadVfxIntoScene(graph))
                {
                    Debug.LogError($"[BatchCaptureVfx] Failed to load {_currentGraphName} into scene");
                    _failCount++;
                    continue;
                }

                var validationResult = ValidateVfx(_currentGraphName);
                if (validationResult != VfxValidationResult.Valid)
                {
                    string reason = validationResult == VfxValidationResult.PurpleArtifacts
                        ? "detected purple artifacts (missing textures/materials)"
                        : "VFX not rendering (no visible output)";
                    Debug.LogWarning($"[BatchCaptureVfx] ⚠ Skipping {_currentGraphName} - {reason}");
                    _skipCount++;
                    continue;
                }

                bool started = VFXCaptureUtility.BeginCapture(captureRootAbsolute, _currentFolderName);
                if (!started)
                {
                    Debug.LogWarning($"[BatchCaptureVfx] ✗ Failed to start capture for {_currentGraphName}");
                    _failCount++;
                    continue;
                }

                _waitingForCapture = true;
                return;
            }
        }

        private static void FinishBatch()
        {
            EditorApplication.update -= BatchCaptureUpdate;
            _isBatchRunning = false;
            _batchGraphs.Clear();

            Debug.Log("[BatchCaptureVfx] Batch capture complete!");
            Debug.Log($"  Success: {_successCount}");
            Debug.Log($"  Skipped : {_skipCount}");
            Debug.Log($"  Failed  : {_failCount}");

            EditorUtility.DisplayDialog(
                "Batch Capture Complete",
                $"Captured {_successCount} VFX graphs\nSkipped {_skipCount} (already exist or invalid)\nFailed {_failCount}",
                "OK"
            );
        }

        private static string GetAbsoluteCaptureRoot()
        {
            var absolute = Path.Combine(Application.dataPath, "..", CaptureRootRelative);
            return Path.GetFullPath(absolute);
        }

        private static bool LoadVfxIntoScene(VisualEffectAsset graphAsset)
        {
            // Find or create VFX instance in the scene
            var existingVfx = Object.FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.name == "VFXInstance");
            
            VisualEffect vfxComponent;
            
            if (existingVfx != null)
            {
                vfxComponent = existingVfx;
            }
            else
            {
                // Create new GameObject with VisualEffect component
                var go = new GameObject("VFXInstance");
                vfxComponent = go.AddComponent<VisualEffect>();
            }
            
            // Assign the graph asset
            vfxComponent.visualEffectAsset = graphAsset;
            vfxComponent.enabled = true;
            
            // Ensure it's at the origin
            vfxComponent.transform.position = Vector3.zero;
            vfxComponent.transform.rotation = Quaternion.identity;
            vfxComponent.transform.localScale = Vector3.one;
            
            // Mark scene as dirty and save to ensure VFX is properly loaded
            EditorUtility.SetDirty(vfxComponent.gameObject);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            
            // Force asset database refresh to ensure VFX asset is loaded
            AssetDatabase.Refresh();
            
            return true;
        }
        
        private enum VfxValidationResult
        {
            Valid,
            PurpleArtifacts,
            NotRendering
        }
        
        private static VfxValidationResult ValidateVfx(string graphName)
        {
            // Check for missing textures/materials in the VFX graph
            var vfxInstance = Object.FindObjectsByType<VisualEffect>(FindObjectsSortMode.None)
                .FirstOrDefault(v => v.gameObject.name == "VFXInstance" && v.visualEffectAsset != null);
            
            if (vfxInstance == null || vfxInstance.visualEffectAsset == null)
                return VfxValidationResult.NotRendering;
            
            // Check if VFX is enabled and playing
            if (!vfxInstance.enabled)
            {
                Debug.LogWarning($"[BatchCaptureVfx] {graphName} is disabled");
                return VfxValidationResult.NotRendering;
            }
            
            // Check for renderers with missing materials (purple = missing texture)
            var renderers = vfxInstance.GetComponentsInChildren<Renderer>();
            bool hasRenderers = renderers != null && renderers.Length > 0;
            
            foreach (var renderer in renderers ?? System.Array.Empty<Renderer>())
            {
                if (renderer == null) continue;
                
                // Check if any material is null or uses the error shader (purple)
                var materials = renderer.sharedMaterials;
                foreach (var mat in materials)
                {
                    if (mat == null)
                    {
                        Debug.LogWarning($"[BatchCaptureVfx] {graphName} has null material on {renderer.gameObject.name}");
                        return VfxValidationResult.PurpleArtifacts;
                    }
                    
                    // Unity's error shader shows purple - check if shader name contains "Error"
                    if (mat.shader != null && mat.shader.name.Contains("Error"))
                    {
                        Debug.LogWarning($"[BatchCaptureVfx] {graphName} uses error shader '{mat.shader.name}' on {renderer.gameObject.name}");
                        return VfxValidationResult.PurpleArtifacts;
                    }
                }
            }
            
            // If VFX has no renderers at all, it might not be rendering
            // This is a heuristic - some VFX might render without traditional renderers
            // But if it has a VisualEffect component and is enabled, assume it's valid
            // The actual rendering check would require capturing a frame and analyzing pixels
            
            return VfxValidationResult.Valid;
        }

        private static bool EnsureCaptureSceneActive()
        {
            const string CaptureScenePath = "Assets/Scenes/VFXCaptureScene.unity";
            
            // Check if capture scene is already active
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.path == CaptureScenePath)
            {
                // Verify cameras exist
                var cameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.gameObject.scene == activeScene && 
                                c.gameObject.name.StartsWith("CaptureCamera", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (cameras.Count > 0)
                {
                    EnsureCamerasEnabled(cameras);
                    Debug.Log($"[BatchCaptureVfx] Capture scene already active with {cameras.Count} cameras");
                    return true;
                }
            }

            // Load the capture scene
            if (!File.Exists(CaptureScenePath))
            {
                Debug.LogWarning($"[BatchCaptureVfx] Capture scene not found at {CaptureScenePath}, setting it up...");
                CaptureVfxMultiAngleTool.SetupCaptureSceneMenuItem();
            }

            var scene = EditorSceneManager.OpenScene(CaptureScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError($"[BatchCaptureVfx] Failed to open capture scene at {CaptureScenePath}");
                return false;
            }

            EditorSceneManager.SetActiveScene(scene);
            
            // Force immediate update to ensure scene is fully loaded
            EditorApplication.QueuePlayerLoopUpdate();
            
            // Verify cameras exist
            var captureCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                .Where(c => c != null && c.gameObject.scene == scene && 
                            c.gameObject.name.StartsWith("CaptureCamera", System.StringComparison.OrdinalIgnoreCase))
                .ToList();
            
            if (captureCameras.Count == 0)
            {
                Debug.LogWarning($"[BatchCaptureVfx] No capture cameras found in scene. Setting up scene...");
                CaptureVfxMultiAngleTool.SetupCaptureSceneMenuItem();
                
                // Re-check after setup
                captureCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)
                    .Where(c => c != null && c.gameObject.scene == scene && 
                                c.gameObject.name.StartsWith("CaptureCamera", System.StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                if (captureCameras.Count == 0)
                {
                    Debug.LogError("[BatchCaptureVfx] Still no capture cameras found after setup");
                    return false;
                }
            }

            EnsureCamerasEnabled(captureCameras);
            Debug.Log($"[BatchCaptureVfx] Capture scene loaded with {captureCameras.Count} cameras");
            return true;
        }

        private static void EnsureCamerasEnabled(List<Camera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                var cam = cameras[i];
                if (cam == null) continue;

                if (!cam.gameObject.activeSelf)
                {
                    cam.gameObject.SetActive(true);
                }

                cam.enabled = true;
                cam.targetDisplay = 0;

                if (i == 0 && !cam.CompareTag("MainCamera"))
                {
                    cam.tag = "MainCamera";
                }
            }
        }
    }
}

