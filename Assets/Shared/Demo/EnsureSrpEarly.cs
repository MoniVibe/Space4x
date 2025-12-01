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
            Debug.Log($"[EnsureSrpEarly] SRP already set: {GraphicsSettings.currentRenderPipeline.GetType().Name}");
            return;
        }

        // Load tiny demo URP (create once below)
        var asset = Resources.Load<UniversalRenderPipelineAsset>("Rendering/DemoURP");
        if (!asset)
        {
            asset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            Debug.LogWarning("[EnsureSrpEarly] DemoURP not found in Resources/Rendering; using a transient URP asset.");
        }

        QualitySettings.renderPipeline = asset;        // assign for this run only
        GraphicsSettings.defaultRenderPipeline = asset; // belt & suspenders

        Debug.Log($"[EnsureSrpEarly] SRP set to {asset.GetType().Name} before world bootstrap.");
    }
}
#endif
