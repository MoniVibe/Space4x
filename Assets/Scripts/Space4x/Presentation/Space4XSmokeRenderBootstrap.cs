#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UCamera = UnityEngine.Camera;
using UObject = UnityEngine.Object;

namespace Space4X.Presentation
{
    [DisallowMultipleComponent]
    internal sealed class Space4XSmokeRenderBootstrap : MonoBehaviour
    {
        private const string SmokeSceneName = "TRI_Space4X_Smoke";
        private const string GamePipelineResource = "Rendering/Space4XURP";
        private const string LegacyPipelineResource = "Rendering/ScenarioURP";

        private void Awake()
        {
            var scene = gameObject.scene;
            if (!scene.IsValid() || !string.Equals(scene.name, SmokeSceneName, StringComparison.Ordinal))
            {
                return;
            }

            EnsurePipeline();
            EnsureCamera();
        }

        private static void EnsurePipeline()
        {
            var asset = Resources.Load<UniversalRenderPipelineAsset>(GamePipelineResource);
            if (asset == null)
            {
                asset = Resources.Load<UniversalRenderPipelineAsset>(LegacyPipelineResource);
            }

            if (asset == null)
            {
                Debug.LogWarning("[Space4XSmokeRenderBootstrap] URP asset not found.");
                return;
            }

            if (GraphicsSettings.defaultRenderPipeline != asset)
            {
                GraphicsSettings.defaultRenderPipeline = asset;
            }

            if (QualitySettings.renderPipeline != asset)
            {
                QualitySettings.renderPipeline = asset;
            }
        }

        private static void EnsureCamera()
        {
            var camera = UCamera.main ?? UObject.FindObjectOfType<UCamera>();
            if (camera == null)
            {
                return;
            }

            if (camera.usePhysicalProperties)
            {
                camera.usePhysicalProperties = false;
            }

            camera.allowHDR = true;
        }
    }
}
#endif
