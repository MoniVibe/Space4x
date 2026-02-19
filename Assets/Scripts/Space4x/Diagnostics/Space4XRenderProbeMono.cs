using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Space4x.Diagnostics
{
    [DefaultExecutionOrder(-10000)] // Run as early as possible
    public class Space4XRenderProbeMono : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const float SmokeAutoFocusDistance = 35f;
        private const float SmokeAutoFocusHeight = 18f;

        void Awake()
        {
            LogDiagnostics();
            EnsureSmokeSceneVisibility();
        }

        public static void LogDiagnostics()
        {
            Debug.Log("--- Space4X Render Probe Diagnostics ---");
            
            // GraphicsSettings
            // Note: 'defaultRenderPipeline' requested, mapping to 'renderPipelineAsset' which is the default SRP
            Debug.Log($"GraphicsSettings.currentRenderPipeline: {GraphicsSettings.currentRenderPipeline}");
            Debug.Log($"GraphicsSettings.renderPipelineAsset (default): {GraphicsSettings.defaultRenderPipeline}");
            
            // QualitySettings
            Debug.Log($"QualitySettings.renderPipeline: {QualitySettings.renderPipeline}");
            
            // SystemInfo
            Debug.Log($"SystemInfo.supportsComputeShaders: {SystemInfo.supportsComputeShaders}");
            Debug.Log($"SystemInfo.graphicsDeviceType: {SystemInfo.graphicsDeviceType}");
            
            // Application
            Debug.Log($"Application.isBatchMode: {Application.isBatchMode}");
            
            Debug.Log("----------------------------------------");
        }

        private static void EnsureSmokeSceneVisibility()
        {
            if (Application.isBatchMode)
                return;

            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
                return;

            EnsureDirectionalLight();
            EnsureAutoFocusCamera();
        }

        private static void EnsureDirectionalLight()
        {
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < lights.Length; i++)
            {
                var light = lights[i];
                if (light != null && light.enabled && light.type == LightType.Directional)
                    return;
            }

            var lightGo = new GameObject("Space4X Smoke Auto Light");
            var autoLight = lightGo.AddComponent<Light>();
            autoLight.type = LightType.Directional;
            autoLight.color = Color.white;
            autoLight.intensity = 1.35f;
            autoLight.shadows = LightShadows.Soft;
            autoLight.shadowStrength = 0.75f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            Debug.Log("[Space4XRenderProbe] Added fallback directional light for smoke scene visibility.");
        }

        private static void EnsureAutoFocusCamera()
        {
            var camera = Camera.main ?? UnityEngine.Object.FindFirstObjectByType<Camera>();
            if (camera == null)
            {
                Debug.LogWarning("[Space4XRenderProbe] Could not find a camera to attach autofocus.");
                return;
            }

            var focus = camera.GetComponent<global::FocusFirstRenderable>();
            if (focus == null)
            {
                focus = camera.gameObject.AddComponent<global::FocusFirstRenderable>();
            }

            focus.distance = SmokeAutoFocusDistance;
            focus.height = SmokeAutoFocusHeight;
        }
    }
}
