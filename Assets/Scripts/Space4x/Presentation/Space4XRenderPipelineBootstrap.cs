#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Core;
using Space4X.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityDebug = UnityEngine.Debug;

namespace Space4X.Presentation
{
    internal static class Space4XRenderPipelineBootstrap
    {
        private const string GamePipelineResource = "Rendering/Space4XURP";
        private const string LegacyPipelineResource = "Rendering/ScenarioURP";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void EnsureOnLoad()
        {
            TryEnsureRenderPipeline(legacyOnly: false);
        }

        internal static bool TryEnsureRenderPipeline(bool legacyOnly)
        {
            if (legacyOnly && !Space4XLegacyScenarioGate.IsEnabled)
            {
                return false;
            }

            if (!RuntimeMode.IsRenderingEnabled)
            {
                return false;
            }

            if (GraphicsSettings.currentRenderPipeline != null)
            {
                return false;
            }

            var asset = Resources.Load<UniversalRenderPipelineAsset>(GamePipelineResource);
            if (asset == null)
            {
                asset = Resources.Load<UniversalRenderPipelineAsset>(LegacyPipelineResource);
            }

            if (asset == null)
            {
                UnityDebug.LogWarning("[Space4XRenderPipelineBootstrap] Render pipeline asset not found. Configure GraphicsSettings/QualitySettings for the game.");
                return false;
            }

            QualitySettings.renderPipeline = asset;
            GraphicsSettings.defaultRenderPipeline = asset;
            UnityDebug.Log($"[Space4XRenderPipelineBootstrap] Render pipeline set to {asset.name}.");
            return true;
        }
    }
}
#endif
