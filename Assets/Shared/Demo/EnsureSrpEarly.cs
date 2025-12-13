#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

static class EnsureSrpEarly
{
    // Runs before any scene/world bootstraps
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void Ensure()
    {
        if (GraphicsSettings.currentRenderPipeline != null)
        {
            if (ShouldLog())
            {
                Debug.Log($"[EnsureSrpEarly] SRP already set: {GraphicsSettings.currentRenderPipeline.GetType().Name}");
            }
            return;
        }

        // Load tiny demo URP (create once below)
        var asset = Resources.Load<UniversalRenderPipelineAsset>("Rendering/DemoURP");
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            if (ShouldLog())
            {
                Debug.LogWarning("[EnsureSrpEarly] DemoURP not found in Resources/Rendering; using a transient URP asset.");
            }
        }

        QualitySettings.renderPipeline = asset;        // assign for this run only
        GraphicsSettings.defaultRenderPipeline = asset; // belt & suspenders

        if (ShouldLog())
        {
            Debug.Log($"[EnsureSrpEarly] SRP set to {asset.GetType().Name} before world bootstrap.");
        }
    }

    static bool ShouldLog()
    {
#if UNITY_EDITOR
        return !Application.isBatchMode;
#else
        return Debug.isDebugBuild && !Application.isBatchMode;
#endif
    }
}
#endif
