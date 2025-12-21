#if UNITY_EDITOR || DEVELOPMENT_BUILD
using PureDOTS.Runtime.Core;
using Space4X.Presentation;
using Space4X.Runtime;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Scripting.APIUpdating;

[DefaultExecutionOrder(-10000)]
[MovedFrom(true, null, null, "DemoEnsureSRP")]
public sealed class Space4XLegacyScenarioEnsureSRP : MonoBehaviour
{
    public UniversalRenderPipelineAsset fallbackAsset;

    private void Awake()
    {
        if (!Space4XLegacyScenarioGate.IsEnabled)
        {
            enabled = false;
            return;
        }

        if (RuntimeMode.IsHeadless)
            return;

        if (GraphicsSettings.currentRenderPipeline != null)
        {
            if (ShouldLog())
            {
                Debug.Log($"[RenderDiagEarly] Current SRP (Pre-existing): {GraphicsSettings.currentRenderPipeline.GetType().Name}");
            }
            return;
        }

        if (fallbackAsset != null)
        {
            QualitySettings.renderPipeline = fallbackAsset;
            GraphicsSettings.defaultRenderPipeline = fallbackAsset;
            if (ShouldLog())
            {
                Debug.Log($"[LegacyScenarioEnsureSRP] Render pipeline set to {fallbackAsset.name}.");
                Debug.Log($"[RenderDiagEarly] Current SRP (Assigned): {QualitySettings.renderPipeline?.GetType().Name ?? "None"}");
            }
        }
        else
        {
            Space4XRenderPipelineBootstrap.TryEnsureRenderPipeline(legacyOnly: true);
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
