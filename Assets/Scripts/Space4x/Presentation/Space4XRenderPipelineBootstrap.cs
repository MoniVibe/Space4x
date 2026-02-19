using Space4X.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityDebug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Space4X.Presentation
{
    internal static class Space4XRenderPipelineBootstrap
    {
        private const string GamePipelineResource = "Rendering/Space4XURP";
        private const string LegacyPipelineResource = "Rendering/ScenarioURP";
        private const string HeadlessEnvVar = "PUREDOTS_HEADLESS";
        private const string NoGraphicsEnvVar = "PUREDOTS_NOGRAPHICS";
        private const string ForceRenderEnvVar = "PUREDOTS_FORCE_RENDER";
        private const string LegacyRenderingEnvVar = "PUREDOTS_RENDERING";
        private static bool s_loggedBootstrapAttempt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EnsureAfterAssembliesLoaded()
        {
            EnsurePipeline("AfterAssembliesLoaded");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureBeforeSceneLoad()
        {
            EnsurePipeline("BeforeSceneLoad");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void EnsureOnLoad()
        {
            EnsurePipeline("BeforeSplashScreen");
        }

#if UNITY_EDITOR
        [InitializeOnLoadMethod]
        private static void EnsureOnEditorLoad()
        {
            EnsurePipeline("EditorLoad");
        }
#endif

        private static void EnsurePipeline(string source)
        {
            if (!s_loggedBootstrapAttempt)
            {
                s_loggedBootstrapAttempt = true;
                UnityDebug.Log($"[Space4XRenderPipelineBootstrap] EnsurePipeline source={source} batch={Application.isBatchMode} current={GraphicsSettings.currentRenderPipeline} default={GraphicsSettings.defaultRenderPipeline} quality={QualitySettings.renderPipeline}");
            }

            TryEnsureRenderPipeline(legacyOnly: false);
        }

        internal static bool TryEnsureRenderPipeline(bool legacyOnly)
        {
            if (legacyOnly && !Space4XLegacyScenarioGate.IsEnabled)
            {
                return false;
            }

            if (ShouldSkipForHeadless())
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
            // Re-apply the active quality level to force pipeline settings to refresh before DOTS world bootstrap.
            QualitySettings.SetQualityLevel(QualitySettings.GetQualityLevel(), true);
            UnityDebug.Log($"[Space4XRenderPipelineBootstrap] Render pipeline set to {asset.name}.");
            return true;
        }

        private static bool ShouldSkipForHeadless()
        {
            if (EnvIsSet(ForceRenderEnvVar) || EnvIsSet(LegacyRenderingEnvVar))
            {
                return false;
            }

            if (Application.isBatchMode)
            {
                return true;
            }

            return EnvIsSet(HeadlessEnvVar) || EnvIsSet(NoGraphicsEnvVar);
        }

        private static bool EnvIsSet(string key)
        {
            var value = System.Environment.GetEnvironmentVariable(key);
            return string.Equals(value, "1", System.StringComparison.OrdinalIgnoreCase)
                   || string.Equals(value, "true", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
