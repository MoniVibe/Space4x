#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Core;
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
        private static UniversalRenderPipelineAsset s_runtimeFallbackPipeline;
        private static UniversalRendererData s_runtimeFallbackRenderer;

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

            var asset = ResolvePipelineAsset(out var source);

            if (asset == null)
            {
                UnityDebug.LogWarning("[Space4XRenderPipelineBootstrap] Render pipeline asset not found. Configure GraphicsSettings/QualitySettings for the game.");
                return false;
            }

            QualitySettings.renderPipeline = asset;
            GraphicsSettings.defaultRenderPipeline = asset;
            UnityDebug.Log($"[Space4XRenderPipelineBootstrap] Render pipeline set to {asset.name} (source={source}).");
            return true;
        }

        private static UniversalRenderPipelineAsset ResolvePipelineAsset(out string source)
        {
            if (QualitySettings.renderPipeline is UniversalRenderPipelineAsset qualityAsset)
            {
                source = "quality";
                return qualityAsset;
            }

            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset defaultAsset)
            {
                source = "graphics";
                return defaultAsset;
            }

            var resourceAsset = Resources.Load<UniversalRenderPipelineAsset>(GamePipelineResource);
            if (resourceAsset == null)
            {
                resourceAsset = Resources.Load<UniversalRenderPipelineAsset>(LegacyPipelineResource);
            }

            if (resourceAsset != null)
            {
                source = "resources";
                return resourceAsset;
            }

#if UNITY_EDITOR
            var editorAsset = TryResolveEditorPipelineAsset();
            if (editorAsset != null)
            {
                source = "editor";
                return editorAsset;
            }
#endif

            var runtimeFallback = TryCreateRuntimeFallbackPipelineAsset();
            if (runtimeFallback != null)
            {
                source = "runtime";
                return runtimeFallback;
            }

            source = "none";
            return null;
        }

#if UNITY_EDITOR
        private static UniversalRenderPipelineAsset TryResolveEditorPipelineAsset()
        {
            var knownPaths = new[]
            {
                "Assets/Resources/Rendering/Space4XURP.asset",
                "Assets/Resources/Rendering/ScenarioURP.asset"
            };

            for (var i = 0; i < knownPaths.Length; i++)
            {
                var candidate = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(knownPaths[i]);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            var guids = AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var candidate = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (candidate != null)
                {
                    return candidate;
                }
            }

            return null;
        }
#endif

        private static UniversalRenderPipelineAsset TryCreateRuntimeFallbackPipelineAsset()
        {
            if (s_runtimeFallbackPipeline != null)
            {
                return s_runtimeFallbackPipeline;
            }

            try
            {
                s_runtimeFallbackRenderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                s_runtimeFallbackRenderer.name = "Space4XRuntimeURP_Renderer";
                s_runtimeFallbackRenderer.hideFlags = HideFlags.HideAndDontSave;

                s_runtimeFallbackPipeline = UniversalRenderPipelineAsset.Create(s_runtimeFallbackRenderer);
                if (s_runtimeFallbackPipeline != null)
                {
                    s_runtimeFallbackPipeline.name = "Space4XRuntimeURP";
                    s_runtimeFallbackPipeline.hideFlags = HideFlags.HideAndDontSave;
                }
            }
            catch
            {
                s_runtimeFallbackPipeline = null;
                s_runtimeFallbackRenderer = null;
            }

            return s_runtimeFallbackPipeline;
        }
    }
}
#endif
